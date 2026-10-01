using System.Collections.Concurrent;
using System.Security.Cryptography;
using NavBR.Client.Omsi;

namespace NavBR.Client.Telemetry;

internal sealed record OmsiVehicleIdentity(
    string? Name,
    string? RelativePath,
    string? CompatibilityId);

/// <summary>
/// Resolves the active OMSI road-vehicle definition without writing to the
/// simulator. Physical multiplayer needs this identity so the receiving client
/// can request the same .bus/.ovh asset instead of spawning an arbitrary bus.
/// </summary>
internal static class OmsiVehicleIdentityReader
{
    private static readonly ConcurrentDictionary<string, FingerprintCacheEntry> FingerprintCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static OmsiVehicleIdentity Read(
        ReadOnlyProcessMemory memory,
        OmsiProcessInfo? processInfo,
        nint vehicleAddress)
    {
        string? sourceObject = null;
        string? definitionFileName = null;
        string? definitionPath = null;
        string? friendlyName = null;

        try
        {
            var fileObjectAddress = memory.ReadUInt32(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleFileObjectOffset));
            if (fileObjectAddress > 0x10000u)
            {
                sourceObject = memory.ReadDelphiAnsiStringField(nint.Add(
                    ReadOnlyProcessMemory.PointerFromUInt32(fileObjectAddress),
                    Omsi23004MemoryProfile.FileObjectPathOffset),
                    maxCharacters: 1024);
            }
        }
        catch
        {
            // Identity is optional for ordinary telemetry. Physical 3D remains
            // disabled if this read cannot be validated.
        }

        try
        {
            var definitionAddress = memory.ReadUInt32(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.RoadVehicleDefinitionOffset));
            if (definitionAddress > 0x10000u)
            {
                var definitionPointer = ReadOnlyProcessMemory.PointerFromUInt32(definitionAddress);
                definitionFileName = memory.ReadDelphiAnsiStringField(nint.Add(
                    definitionPointer,
                    Omsi23004MemoryProfile.RoadVehicleFileNameOffset),
                    maxCharacters: 1024);
                friendlyName = memory.ReadDelphiAnsiStringField(nint.Add(
                    definitionPointer,
                    Omsi23004MemoryProfile.RoadVehicleFriendlyNameOffset),
                    maxCharacters: 256);
                definitionPath = memory.ReadDelphiAnsiStringField(nint.Add(
                    definitionPointer,
                    Omsi23004MemoryProfile.RoadVehicleMyPathOffset),
                    maxCharacters: 1024);
            }
        }
        catch
        {
            // Same rationale as above: never make read-only telemetry fail just
            // because an addon exposes a different object layout.
        }

        var relativePath = NormalizeVehiclePath(
            processInfo?.InstallDirectory,
            sourceObject,
            definitionFileName,
            definitionPath);
        var compatibilityId = TryFingerprintVehicle(processInfo?.InstallDirectory, relativePath);

        if (string.IsNullOrWhiteSpace(friendlyName) && !string.IsNullOrWhiteSpace(relativePath))
        {
            friendlyName = Path.GetFileNameWithoutExtension(relativePath);
        }

        return new OmsiVehicleIdentity(
            NormalizeOptional(friendlyName),
            NormalizeOptional(relativePath),
            compatibilityId);
    }

    private static string? NormalizeVehiclePath(
        string? omsiRoot,
        string? sourceObject,
        string? definitionFileName,
        string? definitionPath)
    {
        var source = NormalizeSeparators(sourceObject);
        var fileName = NormalizeSeparators(definitionFileName);
        var definition = NormalizeSeparators(definitionPath);

        // OmsiRoadVehicle.FileName is authoritative for the loaded .bus/.ovh
        // definition. MyPath commonly contains only its Vehicles\\... folder.
        // MyFileObject.Obj is retained as a compatibility fallback because it
        // can be absent for the player bus even while RoadVehicle is valid.
        var candidates = new List<string>(capacity: 5);

        AddVehicleDefinitionCandidates(candidates, fileName, definition);
        AddVehicleDefinitionCandidates(candidates, source, definition);

        if (LooksLikeVehicleDefinition(definition))
        {
            candidates.Add(definition!);
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normalized = NormalizeVehicleCandidate(omsiRoot, candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static void AddVehicleDefinitionCandidates(
        List<string> candidates,
        string? fileValue,
        string? definitionPath)
    {
        if (!LooksLikeVehicleDefinition(fileValue))
        {
            return;
        }

        candidates.Add(fileValue!);

        if (string.IsNullOrWhiteSpace(definitionPath) ||
            LooksLikeVehicleDefinition(definitionPath))
        {
            return;
        }

        var fileName = fileValue!
            .Replace('/', '\\')
            .Split('\\', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            candidates.Add(Path.Combine(definitionPath!, fileName));
        }
    }

    private static string? NormalizeVehicleCandidate(
        string? omsiRoot,
        string? value)
    {
        var candidate = NormalizeSeparators(value);
        if (string.IsNullOrWhiteSpace(candidate) ||
            !LooksLikeVehicleDefinition(candidate))
        {
            return null;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(omsiRoot) && Path.IsPathRooted(candidate))
            {
                var fullRoot = Path.GetFullPath(omsiRoot);
                var fullCandidate = Path.GetFullPath(candidate);
                if (fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                {
                    candidate = Path.GetRelativePath(fullRoot, fullCandidate);
                }
            }
        }
        catch
        {
            // Keep the source text and try the Vehicles\\ anchor below.
        }

        candidate = NormalizeSeparators(candidate)?.TrimStart('\\');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var vehicleAnchor = candidate.IndexOf("Vehicles\\", StringComparison.OrdinalIgnoreCase);
        if (vehicleAnchor >= 0)
        {
            candidate = candidate[vehicleAnchor..];
        }

        if (!candidate.StartsWith("Vehicles\\", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        return candidate;
    }

    private static string? TryFingerprintVehicle(string? omsiRoot, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(omsiRoot) || string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            var root = Path.GetFullPath(omsiRoot);
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                return null;
            }

            var info = new FileInfo(fullPath);
            var cacheKey = fullPath;
            if (FingerprintCache.TryGetValue(cacheKey, out var cached) &&
                cached.Length == info.Length &&
                cached.LastWriteUtc == info.LastWriteTimeUtc)
            {
                return cached.CompatibilityId;
            }

            using var stream = File.OpenRead(fullPath);
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            var compatibilityId = $"sha256:{hash}";
            FingerprintCache[cacheKey] = new FingerprintCacheEntry(
                info.Length,
                info.LastWriteTimeUtc,
                compatibilityId);
            return compatibilityId;
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikeVehicleDefinition(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.EndsWith(".bus", StringComparison.OrdinalIgnoreCase) ||
         value.EndsWith(".ovh", StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeSeparators(string? value)
    {
        var normalized = value?.Trim().Replace('/', '\\');
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record FingerprintCacheEntry(
        long Length,
        DateTime LastWriteUtc,
        string CompatibilityId);
}
