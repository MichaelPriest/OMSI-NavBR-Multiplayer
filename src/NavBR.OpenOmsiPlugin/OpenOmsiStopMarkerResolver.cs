using System.Globalization;
using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal static class OpenOmsiStopMarkerResolver
{
    private const int MaxCachedTrips = 16;
    private static readonly object CacheSync = new();
    private static readonly Dictionary<string, OpenOmsiMapMarkerState[]> Cache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> CacheOrder = new();

    public static OpenOmsiMapMarkerState[] Resolve(
        OpenOmsiContentContext content,
        OpenOmsiTimetableTrip? trip)
    {
        if (!content.MapAvailable ||
            trip is null ||
            trip.StationIds.Length == 0)
        {
            return [];
        }

        var cacheKey = BuildCacheKey(content, trip);
        lock (CacheSync)
        {
            if (Cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }

        var layout = OpenOmsiMapLayoutReader.Read(content);
        if (layout is null)
        {
            return [];
        }

        var wanted = trip.StationIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);
        var found = new OpenOmsiMapMarkerState?[trip.StationIds.Length];

        foreach (var tile in layout.Tiles)
        {
            if (wanted.Count == 0)
            {
                break;
            }

            var tilePath = ResolveRelativeFile(content.MapDirectory!, tile.File);
            if (tilePath is null)
            {
                continue;
            }

            foreach (var marker in ReadStopObjects(tilePath, layout, tile, wanted))
            {
                if (!wanted.TryGetValue(ParseMarkerId(marker.Id), out var index))
                {
                    continue;
                }

                found[index] = marker with
                {
                    Label = index < trip.Stops.Length
                        ? trip.Stops[index]
                        : marker.Label
                };
                wanted.Remove(ParseMarkerId(marker.Id));
            }
        }

        var result = found
            .Where(marker => marker is not null)
            .Select(marker => marker!)
            .ToArray();

        lock (CacheSync)
        {
            if (!Cache.ContainsKey(cacheKey))
            {
                Cache[cacheKey] = result;
                CacheOrder.Enqueue(cacheKey);
                while (CacheOrder.Count > MaxCachedTrips)
                {
                    var stale = CacheOrder.Dequeue();
                    Cache.Remove(stale);
                }
            }
        }

        return result;
    }

    internal static void ResetCache()
    {
        lock (CacheSync)
        {
            Cache.Clear();
            CacheOrder.Clear();
        }
    }

    private static string BuildCacheKey(
        OpenOmsiContentContext content,
        OpenOmsiTimetableTrip trip)
    {
        var map = content.MapDirectory ?? string.Empty;
        var stations = string.Join(',', trip.StationIds);
        var labels = string.Join(
            '\u001f',
            trip.Stops.Select(stop => stop ?? string.Empty));
        return $"{map}|{stations}|{labels}";
    }

    private static IEnumerable<OpenOmsiMapMarkerState> ReadStopObjects(
        string tilePath,
        OpenOmsiMapLayout layout,
        OpenOmsiMapTileRef tile,
        Dictionary<long, int> wanted)
    {
        var lines = File.ReadAllText(tilePath)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var version = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Trim().Equals("[version]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cursor = i;
            version = ParseInt(ReadParameter(lines, ref cursor)) ?? 0;
            break;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Trim().Equals("[object]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cursor = i;
            if (Has(version, 9))
            {
                _ = ReadParameter(lines, ref cursor);
            }

            _ = ReadParameter(lines, ref cursor);
            var id = Has(version, 6)
                ? ParseLong(ReadParameter(lines, ref cursor))
                : null;
            var x = ParseDouble(ReadParameter(lines, ref cursor));
            var y = ParseDouble(ReadParameter(lines, ref cursor));
            var z = ParseDouble(ReadParameter(lines, ref cursor));
            var heading = ParseDouble(ReadParameter(lines, ref cursor));

            if (id is null ||
                !wanted.ContainsKey(id.Value) ||
                x is null ||
                y is null ||
                z is null ||
                heading is null)
            {
                continue;
            }

            var world = OpenOmsiMapLayoutReader.TileLocalToWorld(
                layout,
                tile.X,
                tile.Y,
                x.Value,
                y.Value);

            yield return new(
                Id: $"stop:{id.Value}",
                Kind: "stop",
                Label: null,
                X: world.X,
                Y: world.Y,
                Z: z.Value,
                HeadingDegrees: heading.Value,
                SpeedKph: null);
        }
    }

    private static long ParseMarkerId(string id) =>
        long.TryParse(
            id.AsSpan(id.IndexOf(':') + 1),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : long.MinValue;

    private static bool Has(int version, int minimum) =>
        version == 0 || version >= minimum;

    private static string? ReadParameter(string[] lines, ref int index)
    {
        index++;
        return index < lines.Length ? lines[index] : null;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
        double.IsFinite(parsed)
            ? parsed
            : null;

    private static string? ResolveRelativeFile(
        string root,
        string relative)
    {
        var parts = relative
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = root;

        foreach (var part in parts)
        {
            var direct = Path.Combine(current, part);
            if (File.Exists(direct) || Directory.Exists(direct))
            {
                current = direct;
                continue;
            }

            try
            {
                var match = Directory
                    .EnumerateFileSystemEntries(current)
                    .FirstOrDefault(entry =>
                        string.Equals(
                            Path.GetFileName(entry),
                            part,
                            StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    return null;
                }

                current = match;
            }
            catch
            {
                return null;
            }
        }

        return File.Exists(current)
            ? current
            : null;
    }
}
