using System.Globalization;
using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiLuaSnapshot(
    long Sequence,
    DateTimeOffset CapturedAtUtc,
    bool HasPosition,
    double? X,
    double? Y,
    double? Z,
    double? HeadingDegrees,
    string? MapName,
    string? Line,
    string? Tour,
    int? TripIndex,
    string? Terminus,
    string? NextStop,
    string? View,
    bool OnFoot,
    bool Paused,
    int? DelaySeconds,
    string? VehicleName,
    double? ClockSeconds,
    int? Day,
    int? Year,
    bool Multiplayer,
    int? TrafficCount,
    double? ReportedSpeedKph,
    double? NextStopArrival,
    double? NextStopDeparture,
    int? TripsCount,
    int? NextStopNumber,
    OpenOmsiNearbyVehicleState[] NearbyVehicles);

internal static class OpenOmsiLuaSnapshotReader
{
    private const long MinimumProbeIntervalMs = 200;
    private const string SnapshotPrefix = "navbr_snapshot = \"";

    private static readonly object Sync = new();
    private static long _lastProbeTickMs;
    private static string? _cachedPath;
    private static DateTime _cachedWriteTimeUtc;
    private static long _cachedLength = -1;
    private static OpenOmsiLuaSnapshot? _cachedSnapshot;

