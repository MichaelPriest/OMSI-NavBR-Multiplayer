using System.Globalization;
using System.IO;

namespace NavBR.Client.Maps;

internal readonly record struct OmsiRouteTrackEntry(
    int ObjectId,
    int PathId,
    int GridX,
    int GridY,
    double PathLength);

/// <summary>
/// Resolves timetable track entries against the real spline instances stored
/// in OMSI tile .map files. This stays read-only and deliberately falls back to
/// the coarser tile trace when an entry belongs to a scenery object/intersection
/// whose internal path geometry has not been decoded yet.
/// </summary>
internal static class OmsiRouteSplineGeometryReader
{
    private const double SampleSpacingMeters = 8d;
    private const int MaxSamplesPerSpline = 96;

    private sealed record SplinePlacement(
        double X,
        double Y,
        double RotationDegrees,
        double Length,
        double Radius,
        string? SplineFilePath,
        bool Mirrored);

    public static IReadOnlyList<OmsiRouteTracePoint> TryBuild(
        OmsiMapInfo map,
        OmsiMapLayout layout,
        IReadOnlyList<OmsiRouteTrackEntry> entries)
    {
        if (entries.Count == 0 || layout.TileSize is not double tileSize || tileSize <= 0d)
        {
            return Array.Empty<OmsiRouteTracePoint>();
        }

        Dictionary<(int X, int Y), string> tileFiles;
        try
        {
            tileFiles = ReadTileCatalog(map.DirectoryPath, map.GlobalConfigPath);
        }
        catch (IOException)
        {
            return Array.Empty<OmsiRouteTracePoint>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<OmsiRouteTracePoint>();
        }

        if (tileFiles.Count == 0)
        {
            return Array.Empty<OmsiRouteTracePoint>();
        }

        var omsiRoot = TryGetOmsiRoot(map.DirectoryPath);
        var splineCache = new Dictionary<string, IReadOnlyDictionary<int, SplinePlacement>>(
            StringComparer.OrdinalIgnoreCase);
        var pathOffsetCache = new Dictionary<string, IReadOnlyList<double>>(
            StringComparer.OrdinalIgnoreCase);
        var result = new List<OmsiRouteTracePoint>();

        foreach (var entry in entries)
        {
            if (!tileFiles.TryGetValue((entry.GridX, entry.GridY), out var tilePath) ||
                !File.Exists(tilePath))
            {
                continue;
            }

            if (!splineCache.TryGetValue(tilePath, out var splines))
            {
                splines = ReadSplines(tilePath, omsiRoot);
                splineCache[tilePath] = splines;
            }

            if (!splines.TryGetValue(entry.ObjectId, out var spline))
            {
                // Many junctions are scenery objects with their own path table.
                // Those are intentionally skipped here; neighboring spline
                // points still bridge the junction without inventing geometry.
                continue;
            }

            var pathOffset = ResolvePathOffset(spline, entry.PathId, pathOffsetCache);
            var segment = SampleSpline(entry.GridX, entry.GridY, spline, pathOffset);
            AppendOriented(result, segment, tileSize);
        }

        return result.Count >= 2
            ? result
            : Array.Empty<OmsiRouteTracePoint>();
    }

