using System.Globalization;
using System.IO;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Maps;

/// <summary>
/// Resolves a conservative ground height for the local RP character from real
/// OMSI tile spline placement data. It never invents terrain: when no nearby,
/// parseable spline exists the caller keeps the character's current Z.
/// </summary>
internal static class OmsiSplineGroundHeightResolver
{
    private const double SampleSpacingMeters = 2.5d;
    private const double MaxSnapDistanceMeters = 16d;
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, MapCache> MapCaches =
        new(StringComparer.OrdinalIgnoreCase);

    private sealed record SplinePlacement(
        double X,
        double Y,
        double Z,
        double RotationDegrees,
        double Length,
        double Radius,
        double GradientStartPercent,
        double GradientEndPercent,
        double? DeltaH);

    private sealed record MapCache(
        double TileSize,
        IReadOnlyDictionary<(int GridX, int GridY), string> Tiles,
        Dictionary<string, IReadOnlyList<SplinePlacement>> Splines);

    public static bool TryResolve(
        OmsiMapInfo map,
        VehicleTelemetry telemetry,
        double characterLocalX,
        double characterLocalY,
        out double groundZ)
    {
        groundZ = 0d;

        if (telemetry.GridX is not int busGridX ||
            telemetry.GridY is not int busGridY ||
            telemetry.TileX is not double busTileX ||
            telemetry.TileY is not double busTileY ||
            telemetry.LocalX is not double busLocalX ||
            telemetry.LocalY is not double busLocalY ||
            !double.IsFinite(busTileX) ||
            !double.IsFinite(busTileY) ||
            !double.IsFinite(busLocalX) ||
            !double.IsFinite(busLocalY) ||
            !double.IsFinite(characterLocalX) ||
            !double.IsFinite(characterLocalY))
        {
            return false;
        }

        var cache = GetMapCache(map);
        if (cache is null)
        {
            return false;
        }

        var busWorldX = busGridX * cache.TileSize + busTileX;
        var busWorldY = busGridY * cache.TileSize + busTileY;
        var characterWorldX = busWorldX + (characterLocalX - busLocalX);
        var characterWorldY = busWorldY + (characterLocalY - busLocalY);

        if (!double.IsFinite(characterWorldX) || !double.IsFinite(characterWorldY))
        {
            return false;
        }

        var bestDistanceSquared = MaxSnapDistanceMeters * MaxSnapDistanceMeters;
        var found = false;
        var bestZ = 0d;

        for (var gridX = busGridX - 1; gridX <= busGridX + 1; gridX++)
        {
            for (var gridY = busGridY - 1; gridY <= busGridY + 1; gridY++)
            {
                if (!cache.Tiles.TryGetValue((gridX, gridY), out var tilePath))
                {
                    continue;
                }

                foreach (var spline in GetSplines(cache, tilePath))
                {
                    var sampleCount = Math.Clamp(
                        (int)Math.Ceiling(spline.Length / SampleSpacingMeters),
                        2,
                        160);

                    for (var index = 0; index <= sampleCount; index++)
                    {
                        var distance = spline.Length * index / sampleCount;
                        var point = SampleSpline(gridX, gridY, cache.TileSize, spline, distance);
                        var dx = point.X - characterWorldX;
                        var dy = point.Y - characterWorldY;
                        var distanceSquared = dx * dx + dy * dy;
                        if (distanceSquared >= bestDistanceSquared)
                        {
                            continue;
                        }

                        bestDistanceSquared = distanceSquared;
                        bestZ = point.Z;
                        found = true;
                    }
                }
            }
        }

        if (!found || !double.IsFinite(bestZ))
        {
            return false;
        }

        groundZ = bestZ;
        return true;
    }