    public static OpenOmsiLuaSnapshot? ReadLatest(bool allowPausedStale)
    {
        lock (Sync)
        {
            var nowTick = Environment.TickCount64;
            if (_lastProbeTickMs > 0 &&
                nowTick >= _lastProbeTickMs &&
                nowTick - _lastProbeTickMs < MinimumProbeIntervalMs)
            {
                return IsFresh(_cachedSnapshot, allowPausedStale)
                    ? _cachedSnapshot
                    : null;
            }

            _lastProbeTickMs = nowTick;
            var path = ResolveSnapshotPath();
            if (path is null)
            {
                _cachedPath = null;
                _cachedSnapshot = null;
                _cachedLength = -1;
                _cachedWriteTimeUtc = default;
                return null;
            }

            try
            {
                var info = new FileInfo(path);
                if (!string.Equals(
                        _cachedPath,
                        path,
                        StringComparison.OrdinalIgnoreCase) ||
                    _cachedLength != info.Length ||
                    _cachedWriteTimeUtc != info.LastWriteTimeUtc)
                {
                    using var stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    var text = reader.ReadToEnd();

                    _cachedSnapshot = TryParse(text);
                    _cachedPath = path;
                    _cachedLength = info.Length;
                    _cachedWriteTimeUtc = info.LastWriteTimeUtc;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return IsFresh(_cachedSnapshot, allowPausedStale)
                ? _cachedSnapshot
                : null;
        }
    }

    internal static OpenOmsiLuaSnapshot? TryParsePayloadForSmoke(string payload)
    {
        return TryParsePayload(payload);
    }

    private static bool IsFresh(
        OpenOmsiLuaSnapshot? snapshot,
        bool allowPausedStale)
    {
        if (snapshot is null)
        {
            return false;
        }

        var age = DateTimeOffset.UtcNow - snapshot.CapturedAtUtc;
        var maxAge = allowPausedStale
            ? TimeSpan.FromMinutes(30)
            : TimeSpan.FromSeconds(6);
        return age >= TimeSpan.FromSeconds(-5) && age <= maxAge;
    }

    private static OpenOmsiLuaSnapshot? TryParse(string text)
    {
        var marker = text.IndexOf(
            SnapshotPrefix,
            StringComparison.Ordinal);
        if (marker < 0)
        {
            return null;
        }

        var start = marker + SnapshotPrefix.Length;
        var end = text.IndexOf('"', start);
        if (end <= start)
        {
            return null;
        }

        // The Lua companion percent-encodes every string field, so the payload
        // itself never contains a quote or a pipe introduced by user content.
        return TryParsePayload(text[start..end]);
    }

    private static OpenOmsiLuaSnapshot? TryParsePayload(string payload)
    {
        var fields = payload.Split('|');
        if (fields.Length < 19 ||
            !int.TryParse(
                fields[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var version) ||
            version is < 1 or > 3 ||
            !long.TryParse(
                fields[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var sequence) ||
            !long.TryParse(
                fields[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var unixSeconds))
        {
            return null;
        }

        if (version == 1 && fields.Length != 19)
        {
            return null;
        }

        if (version == 2 && fields.Length != 28)
        {
            return null;
        }

        if (version == 3 && fields.Length != 30)
        {
            return null;
        }

        DateTimeOffset capturedAt;
        try
        {
            capturedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }

        var hasPosition = fields[3] == "1";
        var x = ParseDouble(fields[4]);
        var y = ParseDouble(fields[5]);
        var z = ParseDouble(fields[6]);
        var heading = ParseDouble(fields[7]);
        if (hasPosition &&
            (x is null || y is null || z is null || heading is null))
        {
            hasPosition = false;
        }

        var nearby = version switch
        {
            >= 3 => ParseNearbyVehicles(fields[29]),
            2 => ParseNearbyVehicles(fields[27]),
            _ => []
        };

        return new OpenOmsiLuaSnapshot(
            sequence,
            capturedAt,
            hasPosition,
            hasPosition ? x : null,
            hasPosition ? y : null,
            hasPosition ? z : null,
            hasPosition ? NormalizeHeading(heading!.Value) : null,
            Decode(fields[8]),
            Decode(fields[9]),
            Decode(fields[10]),
            ParseRoundedInt(fields[11]),
            Decode(fields[12]),
            Decode(fields[13]),
            Decode(fields[14]),
            fields[15] == "1",
            fields[16] == "1",
            ParseRoundedInt(fields[17]),
            Decode(fields[18]),
            version >= 2 ? ParseDouble(fields[19]) : null,
            version >= 2 ? ParseRoundedInt(fields[20]) : null,
            version >= 2 ? ParseRoundedInt(fields[21]) : null,
            version >= 2 && fields[22] == "1",
            version >= 2 ? ParseRoundedInt(fields[23]) : null,
            version >= 2 ? ParseDouble(fields[24]) : null,
            version >= 2 ? ParseDouble(fields[25]) : null,
            version >= 2 ? ParseDouble(fields[26]) : null,
            version >= 3 ? ParseRoundedInt(fields[27]) : null,
            version >= 3 ? ParseRoundedInt(fields[28]) : null,
            nearby);
    }

    private static OpenOmsiNearbyVehicleState[] ParseNearbyVehicles(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var result = new List<OpenOmsiNearbyVehicleState>();
        foreach (var row in value.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            var fields = row.Split(',');
            if (fields.Length != 8)
            {
                continue;
            }

            var id = Decode(fields[0]);
            var kind = Decode(fields[1]);
            var x = ParseDouble(fields[3]);
            var y = ParseDouble(fields[4]);
            var z = ParseDouble(fields[5]);
            var heading = ParseDouble(fields[6]);
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(kind) ||
                x is null ||
                y is null ||
                z is null ||
                heading is null)
            {
                continue;
            }

            var normalizedKind = kind.Trim().ToLowerInvariant();
            if (normalizedKind is not ("ai" or "player"))
            {
                continue;
            }

            result.Add(new OpenOmsiNearbyVehicleState(
                id,
                normalizedKind,
                Decode(fields[2]),
                x.Value,
                y.Value,
                z.Value,
                NormalizeHeading(heading.Value),
                ParseDouble(fields[7])));
        }

        return [.. result];
    }

    private static string? ResolveSnapshotPath()
    {
        foreach (var root in EnumerateContentRoots())
        {
            try
            {
                var path = Path.Combine(
                    root,
                    "Plugins",
                    "NavBR.OpenOmsi",
                    "data.save.lua");
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateContentRoots()
    {
        var seen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var configured = Environment.GetEnvironmentVariable("OMSI_CONTENT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string? normalized = null;
            try
            {
                normalized = Path.GetFullPath(configured.Trim());
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(normalized) &&
                seen.Add(normalized))
            {
                yield return normalized;
            }
        }

        var processPath = Environment.ProcessPath;
        var processDirectory = string.IsNullOrWhiteSpace(processPath)
            ? null
            : Path.GetDirectoryName(processPath);
        if (!string.IsNullOrWhiteSpace(processDirectory))
        {
            var originalOmsiLayout =
                File.Exists(Path.Combine(processDirectory, "Omsi.exe")) &&
                Directory.Exists(Path.Combine(processDirectory, "maps"));
            var primary = originalOmsiLayout
                ? Path.Combine(processDirectory, "openOMSI")
                : processDirectory;
            if (seen.Add(primary))
            {
                yield return primary;
            }

            if (originalOmsiLayout && seen.Add(processDirectory))
            {
                yield return processDirectory;
            }
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        if (OperatingSystem.IsWindows() &&
            (string.IsNullOrWhiteSpace(home) ||
             !Path.IsPathRooted(home)))
        {
            home = Environment.GetEnvironmentVariable("USERPROFILE");
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
        }

        if (!string.IsNullOrWhiteSpace(home))
        {
            var fallback = Path.Combine(home, ".openomsi", "content");
            if (seen.Add(fallback))
            {
                yield return fallback;
            }
        }
    }

    private static double? ParseDouble(string value) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed) &&
        double.IsFinite(parsed)
            ? parsed
            : null;

    private static int? ParseRoundedInt(string value)
    {
        var parsed = ParseDouble(value);
        return parsed is null
            ? null
            : (int)Math.Round(
                Math.Clamp(parsed.Value, int.MinValue, int.MaxValue));
    }

    private static string? Decode(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            var decoded = Uri.UnescapeDataString(value);
            return string.IsNullOrWhiteSpace(decoded)
                ? null
                : decoded.Trim();
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    private static double NormalizeHeading(double value)
    {
        var normalized = value % 360d;
        return normalized < 0d
            ? normalized + 360d
            : normalized;
    }
}