    private static Dictionary<(int X, int Y), string> ReadTileCatalog(
        string mapDirectory,
        string globalConfigPath)
    {
        var result = new Dictionary<(int X, int Y), string>();
        var lines = File.ReadAllLines(globalConfigPath);

        for (var i = 0; i < lines.Length - 3; i++)
        {
            if (!string.Equals(lines[i].Trim(), "[map]", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(lines[i + 1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridX) ||
                !int.TryParse(lines[i + 2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridY))
            {
                continue;
            }

            var relativePath = lines[i + 3].Trim();
            if (string.IsNullOrWhiteSpace(relativePath) || result.ContainsKey((gridX, gridY)))
            {
                continue;
            }

            var normalizedPath = NormalizeFilePath(relativePath);
            var fullPath = Path.IsPathRooted(normalizedPath)
                ? normalizedPath
                : Path.Combine(mapDirectory, normalizedPath);

            result[(gridX, gridY)] = fullPath;
        }

        return result;
    }

    private static IReadOnlyDictionary<int, SplinePlacement> ReadSplines(
        string tilePath,
        string? omsiRoot)
    {
        var result = new Dictionary<int, SplinePlacement>();

        try
        {
            var lines = File.ReadAllLines(tilePath);
            for (var i = 0; i < lines.Length - 11; i++)
            {
                var keyword = lines[i].Trim();
                if (!string.Equals(keyword, "[spline]", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(keyword, "[spline_h]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Common prefix of [spline] and [spline_h]:
                // 1 metadata, 2 .sli file, 3 id, 4 previous, 5 next,
                // 6 X, 7 Z, 8 Y, 9 rotation, 10 length, 11 radius.
                if (!int.TryParse(lines[i + 3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
                    !TryParseDouble(lines[i + 6], out var x) ||
                    !TryParseDouble(lines[i + 8], out var y) ||
                    !TryParseDouble(lines[i + 9], out var rotation) ||
                    !TryParseDouble(lines[i + 10], out var length) ||
                    !TryParseDouble(lines[i + 11], out var radius) ||
                    !double.IsFinite(x) ||
                    !double.IsFinite(y) ||
                    !double.IsFinite(rotation) ||
                    !double.IsFinite(length) ||
                    !double.IsFinite(radius) ||
                    length <= 0d ||
                    length > 10000d)
                {
                    continue;
                }

                var splineFilePath = ResolveSplineFile(omsiRoot, lines[i + 2].Trim());
                var mirrored = IsMirrored(lines, i + 12);
                result.TryAdd(id, new SplinePlacement(
                    x,
                    y,
                    rotation,
                    length,
                    radius,
                    splineFilePath,
                    mirrored));
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

    private static double ResolvePathOffset(
        SplinePlacement spline,
        int pathId,
        Dictionary<string, IReadOnlyList<double>> pathOffsetCache)
    {
        if (pathId < 0 ||
            string.IsNullOrWhiteSpace(spline.SplineFilePath) ||
            !File.Exists(spline.SplineFilePath))
        {
            return 0d;
        }

        if (!pathOffsetCache.TryGetValue(spline.SplineFilePath, out var offsets))
        {
            offsets = ReadPathOffsets(spline.SplineFilePath);
            pathOffsetCache[spline.SplineFilePath] = offsets;
        }

        if (pathId >= offsets.Count)
        {
            return 0d;
        }

        var offset = offsets[pathId];
        return spline.Mirrored ? -offset : offset;
    }

    private static IReadOnlyList<double> ReadPathOffsets(string splineFilePath)
    {
        var result = new List<double>();

        try
        {
            var lines = File.ReadAllLines(splineFilePath);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!string.Equals(lines[i].Trim(), "[path]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // [path] -> type, lateral X, Z, width, direction.
                // Preserve the list index even when a malformed offset occurs,
                // because TTR PathId is zero-based against this path list.
                result.Add(
                    i + 2 < lines.Length && TryParseDouble(lines[i + 2], out var offset)
                        ? offset
                        : 0d);
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

    private static List<OmsiRouteTracePoint> SampleSpline(
        int gridX,
        int gridY,
        SplinePlacement spline,
        double pathOffset)
    {
        var sampleCount = Math.Clamp(
            (int)Math.Ceiling(spline.Length / SampleSpacingMeters),
            2,
            MaxSamplesPerSpline);

        var points = new List<OmsiRouteTracePoint>(sampleCount + 1);
        var rotation = spline.RotationDegrees * Math.PI / 180d;
        var sinRotation = Math.Sin(rotation);
        var cosRotation = Math.Cos(rotation);

        for (var index = 0; index <= sampleCount; index++)
        {
            var distance = spline.Length * index / sampleCount;

            double localX;
            double localY;
            if (Math.Abs(spline.Radius) > 0.001d)
            {
                var angle = distance / spline.Radius;
                var radiusFromCurveCentre = pathOffset - spline.Radius;
                localX = radiusFromCurveCentre * Math.Cos(angle) + spline.Radius;
                localY = -radiusFromCurveCentre * Math.Sin(angle);
            }
            else
            {
                localX = pathOffset;
                localY = distance;
            }

            // OMSI stores spline rotation clockwise from the tile Y axis.
            var tileX = spline.X + localX * cosRotation + localY * sinRotation;
            var tileY = spline.Y - localX * sinRotation + localY * cosRotation;

            if (double.IsFinite(tileX) && double.IsFinite(tileY))
            {
                points.Add(new OmsiRouteTracePoint(gridX, gridY, tileX, tileY));
            }
        }

        return points;
    }

    private static void AppendOriented(
        List<OmsiRouteTracePoint> target,
        List<OmsiRouteTracePoint> segment,
        double tileSize)
    {
        if (segment.Count == 0)
        {
            return;
        }

        if (target.Count > 0 && segment.Count > 1)
        {
            var previous = target[^1];
            var distanceToStart = DistanceSquared(previous, segment[0], tileSize);
            var distanceToEnd = DistanceSquared(previous, segment[^1], tileSize);
            if (distanceToEnd < distanceToStart)
            {
                segment.Reverse();
            }
        }

        foreach (var point in segment)
        {
            if (target.Count == 0 || DistanceSquared(target[^1], point, tileSize) > 0.25d)
            {
                target.Add(point);
            }
        }
    }

    private static double DistanceSquared(
        OmsiRouteTracePoint left,
        OmsiRouteTracePoint right,
        double tileSize)
    {
        var leftX = left.GridX * tileSize + left.TileX;
        var leftY = left.GridY * tileSize + left.TileY;
        var rightX = right.GridX * tileSize + right.TileX;
        var rightY = right.GridY * tileSize + right.TileY;
        var dx = leftX - rightX;
        var dy = leftY - rightY;
        return dx * dx + dy * dy;
    }

    private static string? TryGetOmsiRoot(string mapDirectory)
    {
        try
        {
            var mapsDirectory = Directory.GetParent(mapDirectory);
            return mapsDirectory?.Parent?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveSplineFile(string? omsiRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            var normalizedPath = NormalizeFilePath(relativePath);
            if (Path.IsPathRooted(normalizedPath))
            {
                return normalizedPath;
            }

            return string.IsNullOrWhiteSpace(omsiRoot)
                ? null
                : Path.GetFullPath(Path.Combine(omsiRoot, normalizedPath));
        }
        catch
        {
            return null;
        }
    }

    private static bool IsMirrored(string[] lines, int startIndex)
    {
        var endIndex = Math.Min(lines.Length, startIndex + 20);
        for (var i = startIndex; i < endIndex; i++)
        {
            var value = lines[i].Trim();
            if (value.StartsWith("[", StringComparison.Ordinal))
            {
                break;
            }

            if (string.Equals(value, "mirror", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeFilePath(string value) =>
        value
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
}
