using System.Text.Json;

namespace NavBR.Shared.OpenOmsi;

public sealed record OpenOmsiLanRuntimePeer(
    uint Id,
    string? Name,
    string? Bus,
    string? Line,
    string? Destination,
    bool Drawn);

public sealed record OpenOmsiLanRuntimeStatus(
    int? Pid,
    string? Role,
    bool Connected,
    string? Map,
    DateTimeOffset? UpdatedUtc,
    bool Fresh,
    string? SourcePath,
    IReadOnlyList<OpenOmsiLanRuntimePeer> Players,
    string? Error)
{
    public bool IsDrawn(uint lanId) =>
        Fresh &&
        Connected &&
        Players.Any(player =>
            player.Id == lanId &&
            player.Drawn);
}

public static class OpenOmsiLanRuntimeStatusReader
{
    private static readonly object Sync = new();
    private static readonly TimeSpan CacheDuration =
        TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan Freshness =
        TimeSpan.FromSeconds(8);

    private static DateTimeOffset _cachedAtUtc;
    private static string? _cachedKey;
    private static OpenOmsiLanRuntimeStatus? _cached;

    public static OpenOmsiLanRuntimeStatus? Read(
        int? expectedPid = null,
        string? preferredInstanceId = null)
    {
        var key =
            $"{expectedPid?.ToString() ?? "-"}|" +
            $"{NormalizeInstanceId(preferredInstanceId) ?? "-"}";

        lock (Sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (_cached is not null &&
                string.Equals(
                    key,
                    _cachedKey,
                    StringComparison.Ordinal) &&
                now - _cachedAtUtc <= CacheDuration)
            {
                return _cached;
            }

            _cached =
                ReadCore(
                    expectedPid,
                    NormalizeInstanceId(preferredInstanceId),
                    now);
            _cachedKey = key;
            _cachedAtUtc = now;
            return _cached;
        }
    }

    public static string? ResolveLanDirectory()
    {
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

        return string.IsNullOrWhiteSpace(home)
            ? null
            : Path.Combine(home, ".openomsi", "lan");
    }

    private static OpenOmsiLanRuntimeStatus? ReadCore(
        int? expectedPid,
        string? preferredInstanceId,
        DateTimeOffset now)
    {
        var directory = ResolveLanDirectory();
        if (string.IsNullOrWhiteSpace(directory) ||
            !Directory.Exists(directory))
        {
            return null;
        }

        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(preferredInstanceId))
        {
            var preferred = Path.Combine(
                directory,
                preferredInstanceId + ".json");
            if (File.Exists(preferred))
            {
                candidates.Add(preferred);
            }
        }

        if (expectedPid is int pid)
        {
            var byPid = Path.Combine(
                directory,
                pid.ToString() + ".json");
            if (File.Exists(byPid) &&
                !candidates.Contains(
                    byPid,
                    StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(byPid);
            }
        }

        try
        {
            candidates.AddRange(
                Directory.EnumerateFiles(
                        directory,
                        "*.json",
                        SearchOption.TopDirectoryOnly)
                    .Where(path =>
                        !candidates.Contains(
                            path,
                            StringComparer.OrdinalIgnoreCase))
                    .OrderByDescending(path =>
                    {
                        try
                        {
                            return File.GetLastWriteTimeUtc(path);
                        }
                        catch
                        {
                            return DateTime.MinValue;
                        }
                    })
                    .Take(64));
        }
        catch
        {
        }

        OpenOmsiLanRuntimeStatus? best = null;
        foreach (var path in candidates)
        {
            var parsed = TryReadFile(path, now);
            if (parsed is null)
            {
                continue;
            }

            if (expectedPid is int requiredPid &&
                parsed.Pid != requiredPid)
            {
                continue;
            }

            if (best is null ||
                (parsed.UpdatedUtc ?? DateTimeOffset.MinValue) >
                (best.UpdatedUtc ?? DateTimeOffset.MinValue))
            {
                best = parsed;
            }

            if (!string.IsNullOrWhiteSpace(preferredInstanceId) &&
                string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    preferredInstanceId,
                    StringComparison.OrdinalIgnoreCase) &&
                (expectedPid is null ||
                 parsed.Pid == expectedPid))
            {
                break;
            }
        }

