using System.Globalization;
using System.Text;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiTimetableTrip(
    string Line,
    string Tour,
    int TripIndex,
    string TripName,
    string? Terminus,
    string? TripLine,
    int ProfileIndex,
    double DepartureMinutes,
    long[] StationIds,
    string[] Stops);

internal static class OpenOmsiTimetableResolver
{
    public static OpenOmsiTimetableTrip? Resolve(
        OpenOmsiContentContext content,
        OpenOmsiLuaSnapshot? snapshot)
    {
        if (!content.TimetableAvailable ||
            snapshot is null ||
            string.IsNullOrWhiteSpace(snapshot.Line) ||
            string.IsNullOrWhiteSpace(snapshot.Tour) ||
            snapshot.TripIndex is null ||
            snapshot.TripIndex <= 0)
        {
            return null;
        }

        var ttData = content.TimetableDirectory!;
        var lineFile = FindFile(
            ttData,
            snapshot.Line.Trim() + ".ttl");
        if (lineFile is null)
        {
            return null;
        }

        var trips = ParseTourTrips(
            lineFile,
            snapshot.Tour.Trim());
        var tripOffset = snapshot.TripIndex.Value - 1;
        if (tripOffset < 0 || tripOffset >= trips.Count)
        {
            return null;
        }

        var selected = trips[tripOffset];
        var tripFile = FindFile(
            ttData,
            selected.TripName + ".ttp");
        if (tripFile is null)
        {
            return null;
        }

        var trip = ParseTrip(tripFile);
        if (trip is null)
        {
            return null;
        }

        var stopNames = ReadBusStopNames(ttData);
        var stops = trip.Value.StationIds
            .Select(id =>
                stopNames.TryGetValue(id, out var name)
                    ? name
                    : id.ToString(CultureInfo.InvariantCulture))
            .ToArray();

        return new(
            Line: snapshot.Line.Trim(),
            Tour: snapshot.Tour.Trim(),
            TripIndex: snapshot.TripIndex.Value,
            TripName: selected.TripName,
            Terminus: trip.Value.Terminus,
            TripLine: trip.Value.Line,
            ProfileIndex: selected.ProfileIndex,
            DepartureMinutes: selected.DepartureMinutes,
            StationIds: trip.Value.StationIds,
            Stops: stops);
    }

    private static List<(string TripName, int ProfileIndex, double DepartureMinutes)>
        ParseTourTrips(string path, string wantedTour)
    {
        var lines = ReadLines(path);
        var result =
            new List<(string TripName, int ProfileIndex, double DepartureMinutes)>();
        var inWantedTour = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line == "[newtour]")
            {
                var number = ReadParameter(lines, ref i);
                _ = ReadParameter(lines, ref i);
                _ = ReadParameter(lines, ref i);
                inWantedTour = string.Equals(
                    number?.Trim(),
                    wantedTour,
                    StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inWantedTour || line != "[addtrip]")
            {
                continue;
            }

            var tripName = ReadParameter(lines, ref i)?.Trim();
            var profileText = ReadParameter(lines, ref i);
            var departureText = ReadParameter(lines, ref i);
            if (string.IsNullOrWhiteSpace(tripName))
            {
                continue;
            }

            _ = int.TryParse(
                profileText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var profileIndex);
            _ = double.TryParse(
                departureText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var departureMinutes);

            result.Add((tripName, profileIndex, departureMinutes));
        }

        return result;
    }

    private static (string? Terminus, string? Line, long[] StationIds)?
        ParseTrip(string path)
    {
        var lines = ReadLines(path);
        string? terminus = null;
        string? lineName = null;
        var stations = new List<long>();
        var foundTrip = false;

        for (var i = 0; i < lines.Length; i++)
        {
            switch (lines[i])
            {
                case "[trip]":
                    _ = ReadParameter(lines, ref i);
                    terminus = ReadParameter(lines, ref i)?.Trim();
                    lineName = ReadParameter(lines, ref i)?.Trim();
                    foundTrip = true;
                    break;

                case "[station_typ2]":
                    var idText = ReadParameter(lines, ref i);
                    if (long.TryParse(
                            idText,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var id))
                    {
                        stations.Add(id);
                    }
                    break;
            }
        }

        return foundTrip
            ? (terminus, lineName, [.. stations])
            : null;
    }

    private static Dictionary<long, string> ReadBusStopNames(string ttData)
    {
        var path = FindFile(ttData, "Busstops.cfg");
        var result = new Dictionary<long, string>();
        if (path is null)
        {
            return result;
        }

        var lines = ReadLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i] != "[busstop]")
            {
                continue;
            }

            var name = ReadParameter(lines, ref i)?.Trim();
            _ = ReadParameter(lines, ref i);
            var idText = ReadParameter(lines, ref i);
            _ = ReadParameter(lines, ref i);
            _ = ReadParameter(lines, ref i);
            _ = ReadParameter(lines, ref i);

            if (!string.IsNullOrWhiteSpace(name) &&
                long.TryParse(
                    idText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id))
            {
                result[id] = name;
            }
        }

        return result;
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
        return index < lines.Length
            ? lines[index]
            : null;
    }

    private static string[] ReadLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var text = DecodeText(bytes);
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        try
        {
            return new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }
}
