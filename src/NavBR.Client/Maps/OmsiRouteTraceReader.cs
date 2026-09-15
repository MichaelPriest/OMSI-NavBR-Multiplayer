using System.Globalization;
using System.IO;

namespace NavBR.Client.Maps;

public sealed record OmsiRouteTracePoint(int GridX, int GridY, double TileX, double TileY);

/// <summary>
/// Reads the tile sequence of an OMSI timetable track (.ttr). The track data
/// is never modified. The first implementation deliberately uses the centre
/// of each real route tile; this gives a trustworthy route trace without
/// inventing geometry that OMSI has not exposed to NavBR yet.
/// </summary>
public static class OmsiRouteTraceReader
{
    public static IReadOnlyList<OmsiRouteTracePoint> TryRead(
        OmsiMapInfo map,
        OmsiMapLayout layout,
        string? activeTrackName)
    {
        if (string.IsNullOrWhiteSpace(activeTrackName) || layout.TileSize is not double tileSize)
        {
            return Array.Empty<OmsiRouteTracePoint>();
        }

        try
        {
            var trackPath = FindTrackPath(map.DirectoryPath, activeTrackName);
            if (trackPath is null)
            {
                return Array.Empty<OmsiRouteTracePoint>();
            }

            var tilesByGlobalLine = ReadGlobalTileIndex(map.GlobalConfigPath);
            if (tilesByGlobalLine.Count == 0)
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

                // OMSI TTR entry layout begins with:
                // object/id, path index, global.cfg tile index, path id, length...
                if (i + 3 >= lines.Length ||
                    !int.TryParse(lines[i + 3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tileIndex) ||
                    !tilesByGlobalLine.TryGetValue(tileIndex, out var grid))
                {
                    continue;
                }

                if (previousGrid == grid)
                {
                    continue;
                }

                previousGrid = grid;
                points.Add(new OmsiRouteTracePoint(
                    grid.X,
                    grid.Y,
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

    private static string? FindTrackPath(string mapDirectory, string activeTrackName)
    {
        var requested = Path.GetFileNameWithoutExtension(activeTrackName.Trim());
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

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

    private static Dictionary<int, (int X, int Y)> ReadGlobalTileIndex(string globalConfigPath)
    {
        var result = new Dictionary<int, (int X, int Y)>();
        var lines = File.ReadAllLines(globalConfigPath);

        // TTR stores the zero-based source-line index of the [map] block.
        // This is also how established OMSI tooling associates timetable
        // track entries with map tiles.
        for (var i = 0; i < lines.Length - 2; i++)
        {
            if (!string.Equals(lines[i].Trim(), "[map]", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(lines[i + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridX) ||
                !int.TryParse(lines[i + 2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridY))
            {
                continue;
            }

            result[i] = (gridX, gridY);
        }

        return result;
    }

    private static string Normalize(string value) =>
        new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
