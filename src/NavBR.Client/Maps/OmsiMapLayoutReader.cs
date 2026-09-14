using System.Globalization;
using System.IO;

namespace NavBR.Client.Maps;

public static class OmsiMapLayoutReader
{
    private const double StandardTileSize = 300d;
    private const double EarthRadiusMeters = 6378137d;
    private const double RealWorldTileCount = 65536d;

    public static OmsiMapLayout? TryRead(string globalConfigPath)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(globalConfigPath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var usesWorldCoordinates = lines.Any(line =>
            string.Equals(line.Trim(), "[worldcoordinates]", StringComparison.OrdinalIgnoreCase));

        var gridCoordinates = new List<(int X, int Y)>();
        for (var i = 0; i < lines.Length - 2; i++)
        {
            if (!string.Equals(lines[i].Trim(), "[map]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(lines[i + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(lines[i + 2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            {
                continue;
            }

            gridCoordinates.Add((x, y));
        }

        if (gridCoordinates.Count == 0)
        {
            return null;
        }

        var minGridX = gridCoordinates.Min(tile => tile.X);
        var minGridY = gridCoordinates.Min(tile => tile.Y);
        var maxGridX = gridCoordinates.Max(tile => tile.X);
        var maxGridY = gridCoordinates.Max(tile => tile.Y);

        var tileSize = usesWorldCoordinates
            ? CalculateWorldCoordinateTileSize((minGridY + maxGridY) / 2d)
            : StandardTileSize;

        return new OmsiMapLayout(
            MinGridX: minGridX,
            MinGridY: minGridY,
            MaxGridX: maxGridX,
            MaxGridY: maxGridY,
            UsesWorldCoordinates: usesWorldCoordinates,
            TileSize: tileSize);
    }

    /// <summary>
    /// OMSI world-coordinate maps use a Mercator grid with 2^16 tiles around
    /// the globe. Their physical tile width therefore varies with latitude.
    /// The OMSI grid Y value can be converted to latitude directly; using the
    /// map's centre tile keeps the scale stable across the roadmap.
    /// </summary>
    private static double CalculateWorldCoordinateTileSize(double gridY)
    {
        var latitudeRadians = Math.Atan(Math.Sinh(
            Math.PI * (2d * gridY / RealWorldTileCount)));

        var earthCircumference = 2d * Math.PI * EarthRadiusMeters;
        var tileSize = earthCircumference / RealWorldTileCount * Math.Cos(latitudeRadians);

        return double.IsFinite(tileSize) && tileSize > 0d
            ? tileSize
            : StandardTileSize;
    }
}
