using System.Globalization;
using System.IO;

namespace NavBR.Client.Maps;

public sealed record OmsiOrderedRouteStops(
    bool RouteResolved,
    IReadOnlyList<string> StopNames);

/// <summary>
/// Reads the active OMSI .ttp preserving station order and display names. This
/// complements OmsiActiveRouteStopReader, which intentionally exposes a set
/// for map filtering. Ambiguous trip resolution fails closed.
/// </summary>
public static class OmsiOrderedRouteStopReader
{
    public static OmsiOrderedRouteStops TryRead(
        OmsiMapInfo map,
        string? activeRoute,
        string? activeLine,
        string? activeDestination)
    {
        ArgumentNullException.ThrowIfNull(map);

        try
        {
            var tripPath = FindTripPath(
                map.DirectoryPath,
                activeRoute,
                activeLine,
                activeDestination);
            if (tripPath is null)
            {
                return Unavailable();
            }

            var stopNamesById = ReadBusStopNamesById(
                Path.GetDirectoryName(tripPath) ?? map.DirectoryPath,
                map.DirectoryPath);
            return new OmsiOrderedRouteStops(
                true,
                ReadOrderedStops(tripPath, stopNamesById));
        }
        catch (IOException)
        {
            return Unavailable();
        }
        catch (UnauthorizedAccessException)
        {
            return Unavailable();
        }
    }

    private static IReadOnlyList<string> ReadOrderedStops(
        string tripPath,
        IReadOnlyDictionary<int, string> busStopNamesById)
    {
        var result = new List<string>();
        var lines = File.ReadAllLines(tripPath);

        for (var index = 0; index < lines.Length; index++)
        {
            var token = lines[index].Trim();
            if (string.Equals(token, "[station]", StringComparison.OrdinalIgnoreCase))
            {
                // OMSI .ttp [station]: id, interval, name, tile index, ...
                if (index + 3 < lines.Length)
                {
                    AddStop(result, lines[index + 3]);
                }
                continue;
            }

            if (string.Equals(token, "[station_typ2]", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < lines.Length &&
                int.TryParse(
                    lines[index + 1].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var stopId) &&
                busStopNamesById.TryGetValue(stopId, out var stopName))
            {
                AddStop(result, stopName);
            }
        }

        return result;
    }

    private static void AddStop(ICollection<string> target, string? rawValue)
    {
        var value = rawValue?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("[", StringComparison.Ordinal))
        {
            return;
        }

        if (target is List<string> list && list.Count > 0 &&
            string.Equals(Normalize(list[^1]), Normalize(value), StringComparison.Ordinal))
        {
            // Some custom TTData repeats the same station through adjacent
            // compatible station tokens. Collapse only consecutive duplicates.
            return;
        }

        target.Add(value);
    }

    private static string? FindTripPath(
        string mapDirectory,
        string? activeRoute,
        string? activeLine,
        string? activeDestination)
    {
        var directories = GetTimetableDirectories(mapDirectory);
        if (directories.Count == 0)
        {
            return null;
        }

        var routeKey = Normalize(activeRoute);
        var lineKey = Normalize(activeLine);
        var destinationKey = Normalize(activeDestination);

        if (routeKey.Length > 0)
        {
            foreach (var directory in directories)
            {
                foreach (var path in EnumerateTripFiles(directory))
                {
                    if (Normalize(Path.GetFileNameWithoutExtension(path)) == routeKey)
                    {
                        return path;
                    }
                }
            }
        }

        var best = new List<string>();
        var lineOnly = new List<string>();
        foreach (var directory in directories)
        {
            foreach (var path in EnumerateTripFiles(directory))
            {
                if (!TryReadTripHeader(path, out var track, out var destination, out var line))
                {
                    continue;
                }

                var fileKey = Normalize(Path.GetFileNameWithoutExtension(path));
                var trackKey = Normalize(track);
                var tripDestinationKey = Normalize(destination);
                var tripLineKey = Normalize(line);

                if (lineKey.Length > 0 && tripLineKey != lineKey)
                {
                    continue;
                }

                var routeMatches = routeKey.Length > 0 &&
                    (fileKey == routeKey || trackKey == routeKey || tripDestinationKey == routeKey);
                var destinationMatches = destinationKey.Length > 0 &&
                    (tripDestinationKey == destinationKey || fileKey == destinationKey);

                if (routeMatches || destinationMatches)
                {
                    best.Add(path);
                }
                else if (lineKey.Length > 0)
                {
                    lineOnly.Add(path);
                }
            }
        }

        var distinctBest = best
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        if (distinctBest.Length == 1)
        {
            return distinctBest[0];
        }

        var distinctLineOnly = lineOnly
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        return distinctLineOnly.Length == 1 ? distinctLineOnly[0] : null;
    }

    private static IReadOnlyDictionary<int, string> ReadBusStopNamesById(
        string tripDirectory,
        string mapDirectory)
    {
        var result = new Dictionary<int, string>();
        var candidates = new[]
        {
            Path.Combine(tripDirectory, "Busstops.cfg"),
            Path.Combine(mapDirectory, "TTData", "Busstops.cfg")
        };

        foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!string.Equals(lines[index].Trim(), "[busstop]", StringComparison.OrdinalIgnoreCase) ||
                    index + 3 >= lines.Length ||
                    !int.TryParse(
                        lines[index + 3].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var id))
                {
                    continue;
                }

                var name = lines[index + 1].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith("[", StringComparison.Ordinal))
                {
                    result[id] = name;
                }
            }
        }

        return result;
    }

    private static bool TryReadTripHeader(
        string path,
        out string trackName,
        out string destination,
        out string line)
    {
        trackName = string.Empty;
        destination = string.Empty;
        line = string.Empty;
        var lines = File.ReadAllLines(path);

        for (var index = 0; index + 3 < lines.Length; index++)
        {
            if (!string.Equals(lines[index].Trim(), "[trip]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            trackName = lines[index + 1].Trim();
            destination = lines[index + 2].Trim();
            line = lines[index + 3].Trim();
            return !string.IsNullOrWhiteSpace(trackName);
        }

        return false;
    }

    private static IReadOnlyList<string> GetTimetableDirectories(string mapDirectory)
    {
        var result = new List<string>();
        var standard = Path.Combine(mapDirectory, "TTData");
        if (Directory.Exists(standard))
        {
            result.Add(standard);
        }

        var chronoRoot = Path.Combine(mapDirectory, "Chrono");
        if (!Directory.Exists(chronoRoot))
        {
            return result;
        }

        try
        {
            result.AddRange(
                Directory.EnumerateDirectories(chronoRoot, "*", SearchOption.TopDirectoryOnly)
                    .Select(directory => Path.Combine(directory, "TTData"))
                    .Where(Directory.Exists));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return result;
    }

    private static IEnumerable<string> EnumerateTripFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.ttp", SearchOption.TopDirectoryOnly).ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => char.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private static OmsiOrderedRouteStops Unavailable() =>
        new(false, Array.Empty<string>());
}