        return best;
    }

    private static OpenOmsiLanRuntimeStatus? TryReadFile(
        string path,
        DateTimeOffset now)
    {
        try
        {
            byte[] bytes;
            using (var stream = new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length is <= 1 or > 2_000_000)
                {
                    return null;
                }

                bytes = new byte[stream.Length];
                var offset = 0;
                while (offset < bytes.Length)
                {
                    var read = stream.Read(
                        bytes,
                        offset,
                        bytes.Length - offset);
                    if (read <= 0)
                    {
                        return null;
                    }

                    offset += read;
                }
            }

            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            int? pid = null;
            if (root.TryGetProperty("pid", out var pidNode) &&
                pidNode.TryGetInt32(out var parsedPid))
            {
                pid = parsedPid;
            }

            var role = ReadString(root, "role");
            var connected =
                root.TryGetProperty("connected", out var connectedNode) &&
                connectedNode.ValueKind is
                    JsonValueKind.True or JsonValueKind.False &&
                connectedNode.GetBoolean();
            var map = ReadString(root, "map");

            DateTimeOffset? updatedUtc = null;
            if (root.TryGetProperty(
                    "updated",
                    out var updatedNode) &&
                updatedNode.TryGetInt64(out var unixSeconds))
            {
                try
                {
                    updatedUtc =
                        DateTimeOffset.FromUnixTimeSeconds(
                            unixSeconds);
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }

            var players =
                new List<OpenOmsiLanRuntimePeer>();
            if (root.TryGetProperty(
                    "players",
                    out var playersNode) &&
                playersNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var player in playersNode.EnumerateArray())
                {
                    if (player.ValueKind != JsonValueKind.Object ||
                        !player.TryGetProperty(
                            "id",
                            out var idNode) ||
                        !idNode.TryGetUInt32(out var id))
                    {
                        continue;
                    }

                    var drawn =
                        player.TryGetProperty(
                            "drawn",
                            out var drawnNode) &&
                        drawnNode.ValueKind is
                            JsonValueKind.True or
                            JsonValueKind.False &&
                        drawnNode.GetBoolean();

                    players.Add(
                        new OpenOmsiLanRuntimePeer(
                            id,
                            ReadString(player, "name"),
                            ReadString(player, "bus"),
                            ReadString(player, "line"),
                            ReadString(
                                player,
                                "destination"),
                            drawn));
                }
            }

            var fresh =
                updatedUtc is DateTimeOffset updated &&
                updated <= now + TimeSpan.FromSeconds(10) &&
                now - updated <= Freshness;

            return new OpenOmsiLanRuntimeStatus(
                pid,
                role,
                connected,
                map,
                updatedUtc,
                fresh,
                path,
                players,
                null);
        }
        catch (IOException ex)
        {
            return new OpenOmsiLanRuntimeStatus(
                null,
                null,
                false,
                null,
                null,
                false,
                path,
                [],
                ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new OpenOmsiLanRuntimeStatus(
                null,
                null,
                false,
                null,
                null,
                false,
                path,
                [],
                ex.Message);
        }
        catch (JsonException)
        {
            // The status file is replaced atomically by openOMSI, but an
            // antivirus or sync utility can still expose a transient read.
            // Treat that as unavailable and retry on the next UI refresh.
            return null;
        }
    }

    private static string? ReadString(
        JsonElement element,
        string property)
    {
        if (!element.TryGetProperty(
                property,
                out var node) ||
            node.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = node.GetString();
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizeInstanceId(string? value)
    {
        var candidate = value?.Trim();
        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Length > 64 ||
            candidate.Any(ch =>
                !char.IsAsciiLetterOrDigit(ch) &&
                ch is not '-' and not '_'))
        {
            return null;
        }

        return candidate;
    }
}