    private static MapCache? GetMapCache(OmsiMapInfo map)
    {
        lock (CacheLock)
        {
            if (MapCaches.TryGetValue(map.DirectoryPath, out var cached))
            {
                return cached;
            }
        }

        var layout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
        if (layout?.TileSize is not double tileSize ||
            !double.IsFinite(tileSize) ||
            tileSize <= 0d)
        {
            return null;
        }

        IReadOnlyDictionary<(int GridX, int GridY), string> tiles;
        try
        {
            tiles = ReadTileCatalog(map.DirectoryPath, map.GlobalConfigPath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (tiles.Count == 0)
        {
            return null;
        }

        var created = new MapCache(
            tileSize,
            tiles,
            new Dictionary<string, IReadOnlyList<SplinePlacement>>(StringComparer.OrdinalIgnoreCase));

        lock (CacheLock)
        {
            MapCaches[map.DirectoryPath] = created;
        }

        return created;
    }

    private static IReadOnlyDictionary<(int GridX, int GridY), string> ReadTileCatalog(
        string mapDirectory,
        string globalConfigPath)
    {
        var result = new Dictionary<(int GridX, int GridY), string>();
        var lines = File.ReadAllLines(globalConfigPath);

        for (var index = 0; index < lines.Length - 3; index++)
        {
            if (!string.Equals(lines[index].Trim(), "[map]", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(lines[index + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridX) ||
                !int.TryParse(lines[index + 2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridY))
            {
                continue;
            }

            var relativePath = lines[index + 3].Trim()
                .Replace('\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            result[(gridX, gridY)] = Path.IsPathRooted(relativePath)
                ? relativePath
                : Path.Combine(mapDirectory, relativePath);
        }

        return result;
    }

    private static IReadOnlyList<SplinePlacement> GetSplines(MapCache cache, string tilePath)
    {
        lock (CacheLock)
        {
            if (cache.Splines.TryGetValue(tilePath, out var cached))
            {
                return cached;
            }
        }

        var splines = ReadSplines(tilePath);
        lock (CacheLock)
        {
            cache.Splines[tilePath] = splines;
        }

        return splines;
    }

    private static IReadOnlyList<SplinePlacement> ReadSplines(string tilePath)
    {
        var result = new List<SplinePlacement>();

        try
        {
            var lines = File.ReadAllLines(tilePath);
            for (var index = 0; index < lines.Length - 13; index++)
            {
                var keyword = lines[index].Trim();
                var isSplineH = string.Equals(keyword, "[spline_h]", StringComparison.OrdinalIgnoreCase);
                if (!isSplineH &&
                    !string.Equals(keyword, "[spline]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryParseDouble(lines[index + 6], out var x) ||
                    !TryParseDouble(lines[index + 7], out var z) ||
                    !TryParseDouble(lines[index + 8], out var y) ||
                    !TryParseDouble(lines[index + 9], out var rotation) ||
                    !TryParseDouble(lines[index + 10], out var length) ||
                    !TryParseDouble(lines[index + 11], out var radius) ||
                    !TryParseDouble(lines[index + 12], out var gradientStart) ||
                    !TryParseDouble(lines[index + 13], out var gradientEnd) ||
                    !double.IsFinite(x) ||
                    !double.IsFinite(y) ||
                    !double.IsFinite(z) ||
                    !double.IsFinite(rotation) ||
                    !double.IsFinite(length) ||
                    !double.IsFinite(radius) ||
                    !double.IsFinite(gradientStart) ||
                    !double.IsFinite(gradientEnd) ||
                    length <= 0d ||
                    length > 10_000d ||
                    Math.Abs(gradientStart) > 100d ||
                    Math.Abs(gradientEnd) > 100d)
                {
                    continue;
                }

                double? deltaH = null;
                if (isSplineH &&
                    index + 14 < lines.Length &&
                    TryParseDouble(lines[index + 14], out var parsedDeltaH) &&
                    double.IsFinite(parsedDeltaH) &&
                    Math.Abs(parsedDeltaH) <= 1_000d)
                {
                    deltaH = parsedDeltaH;
                }

                result.Add(new SplinePlacement(
                    x,
                    y,
                    z,
                    rotation,
                    length,
                    radius,
                    gradientStart,
                    gradientEnd,
                    deltaH));
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return result;
    }

    private static (double X, double Y, double Z) SampleSpline(
        int gridX,
        int gridY,
        double tileSize,
        SplinePlacement spline,
        double distance)
    {
        var rotation = spline.RotationDegrees * Math.PI / 180d;
        var sinRotation = Math.Sin(rotation);
        var cosRotation = Math.Cos(rotation);

        double localX;
        double localY;
        if (Math.Abs(spline.Radius) > 0.001d)
        {
            var angle = distance / spline.Radius;
            localX = spline.Radius * (1d - Math.Cos(angle));
            localY = spline.Radius * Math.Sin(angle);
        }
        else
        {
            localX = 0d;
            localY = distance;
        }

        var tileX = spline.X + localX * cosRotation + localY * sinRotation;
        var tileY = spline.Y - localX * sinRotation + localY * cosRotation;
        var z = ResolveHeight(spline, distance);

        return (
            gridX * tileSize + tileX,
            gridY * tileSize + tileY,
            z);
    }

    private static double ResolveHeight(SplinePlacement spline, double distance)
    {
        var t = Math.Clamp(distance / spline.Length, 0d, 1d);
        var startSlope = spline.GradientStartPercent / 100d;
        var endSlope = spline.GradientEndPercent / 100d;

        if (spline.DeltaH is double deltaH)
        {
            // [spline_h] carries an explicit endpoint height delta. Hermite
            // interpolation honors that delta and both endpoint gradients.
            var t2 = t * t;
            var t3 = t2 * t;
            var h10 = t3 - 2d * t2 + t;
            var h01 = -2d * t3 + 3d * t2;
            var h11 = t3 - t2;
            return spline.Z +
                   h10 * (startSlope * spline.Length) +
                   h01 * deltaH +
                   h11 * (endSlope * spline.Length);
        }

        // Standard splines expose start/end gradients. Treat the gradient as
        // changing linearly over the segment and integrate it along distance.
        var gradientDelta = endSlope - startSlope;
        return spline.Z +
               startSlope * distance +
               gradientDelta * distance * distance / (2d * spline.Length);
    }

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
}
