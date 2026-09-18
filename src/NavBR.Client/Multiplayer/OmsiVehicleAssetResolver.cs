using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace NavBR.Client.Multiplayer;

/// <summary>
/// Resolves a remote vehicle definition against the receiver's OMSI install.
/// Expensive directory scans and hashing stay in the desktop client process,
/// never on OMSI's in-process plugin callback thread.
/// </summary>
internal sealed class OmsiVehicleAssetResolver
{
    private const int MaxVehicleDefinitionsToScan = 10_000;
    private static readonly TimeSpan MissingFingerprintRetryDelay =
        TimeSpan.FromSeconds(15);
    private static readonly TimeSpan VehicleIndexRefreshInterval =
        TimeSpan.FromMinutes(1);

    private readonly Func<string?> _omsiInstallDirectorySource;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly ConcurrentDictionary<string, FingerprintCacheEntry> _fingerprintCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _pathByCompatibilityId =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _missingUntil =
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

        var hasFingerprint = TryNormalizeCompatibilityId(
            compatibilityId,
            out var normalizedCompatibilityId);

        if (!hasFingerprint)
        {
            return TryResolveExactPath(root, reportedPath, out var exactPath)
                ? exactPath
                : null;
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
}
