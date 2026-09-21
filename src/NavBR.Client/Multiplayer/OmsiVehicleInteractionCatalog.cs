using System.Text;

namespace NavBR.Client.Multiplayer;

internal static class OmsiVehicleInteractionCatalog
{
    private const long MaxTextFileBytes = 4 * 1024 * 1024;
    private const int MaxConfigFiles = 32;
    private const int MaxEvents = 128;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);
    private static readonly object Sync = new();
    private static readonly Dictionary<string, CacheEntry> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Read(
        string? omsiInstallDirectory,
        string? vehiclePath)
    {
        if (!TryResolveVehicleDefinition(
                omsiInstallDirectory,
                vehiclePath,
                out var definitionPath,
                out var vehicleRoot))
        {
            return Array.Empty<string>();
        }

        lock (Sync)
        {
            if (Cache.TryGetValue(definitionPath, out var cached) &&
                cached.ExpiresAtUtc > DateTimeOffset.UtcNow)
            {
                return cached.Events;
            }
        }

        var events = ReadCore(definitionPath, vehicleRoot);
        lock (Sync)
        {
            Cache[definitionPath] = new CacheEntry(
                DateTimeOffset.UtcNow + CacheLifetime,
                events);
        }

        return events;
    }

    public static IReadOnlyList<string> ReadIbisEvents(
        string? omsiInstallDirectory,
        string? vehiclePath)
    {
        return Read(omsiInstallDirectory, vehiclePath)
            .Where(IsLikelyIbisEvent)
            .Take(64)
            .ToArray();
    }

    private static string[] ReadCore(
        string definitionPath,
        string vehicleRoot)
    {
        var events = new HashSet<string>(StringComparer.Ordinal);
        var pendingConfigs = new Queue<string>();
        var seenConfigs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!TryReadLines(definitionPath, out var definitionLines))
        {
            return [];
        }

        EnqueueModelConfigReferences(
            definitionLines,
            Path.GetDirectoryName(definitionPath) ?? vehicleRoot,
            vehicleRoot,
            pendingConfigs,
            seenConfigs);

        while (pendingConfigs.Count > 0 &&
               seenConfigs.Count <= MaxConfigFiles &&
               events.Count < MaxEvents)
        {
            var configPath = pendingConfigs.Dequeue();
            if (!TryReadLines(configPath, out var lines))
            {
                continue;
            }

            for (var index = 0; index < lines.Length && events.Count < MaxEvents; index++)
            {
                if (!string.Equals(
                        lines[index].Trim(),
                        "[mouseevent]",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = ReadNextValue(lines, index + 1);
                if (IsValidEventName(value))
                {
                    events.Add(value!);
                }
            }

            EnqueueConfigReferences(
                lines,
                Path.GetDirectoryName(configPath) ?? vehicleRoot,
                vehicleRoot,
                pendingConfigs,
                seenConfigs);
        }

        return events
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(MaxEvents)
            .ToArray();
    }

    private static void EnqueueModelConfigReferences(
        string[] lines,
        string baseDirectory,
        string vehicleRoot,
        Queue<string> pending,
        HashSet<string> seen)
    {
        for (var index = 0; index < lines.Length; index++)
        {
            if (!string.Equals(
                    lines[index].Trim(),
                    "[model]",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TryEnqueueConfig(
                ReadNextValue(lines, index + 1),
                baseDirectory,
                vehicleRoot,
                pending,
                seen);
        }

        EnqueueConfigReferences(
            lines,
            baseDirectory,
            vehicleRoot,
            pending,
            seen);
    }

    private static void EnqueueConfigReferences(
        string[] lines,
        string baseDirectory,
        string vehicleRoot,
        Queue<string> pending,
        HashSet<string> seen)
    {
        foreach (var line in lines)
        {
            var candidate = NormalizeValue(line);
            if (candidate.EndsWith(
                    ".cfg",
                    StringComparison.OrdinalIgnoreCase))
            {
                TryEnqueueConfig(
                    candidate,
                    baseDirectory,
                    vehicleRoot,
                    pending,
                    seen);
            }
        }
    }

    private static void TryEnqueueConfig(
        string? value,
        string baseDirectory,
        string vehicleRoot,
        Queue<string> pending,
        HashSet<string> seen)
    {
        if (seen.Count >= MaxConfigFiles ||
            string.IsNullOrWhiteSpace(value) ||
            !value.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase) ||
            !TryResolveWithinVehicle(
                value,
                baseDirectory,
                vehicleRoot,
                out var fullPath) ||
            !seen.Add(fullPath))
        {
            return;
        }

        pending.Enqueue(fullPath);
    }

    private static bool TryResolveVehicleDefinition(
        string? omsiInstallDirectory,
        string? vehiclePath,
        out string definitionPath,
        out string vehicleRoot)
    {
        definitionPath = string.Empty;
        vehicleRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(omsiInstallDirectory) ||
            string.IsNullOrWhiteSpace(vehiclePath))
        {
            return false;
        }

        try
        {
            var omsiRoot = Path.GetFullPath(omsiInstallDirectory);
            var vehiclesRoot = Path.GetFullPath(
                Path.Combine(omsiRoot, "Vehicles"));
            var normalized = NormalizeValue(vehiclePath)
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            var fullPath = Path.IsPathRooted(normalized)
                ? Path.GetFullPath(normalized)
                : Path.GetFullPath(Path.Combine(omsiRoot, normalized));

            if (!IsWithin(fullPath, vehiclesRoot) ||
                !File.Exists(fullPath) ||
                (!fullPath.EndsWith(".bus", StringComparison.OrdinalIgnoreCase) &&
                 !fullPath.EndsWith(".ovh", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            definitionPath = fullPath;
            vehicleRoot = Path.GetDirectoryName(fullPath) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(vehicleRoot);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveWithinVehicle(
        string rawValue,
        string baseDirectory,
        string vehicleRoot,
        out string fullPath)
    {
        fullPath = string.Empty;
        try
        {
            var normalized = NormalizeValue(rawValue)
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            var relative = normalized.TrimStart(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

            var candidates = new[]
            {
                Path.Combine(baseDirectory, normalized),
                Path.Combine(vehicleRoot, relative),
                Path.Combine(vehicleRoot, "Model", relative)
            };

            foreach (var candidate in candidates)
            {
                var resolved = Path.GetFullPath(candidate);
                if (IsWithin(resolved, vehicleRoot) &&
                    File.Exists(resolved))
                {
                    fullPath = resolved;
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool TryReadLines(
        string path,
        out string[] lines)
    {
        lines = [];
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists ||
                info.Length < 0 ||
                info.Length > MaxTextFileBytes)
            {
                return false;
            }

            lines = File.ReadAllLines(path, Encoding.Latin1);
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

    private static string? ReadNextValue(
        string[] lines,
        int startIndex)
    {
        for (var index = startIndex; index < lines.Length; index++)
        {
            var value = NormalizeValue(lines[index]);
            if (value.Length == 0 ||
                value.StartsWith(";", StringComparison.Ordinal) ||
                value.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            return value.StartsWith("[", StringComparison.Ordinal)
                ? null
                : value;
        }

        return null;
    }

    private static string NormalizeValue(string value) =>
        value.Trim().Trim('"');

    private static bool IsLikelyIbisEvent(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();

        // Conservative families used by real OMSI IBIS/AFR/ticket-printer/matrix controls.
        // The event must already have been parsed from [mouseevent] in the loaded vehicle.
        return normalized.Contains("ibis", StringComparison.Ordinal) ||
               normalized.Contains("efad", StringComparison.Ordinal) ||
               normalized.Contains("atron", StringComparison.Ordinal) ||
               normalized.Contains("almex", StringComparison.Ordinal) ||
               normalized.Contains("fahrscheindrucker", StringComparison.Ordinal) ||
               normalized.Contains("ticketprinter", StringComparison.Ordinal) ||
               normalized.Contains("ticket_printer", StringComparison.Ordinal) ||
               normalized.Contains("farebox", StringComparison.Ordinal) ||
               normalized.Contains("lawo", StringComparison.Ordinal) ||
               normalized.Contains("krueger", StringComparison.Ordinal) ||
               normalized.Contains("matrix", StringComparison.Ordinal);
    }

    private static bool IsValidEventName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 128)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWithin(
        string path,
        string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) &&
               !relative.Equals("..", StringComparison.Ordinal) &&
               !relative.StartsWith(
                   $"..{Path.DirectorySeparatorChar}",
                   StringComparison.Ordinal);
    }

    private sealed record CacheEntry(
        DateTimeOffset ExpiresAtUtc,
        string[] Events);
}
