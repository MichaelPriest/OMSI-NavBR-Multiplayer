using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace NavBR.Client.Multiplayer;

internal sealed record OmsiVehicleConsistInfo(
    int ExpectedPartCount,
    bool IsComplete);

/// <summary>
/// Resolves a remote vehicle definition against the receiver's OMSI install.
/// Expensive directory scans and hashing stay in the desktop client process,
/// never on OMSI's in-process plugin callback thread.
/// </summary>
internal sealed class OmsiVehicleAssetResolver
{
    private const int MaxVehicleDefinitionsToScan = 10_000;
    private const int MaxConsistDefinitions = 16;
    private const long MaxConsistDefinitionBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan MissingFingerprintRetryDelay =
        TimeSpan.FromSeconds(15);
    private static readonly TimeSpan VehicleIndexRefreshInterval =
        TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ConsistInspectionCacheInterval =
        TimeSpan.FromMinutes(1);

    private readonly Func<string?> _omsiInstallDirectorySource;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly ConcurrentDictionary<string, FingerprintCacheEntry> _fingerprintCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _pathByCompatibilityId =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _missingUntil =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConsistCacheEntry> _consistCache =
        new(StringComparer.OrdinalIgnoreCase);
    private string? _indexedRoot;
    private DateTimeOffset _lastIndexBuildUtc;

    public OmsiVehicleAssetResolver(Func<string?> omsiInstallDirectorySource)
    {
        _omsiInstallDirectorySource = omsiInstallDirectorySource;
    }

