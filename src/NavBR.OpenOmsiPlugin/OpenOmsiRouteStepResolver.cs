using System.Globalization;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiRouteStep(
    int Leg,
    int TileIndex,
    long ObjectId,
    int PathIndex,
    double LengthMeters,
    bool IsTrack);

internal static class OpenOmsiRouteStepResolver
{
    public static OpenOmsiRouteStep[] Resolve(
        OpenOmsiContentContext content,
        OpenOmsiTimetableTrip? trip)
    {
        if (!content.TimetableAvailable || trip is null)
        {
            return [];
        }

        var ttData = content.TimetableDirectory!;
        var tripFile = FindFile(ttData, trip.TripName + ".ttp");
        if (tripFile is null)
        {
            return [];
        }

        var parsedTrip = ParseTripRouteSource(tripFile);
        if (parsedTrip is null)
        {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(parsedTrip.Value.TrackName))
        {
            var trackFile = FindFile(
                ttData,
                parsedTrip.Value.TrackName + ".ttr")
                ?? FindFile(ttData, trip.TripName + ".ttr");
            if (trackFile is not null)
            {
                return ParseTrack(trackFile);
            }
        }

        if (parsedTrip.Value.StationIds.Length < 2)
        {
            return [];
        }

        var linksFile = FindFile(ttData, "StnLinks.cfg");
        if (linksFile is null)
        {
            return [];
        }

        return ResolveStationLinks(
            linksFile,
            parsedTrip.Value.StationIds);
    }

    private static OpenOmsiRouteStep[] ParseTrack(string path)
    {
        var lines = ReadLines(path);
        var steps = new List<OpenOmsiRouteStep>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i] != "[track_entry]")
            {
                continue;
            }

            var id = ParseLong(ReadParameter(lines, ref i));
            var pathIndex = ParseInt(ReadParameter(lines, ref i));
            var tileIndex = ParseInt(ReadParameter(lines, ref i));
            _ = ReadParameter(lines, ref i);
            var length = ParseDouble(ReadParameter(lines, ref i));
            _ = ReadParameter(lines, ref i);

            if (id is null ||
                pathIndex is null ||
                tileIndex is null ||
                length is null)
            {
                continue;
            }

            steps.Add(new(
                Leg: 0,
                TileIndex: tileIndex.Value,
                ObjectId: id.Value,
                PathIndex: pathIndex.Value,
                LengthMeters: length.Value,
                IsTrack: true));
        }

        return [.. steps];
    }

    private static OpenOmsiRouteStep[] ResolveStationLinks(
        string path,
        long[] stations)
    {
        var links = ParseLinks(path);
        var steps = new List<OpenOmsiRouteStep>();

        for (var leg = 0; leg < stations.Length - 1; leg++)
        {
            var from = stations[leg];
            var to = stations[leg + 1];
            var link = links.FirstOrDefault(item =>
                item.FromId == from &&
                item.ToId == to);
            if (link is null)
            {
                continue;
            }

            foreach (var entry in link.Entries)
            {
                var step = new OpenOmsiRouteStep(
                    Leg: leg,
                    TileIndex: entry.TileIndex,
                    ObjectId: entry.ObjectId,
                    PathIndex: entry.PathIndex,
                    LengthMeters: entry.LengthMeters,
                    IsTrack: false);

                var last = steps.Count == 0 ? null : steps[^1];
                if (last is not null &&
                    last.TileIndex == step.TileIndex &&
                    last.ObjectId == step.ObjectId &&
                    last.PathIndex == step.PathIndex)
                {
                    continue;
                }

                steps.Add(step);
            }
        }

        return [.. steps];
    }

    private sealed record Link(
        long FromId,
        long ToId,
        List<LinkEntry> Entries);

    private sealed record LinkEntry(
        int TileIndex,
        long ObjectId,
        int PathIndex,
        double LengthMeters);

    private static List<Link> ParseLinks(string path)
    {
        var lines = ReadLines(path);
        var result = new List<Link>();
        Link? current = null;

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i] == "[StnLink]")
            {
                _ = ReadParameter(lines, ref i);
                var from = ParseLong(ReadParameter(lines, ref i));
                var to = ParseLong(ReadParameter(lines, ref i));
                for (var skip = 0; skip < 6; skip++)
                {
                    _ = ReadParameter(lines, ref i);
                }

                if (from is not null && to is not null)
                {
                    current = new Link(from.Value, to.Value, []);
                    result.Add(current);
                }
                else
                {
                    current = null;
                }

                continue;
            }

            if (lines[i] != "[StnLink_entry]" || current is null)
            {
                continue;
            }

            var id = ParseLong(ReadParameter(lines, ref i));
            var pathIndex = ParseInt(ReadParameter(lines, ref i));
            var tileIndex = ParseInt(ReadParameter(lines, ref i));
            var length = ParseDouble(ReadParameter(lines, ref i));
            for (var skip = 0; skip < 3; skip++)
            {
                _ = ReadParameter(lines, ref i);
            }

            if (id is not null &&
                pathIndex is not null &&
                tileIndex is not null &&
                length is not null)
            {
                current.Entries.Add(new(
                    tileIndex.Value,
                    id.Value,
                    pathIndex.Value,
                    length.Value));
            }
        }

        return result;
    }

    private static (string? TrackName, long[] StationIds)?
        ParseTripRouteSource(string path)
    {
        var lines = ReadLines(path);
        string? track = null;
        var stations = new List<long>();
        var found = false;

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i] == "[trip]")
            {
                track = ReadParameter(lines, ref i)?.Trim();
                _ = ReadParameter(lines, ref i);
                _ = ReadParameter(lines, ref i);
                found = true;
                continue;
            }

            if (lines[i] == "[station_typ2]")
            {
                var id = ParseLong(ReadParameter(lines, ref i));
                if (id is not null)
                {
                    stations.Add(id.Value);
                }
            }
        }

        return found
            ? (string.IsNullOrWhiteSpace(track) ? null : track, [.. stations])
            : null;
    }

    private static string? FindFile(string directory, string wantedName)
    {
        var direct = Path.Combine(directory, wantedName);
        if (File.Exists(direct))
        {
            return direct;
        }

        try
        {
            return Directory.EnumerateFiles(directory)
                .FirstOrDefault(path =>
                    string.Equals(
                        Path.GetFileName(path),
                        wantedName,
                        StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadParameter(string[] lines, ref int index)
    {
        index++;
        return index < lines.Length ? lines[index] : null;
    }

    private static string[] ReadLines(string path) =>
        File.ReadAllText(path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static long? ParseLong(string? value) =>
        long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static int? ParseInt(string? value) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static double? ParseDouble(string? value) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed) &&
        double.IsFinite(parsed)
            ? parsed
            : null;
}
