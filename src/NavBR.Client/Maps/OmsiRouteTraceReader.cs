using System.Globalization;
using System.IO;

namespace NavBR.Client.Maps;

public sealed record OmsiRouteTracePoint(int GridX, int GridY, double TileX, double TileY);

/// <summary>
/// Reads the tile sequence of an OMSI timetable track (.ttr). The track data
/// is never modified. The current renderer deliberately uses the centre of
/// each real route tile; this gives a trustworthy route trace without
/// inventing detailed spline geometry that NavBR has not decoded yet.
/// </summary>
public static class OmsiRouteTraceReader
{
    public static IReadOnlyList<OmsiRouteTracePoint> TryRead(
        OmsiMapInfo map,
        OmsiMapLayout layout,
        string? activeTrackOrTarget,
        string? activeLine = null)
    {
        if (layout.TileSize is not double tileSize)
        {
            return Array.Empty<OmsiRouteTracePoint>();
        }

        try
        {
            var trackPath = FindTrackPath(map.DirectoryPath, activeTrackOrTarget);
            if (trackPath is null)
            {
                var resolvedTrackName = ResolveTrackNameFromTrip(
                    map.DirectoryPath,
                    activeLine,
                    activeTrackOrTarget);
                trackPath = FindTrackPath(map.DirectoryPath, resolvedTrackName);
            }

            if (trackPath is null)
            {
                return Array.Empty<OmsiRouteTracePoint>();
            }

            var lines = File.ReadAllLines(trackPath);
            var points = new List<OmsiRouteTracePoint>();
            (int X, int Y)? previousGrid = null;

            for (var i = 0; i < lines.Length; i++)
            {
                if (!string.Equals(lines[i].Trim(), "[track_entry]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // OMSI TTR [track_entry] layout:
                // object/spline id
                // path id
                // tile X
                // tile Y
                // approximate path length
                // flags/reserved
                //
                // Older NavBR builds incorrectly treated tile X as an index
                // into global.cfg. TTR already stores the real grid coordinates.
                if (i + 4 >= lines.Length ||
                    !int.TryParse(
                        lines[i + 3].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var gridX) ||
                    !int.TryParse(
                        lines[i + 4].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var gridY))
                {
                    continue;
                }

                var grid = (X: gridX, Y: gridY);
                if (previousGrid == grid)
                {
                    continue;
                }

                previousGrid = grid;
                points.Add(new OmsiRouteTracePoint(
                    gridX,
                    gridY,
                    tileSize / 2d,
                    tileSize / 2d));
            }

            return points;
        }
        catch (IOException)
        {
            return Array.Empty<OmsiRouteTracePoint>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<OmsiRouteTracePoint>();
        }
    }

    private static string? FindTrackPath(string mapDirectory, string? activeTrackName)
    {
        if (string.IsNullOrWhiteSpace(activeTrackName))
        {
            return null;
        }

        var requested = Path.GetFileNameWithoutExtension(activeTrackName.Trim());
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        var timetableDirectories = GetTimetableDirectories(mapDirectory);
        foreach (var directory in timetableDirectories)
        {
            try
            {
                var exact = Directory
                    .EnumerateFiles(directory, "*.ttr", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path => string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        requested,
                        StringComparison.OrdinalIgnoreCase));
                if (exact is not null)
                {
                    return exact;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        var normalizedRequested = Normalize(requested);
        foreach (var directory in timetableDirectories)
        {
            try
            {
                var normalized = Directory
                    .EnumerateFiles(directory, "*.ttr", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path => Normalize(Path.GetFileNameWithoutExtension(path)) == normalizedRequested);
                if (normalized is not null)
                {
                    return normalized;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return null;
    }

    /// <summary>
    /// OMSI trip files (.ttp) link a trip to a track using the [trip] block:
    /// line 1 = track/TTR name, line 2 = destination, line 3 = line number.
    /// Some runtimes expose the destination but not the track name through the
    /// in-memory timetable record. In that case, line + destination can resolve
    /// the real TTR without guessing route geometry.
    /// </summary>
    private static string? ResolveTrackNameFromTrip(
        string mapDirectory,
        string? activeLine,
        string? activeTarget)
    {
        if (string.IsNullOrWhiteSpace(activeLine) && string.IsNullOrWhiteSpace(activeTarget))
        {
            return null;
        }

        var normalizedLine = Normalize(activeLine ?? string.Empty);
        var normalizedTarget = Normalize(activeTarget ?? string.Empty);
        var fallbackMatches = new List<string>();

        foreach (var directory in GetTimetableDirectories(mapDirectory))
        {
            IEnumerable<string> tripFiles;
            try
            {
                tripFiles = Directory.EnumerateFiles(directory, "*.ttp", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var tripPath in tripFiles)
            {
                try
                {
                    var lines = File.ReadAllLines(tripPath);
                    for (var i = 0; i < lines.Length - 3; i++)
                    {
                        if (!string.Equals(lines[i].Trim(), "[trip]", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var trackName = lines[i + 1].Trim();
                        var target = lines[i + 2].Trim();
                        var line = lines[i + 3].Trim();
                        if (string.IsNullOrWhiteSpace(trackName))
                        {
                            break;
                        }

                        var lineMatches = string.IsNullOrWhiteSpace(normalizedLine) ||
                                          Normalize(line) == normalizedLine;
                        var targetMatches = string.IsNullOrWhiteSpace(normalizedTarget) ||
                                            Normalize(target) == normalizedTarget ||
                                            Normalize(Path.GetFileNameWithoutExtension(tripPath)) == normalizedTarget;

                        if (lineMatches && targetMatches)
                        {
                            return trackName;
                        }

                        if (lineMatches)
                        {
                            fallbackMatches.Add(trackName);
                        }

                        break;
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return fallbackMatches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Count() == 1
            ? fallbackMatches[0]
            : null;
    }

    private static IReadOnlyList<string> GetTimetableDirectories(string mapDirectory)
    {
        var timetableDirectories = new List<string>();
        var baseTimetable = Path.Combine(mapDirectory, "TTData");
        if (Directory.Exists(baseTimetable))
        {
            timetableDirectories.Add(baseTimetable);
        }

        var chronoDirectory = Path.Combine(mapDirectory, "Chrono");
        if (Directory.Exists(chronoDirectory))
        {
            try
            {
                timetableDirectories.AddRange(
                    Directory.EnumerateDirectories(chronoDirectory, "*", SearchOption.TopDirectoryOnly)
                        .Select(path => Path.Combine(path, "TTData"))
                        .Where(Directory.Exists));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return timetableDirectories;
    }

    private static string Normalize(string value) =>
        new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
