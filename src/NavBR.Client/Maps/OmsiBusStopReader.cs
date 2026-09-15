using System.Globalization;
using System.IO;

namespace NavBR.Client.Maps;

public sealed record OmsiBusStopPoint(
    int ObjectId,
    int GridX,
    int GridY,
    double TileX,
    double TileY,
    string Name,
    string ObjectPath);

/// <summary>
/// Reads functional OMSI stop objects from the map tiles. The format follows
/// the same public map structures used by OMSI RouteAdvisor, but the parser is
/// implemented independently by NavBR and fails closed on malformed objects.
/// </summary>
public static class OmsiBusStopReader
{
    public static IReadOnlyList<OmsiBusStopPoint> TryRead(OmsiMapInfo map)
    {
        ArgumentNullException.ThrowIfNull(map);

        try
        {
            var result = new List<OmsiBusStopPoint>();
            foreach (var tile in ReadTileCatalog(map.GlobalConfigPath))
            {
                var tilePath = ResolveTilePath(map.DirectoryPath, tile.GridX, tile.GridY, tile.FileName);
                if (tilePath is null)
                {
                    continue;
                }

                ReadTileStops(tilePath, tile.GridX, tile.GridY, result);
            }

            return result;
        }
        catch (IOException)
        {
            return Array.Empty<OmsiBusStopPoint>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<OmsiBusStopPoint>();
        }
    }

    private static IReadOnlyList<(int GridX, int GridY, string? FileName)> ReadTileCatalog(string globalConfigPath)
    {
        var lines = File.ReadAllLines(globalConfigPath);
        var result = new List<(int GridX, int GridY, string? FileName)>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (!string.Equals(lines[index].Trim(), "[map]", StringComparison.OrdinalIgnoreCase) ||
                index + 2 >= lines.Length ||
                !int.TryParse(lines[index + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridX) ||
                !int.TryParse(lines[index + 2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridY))
            {
                continue;
            }

            string? fileName = null;
            for (var lookAhead = index + 3; lookAhead < Math.Min(lines.Length, index + 9); lookAhead++)
            {
                var candidate = lines[lookAhead].Trim().Trim('"');
                if (candidate.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = candidate;
                    break;
                }

                if (candidate.StartsWith("[", StringComparison.Ordinal))
                {
                    break;
                }
            }

            result.Add((gridX, gridY, fileName));
        }

        return result;
    }

    private static string? ResolveTilePath(
        string mapDirectory,
        int gridX,
        int gridY,
        string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var configured = Path.Combine(mapDirectory, fileName.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(configured))
            {
                return configured;
            }
        }

        var conventional = Path.Combine(mapDirectory, $"tile_{gridX}_{gridY}.map");
        return File.Exists(conventional) ? conventional : null;
    }

    private static void ReadTileStops(
        string tilePath,
        int gridX,
        int gridY,
        ICollection<OmsiBusStopPoint> result)
    {
        var lines = File.ReadAllLines(tilePath);
        for (var index = 0; index < lines.Length; index++)
        {
            if (!string.Equals(lines[index].Trim(), "[object]", StringComparison.OrdinalIgnoreCase) ||
                index + 5 >= lines.Length)
            {
                continue;
            }

            // Public OMSI tooling documents this [object] layout for functional
            // bus stops: path at +2, object id at +3, X/Y at +4/+5 and the stop
            // name later in the block. We validate every field before using it.
            var objectPath = lines[index + 2].Trim().Trim('"');
            if (!LooksLikeFunctionalStopObject(objectPath) ||
                !int.TryParse(lines[index + 3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId) ||
                !double.TryParse(lines[index + 4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var tileX) ||
                !double.TryParse(lines[index + 5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var tileY) ||
                !double.IsFinite(tileX) ||
                !double.IsFinite(tileY))
            {
                continue;
            }

            var name = index + 11 < lines.Length ? lines[index + 11].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("[", StringComparison.Ordinal))
            {
                name = "Parada";
            }

            result.Add(new OmsiBusStopPoint(
                objectId,
                gridX,
                gridY,
                tileX,
                tileY,
                name,
                objectPath));
        }
    }

    private static bool LooksLikeFunctionalStopObject(string objectPath)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
        {
            return false;
        }

        var fileName = Path.GetFileName(objectPath).Replace('-', '_').ToLowerInvariant();
        return string.Equals(fileName, "bus_stop.sco", StringComparison.OrdinalIgnoreCase) ||
               fileName.Contains("bus_stop", StringComparison.Ordinal) ||
               fileName.Contains("busstop", StringComparison.Ordinal);
    }
}