    public async Task<string?> ResolveAsync(
        string? reportedPath,
        string? compatibilityId,
        CancellationToken cancellationToken = default)
    {
        var rootValue = _omsiInstallDirectorySource();
        if (string.IsNullOrWhiteSpace(rootValue))
        {
            return null;
        }

        string root;
        try
        {
            root = Path.GetFullPath(rootValue);
        }
        catch
        {
            return null;
        }

        if (!TryNormalizeCompatibilityId(
                compatibilityId,
                out var normalizedCompatibilityId))
        {
            // Physical remote vehicles are content-identified. Never fall
            // back to a path-only spawn for malformed/legacy identities.
            return null;
        }

        var missKey = $"{root}|{normalizedCompatibilityId}";
        if (_missingUntil.TryGetValue(missKey, out var retryAfter) &&
            retryAfter > DateTimeOffset.UtcNow)
        {
            return null;
        }

        await _scanGate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(
                () => ResolveFingerprintCore(
                    root,
                    reportedPath,
                    normalizedCompatibilityId,
                    missKey,
                    cancellationToken),
                cancellationToken);
        }
        finally
        {
            _scanGate.Release();
        }
    }

    public async Task<OmsiVehicleConsistInfo?> InspectConsistAsync(
        string? resolvedVehiclePath,
        CancellationToken cancellationToken = default)
    {
        var rootValue = _omsiInstallDirectorySource();
        if (string.IsNullOrWhiteSpace(rootValue))
        {
            return null;
        }

        string root;
        try
        {
            root = Path.GetFullPath(rootValue);
        }
        catch
        {
            return null;
        }

        if (!TryResolveExactPath(root, resolvedVehiclePath, out var relativePath))
        {
            return null;
        }

        var cacheKey = $"{root}|{relativePath}";
        if (_consistCache.TryGetValue(cacheKey, out var cached) &&
            cached.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            return cached.Info;
        }

        var info = await Task.Run(
            () => InspectConsistCore(root, relativePath, cancellationToken),
            cancellationToken);
        if (info is not null)
        {
            _consistCache[cacheKey] = new ConsistCacheEntry(
                DateTimeOffset.UtcNow + ConsistInspectionCacheInterval,
                info);
        }

        return info;
    }

    private static OmsiVehicleConsistInfo? InspectConsistCore(
        string root,
        string rootRelativePath,
        CancellationToken cancellationToken)
    {
        if (!TryBuildSafeFullPath(root, rootRelativePath, out var rootFullPath))
        {
            return null;
        }

        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Enqueue(rootFullPath);
        var complete = true;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (visited.Count > MaxConsistDefinitions)
            {
                complete = false;
                break;
            }

            if (!TryReadCoupledVehicleReferences(current, out var references))
            {
                complete = false;
                continue;
            }

            foreach (var reference in references)
            {
                if (!TryResolveCoupledVehiclePath(
                        root,
                        current,
                        reference,
                        out var coupledFullPath))
                {
                    complete = false;
                    continue;
                }

                if (!visited.Contains(coupledFullPath))
                {
                    pending.Enqueue(coupledFullPath);
                }
            }
        }

        return new OmsiVehicleConsistInfo(
            ExpectedPartCount: Math.Min(visited.Count, MaxConsistDefinitions),
            IsComplete: complete && pending.Count == 0);
    }

    private static bool TryReadCoupledVehicleReferences(
        string fullPath,
        out string[] references)
    {
        references = Array.Empty<string>();
        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists ||
                info.Length <= 0 ||
                info.Length > MaxConsistDefinitionBytes ||
                (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            var found = new List<string>();
            using var stream = File.Open(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(
                stream,
                Encoding.Latin1,
                detectEncodingFromByteOrderMarks: true);

            while (reader.ReadLine() is { } line)
            {
                cancellationPoint:
                var tag = line.Trim();
                if (!tag.Equals("[couple_back]", StringComparison.OrdinalIgnoreCase) &&
                    !tag.Equals("[couple_front]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                while (reader.ReadLine() is { } valueLine)
                {
                    var value = valueLine.Trim();
                    if (value.Length == 0 ||
                        value.StartsWith("//", StringComparison.Ordinal) ||
                        value.StartsWith(";", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (value.StartsWith("[", StringComparison.Ordinal))
                    {
                        line = value;
                        goto cancellationPoint;
                    }

                    found.Add(value.Trim('"'));
                    break;
                }
            }

            references = found
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryResolveCoupledVehiclePath(
        string root,
        string currentFullPath,
        string reference,
        out string coupledFullPath)
    {
        coupledFullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 1024)
        {
            return false;
        }

        try
        {
            var normalizedRoot = Path.GetFullPath(root);
            var vehiclesRoot = Path.GetFullPath(
                Path.Combine(normalizedRoot, "Vehicles"));
            var vehiclesPrefix = vehiclesRoot.EndsWith(Path.DirectorySeparatorChar)
                ? vehiclesRoot
                : vehiclesRoot + Path.DirectorySeparatorChar;
            var normalizedReference = reference.Trim()
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            string candidate;
            if (normalizedReference.StartsWith(
                    $"Vehicles{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.GetFullPath(
                    Path.Combine(normalizedRoot, normalizedReference));
            }
            else
            {
                var currentDirectory = Path.GetDirectoryName(currentFullPath);
                if (string.IsNullOrWhiteSpace(currentDirectory))
                {
                    return false;
                }

                candidate = Path.GetFullPath(
                    Path.Combine(currentDirectory, normalizedReference));
            }

            if (!candidate.StartsWith(
                    vehiclesPrefix,
                    StringComparison.OrdinalIgnoreCase) ||
                !(candidate.EndsWith(".bus", StringComparison.OrdinalIgnoreCase) ||
                  candidate.EndsWith(".ovh", StringComparison.OrdinalIgnoreCase)) ||
                !File.Exists(candidate))
            {
                return false;
            }

            var info = new FileInfo(candidate);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            coupledFullPath = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string? ResolveFingerprintCore(
        string root,
        string? reportedPath,
        string compatibilityId,
        string missKey,
        CancellationToken cancellationToken)
    {
        if (TryResolveExactPath(root, reportedPath, out var candidate) &&
            TryBuildSafeFullPath(root, candidate, out var candidateFullPath) &&
            TryFingerprintVehicle(candidateFullPath, out var candidateId) &&
            string.Equals(
                candidateId,
                compatibilityId,
                StringComparison.OrdinalIgnoreCase))
        {
            _pathByCompatibilityId[compatibilityId] = candidate;
            _missingUntil.TryRemove(missKey, out _);
            return candidate;
        }

        if (_pathByCompatibilityId.TryGetValue(
                compatibilityId,
                out var cachedRelativePath) &&
            TryResolveExactPath(
                root,
                cachedRelativePath,
                out cachedRelativePath) &&
            TryBuildSafeFullPath(
                root,
                cachedRelativePath,
                out var cachedFullPath) &&
            TryFingerprintVehicle(cachedFullPath, out var cachedId) &&
            string.Equals(
                cachedId,
                compatibilityId,
                StringComparison.OrdinalIgnoreCase))
        {
            _missingUntil.TryRemove(missKey, out _);
            return cachedRelativePath;
        }

        _pathByCompatibilityId.TryRemove(compatibilityId, out _);

        var now = DateTimeOffset.UtcNow;
        var sameIndexedRoot = string.Equals(
            _indexedRoot,
            root,
            StringComparison.OrdinalIgnoreCase);
        if (!sameIndexedRoot)
        {
            _pathByCompatibilityId.Clear();
            _missingUntil.Clear();
            _indexedRoot = root;
            _lastIndexBuildUtc = DateTimeOffset.MinValue;
        }

        if (now - _lastIndexBuildUtc >= VehicleIndexRefreshInterval)
        {
            BuildVehicleIndex(root, cancellationToken);
        }

        if (_pathByCompatibilityId.TryGetValue(
                compatibilityId,
                out var indexedPath) &&
            TryResolveExactPath(root, indexedPath, out indexedPath))
        {
            _missingUntil.TryRemove(missKey, out _);
            return indexedPath;
        }

        CacheMiss(missKey);
        return null;
    }

    private void BuildVehicleIndex(
        string root,
        CancellationToken cancellationToken)
    {
        var vehiclesRoot = Path.Combine(root, "Vehicles");
        if (!Directory.Exists(vehiclesRoot))
        {
            _lastIndexBuildUtc = DateTimeOffset.UtcNow;
            return;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        var inspected = 0;
        try
        {
            foreach (var fullPath in Directory.EnumerateFiles(
                         vehiclesRoot,
                         "*.*",
                         options))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!(fullPath.EndsWith(".bus", StringComparison.OrdinalIgnoreCase) ||
                      fullPath.EndsWith(".ovh", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (++inspected > MaxVehicleDefinitionsToScan)
                {
                    break;
                }

                if (!TryFingerprintVehicle(fullPath, out var localId))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(root, fullPath)
                    .Replace('/', '\\');
                if (!TryNormalizeRelativePath(relativePath, out relativePath))
                {
                    continue;
                }

                _pathByCompatibilityId.TryAdd(localId, relativePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        // Only a completed/non-cancelled pass is considered a fresh index.
        _lastIndexBuildUtc = DateTimeOffset.UtcNow;
    }

    private void CacheMiss(string missKey) =>
        _missingUntil[missKey] =
            DateTimeOffset.UtcNow + MissingFingerprintRetryDelay;

    private static bool TryResolveExactPath(
        string root,
        string? value,
        out string relativePath)
    {
        relativePath = string.Empty;
        if (!TryNormalizeRelativePath(value, out var candidate) ||
            !TryBuildSafeFullPath(root, candidate, out var fullPath) ||
            !File.Exists(fullPath))
        {
            return false;
        }

        relativePath = candidate;
        return true;
    }

    private static bool TryBuildSafeFullPath(
        string root,
        string relativePath,
        out string fullPath)
    {
        fullPath = string.Empty;
        try
        {
            var normalizedRoot = Path.GetFullPath(root);
            var rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
                ? normalizedRoot
                : normalizedRoot + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(
                Path.Combine(normalizedRoot, relativePath));
            if (!candidate.StartsWith(
                    rootPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryNormalizeRelativePath(
        string? value,
        out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024)
        {
            return false;
        }

        var candidate = value.Trim().Replace('/', '\\').TrimStart('\\');
        if (!candidate.StartsWith("Vehicles\\", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("..", StringComparison.Ordinal) ||
            !(candidate.EndsWith(".bus", StringComparison.OrdinalIgnoreCase) ||
              candidate.EndsWith(".ovh", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        relativePath = candidate;
        return true;
    }

    private static bool TryNormalizeCompatibilityId(
        string? value,
        out string compatibilityId)
    {
        compatibilityId = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        const string prefix = "sha256:";
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal) ||
            normalized.Length != prefix.Length + 64)
        {
            return false;
        }

        foreach (var character in normalized.AsSpan(prefix.Length))
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        compatibilityId = normalized;
        return true;
    }

    private bool TryFingerprintVehicle(
        string fullPath,
        out string compatibilityId)
    {
        compatibilityId = string.Empty;
        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return false;
            }

            if (_fingerprintCache.TryGetValue(fullPath, out var cached) &&
                cached.Length == info.Length &&
                cached.LastWriteUtc == info.LastWriteTimeUtc)
            {
                compatibilityId = cached.CompatibilityId;
                return true;
            }

            using var stream = File.Open(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var hash = Convert.ToHexString(SHA256.HashData(stream))
                .ToLowerInvariant();
            compatibilityId = $"sha256:{hash}";
            _fingerprintCache[fullPath] = new FingerprintCacheEntry(
                info.Length,
                info.LastWriteTimeUtc,
                compatibilityId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record FingerprintCacheEntry(
        long Length,
        DateTime LastWriteUtc,
        string CompatibilityId);

    private sealed record ConsistCacheEntry(
        DateTimeOffset ExpiresAtUtc,
        OmsiVehicleConsistInfo Info);
}
