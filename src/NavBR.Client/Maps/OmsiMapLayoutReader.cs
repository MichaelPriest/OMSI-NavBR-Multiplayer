using System.Globalization;
using System.IO;

namespace NavBR.Client.Maps;

public static class OmsiMapLayoutReader
{
    private const double StandardTileSize = 300d;

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

        return new OmsiMapLayout(
            MinGridX: gridCoordinates.Min(tile => tile.X),
            MinGridY: gridCoordinates.Min(tile => tile.Y),
            MaxGridX: gridCoordinates.Max(tile => tile.X),
            MaxGridY: gridCoordinates.Max(tile => tile.Y),
            UsesWorldCoordinates: usesWorldCoordinates,
            TileSize: usesWorldCoordinates ? null : StandardTileSize);
    }
}
