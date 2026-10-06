using System.Globalization;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiMapTileRef(
    int Index,
    int X,
    int Y,
    string File);

internal sealed record OpenOmsiMapLayout(
    bool WorldCoordinates,
    double TileSizeMeters,
    OpenOmsiMapTileRef[] Tiles);

internal static class OpenOmsiMapLayoutReader
{
    public static OpenOmsiMapLayout? Read(OpenOmsiContentContext content)
    {
        if (!content.MapAvailable)
        {
            return null;
        }

        var globalPath = Path.Combine(content.MapDirectory!, "global.cfg");
        if (!File.Exists(globalPath))
        {
            return null;
        }

        var lines = ReadLines(globalPath);
        var tiles = new List<OpenOmsiMapTileRef>();
        var worldCoordinates = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Equals("[worldcoordinates]", StringComparison.OrdinalIgnoreCase))
            {
                worldCoordinates = true;
                continue;
            }

            if (!line.Equals("[map]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var x = ParseInt(ReadParameter(lines, ref i));
            var y = ParseInt(ReadParameter(lines, ref i));
            var file = ReadParameter(lines, ref i)?.Trim();
            if (x is null || y is null || string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            tiles.Add(new(
                Index: tiles.Count,
                X: x.Value,
                Y: y.Value,
                File: file));
        }

        if (tiles.Count == 0)
        {
            return null;
        }

        var tileSize = worldCoordinates
            ? WorldTileSize(tiles.Select(tile => tile.Y))
            : 300d;

        return new(
            WorldCoordinates: worldCoordinates,
            TileSizeMeters: tileSize,
            Tiles: [.. tiles]);
    }

    public static (double X, double Y) TileLocalToWorld(
        OpenOmsiMapLayout layout,
        int tileX,
        int tileY,
        double localX,
        double localY)
    {
        var (kx, ky) = TileScale(layout, tileY);
        return (
            tileX * layout.TileSizeMeters + localX * kx,
            tileY * layout.TileSizeMeters + localY * ky);
    }

    public static (double X, double Y) TileScale(
        OpenOmsiMapLayout layout,
        int tileY)
    {
        if (!layout.WorldCoordinates)
        {
            return (1d, 1d);
        }

        var w0 = WorldRowWidth(tileY);
        var w1 = WorldRowWidth(tileY + 1);
        return (
            layout.TileSizeMeters / ((w0 + w1) / 2d),
            layout.TileSizeMeters / w1);
    }

    private static double WorldTileSize(IEnumerable<int> rows)
    {
        var sorted = rows.Order().ToArray();
        if (sorted.Length == 0)
        {
            return 371.9d;
        }

        var row = sorted[sorted.Length / 2];
        return (WorldRowWidth(row) + WorldRowWidth(row + 1)) / 2d;
    }

    private static double WorldRowWidth(int tileY)
    {
        var lat =
            2d * Math.Atan(Math.Exp(Math.Tau * tileY / 65536d)) -
            Math.PI / 2d;
        return 40_075_016.69d * Math.Cos(lat) / 65536d;
    }

    private static string[] ReadLines(string path)
    {
        var bytes = File.ReadAllBytes(path);
        string text;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        else
        {
            text = System.Text.Encoding.UTF8.GetString(bytes);
        }

        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string? ReadParameter(string[] lines, ref int index)
    {
        index++;
        return index < lines.Length ? lines[index] : null;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
}
