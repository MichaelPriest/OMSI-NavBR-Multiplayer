using System.Globalization;
using System.IO;

namespace NavBR.Client.Maps;

/// <summary>
/// Resolves TTR entries that reference a scenery object (typically a crossing)
/// instead of a tile spline. OMSI crossing SCOs expose their drivable geometry
/// through ordered [path]/[path_2] blocks; TTR PathId addresses that list.
/// </summary>
internal readonly record struct OmsiSceneryRoadPoint(
    double TileX,
    double TileY,
    double Z);

internal static class OmsiRouteSceneryPathGeometryReader
{
    private const double SampleSpacingMeters = 6d;
    private const int MaxSamplesPerPath = 96;
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, IReadOnlyDictionary<int, SceneryPlacement>> ObjectCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, IReadOnlyList<SceneryPath>> PathCache =
        new(StringComparer.OrdinalIgnoreCase);

    private sealed record SceneryPlacement(
        double X,
        double Y,
        double Z,
        double RotationDegrees,
        string? ObjectFilePath);

    private readonly record struct SceneryPath(
        double X,
        double Y,
        double Z,
        double HeadingDegrees,
        double Radius,
        double Length,
        bool IsValid);

    public static IReadOnlyList<OmsiRouteTracePoint> TryResolve(
        string mapDirectory,
        string tilePath,
        OmsiRouteTrackEntry entry)
    {
        var objects = GetObjects(mapDirectory, tilePath);
        if (!objects.TryGetValue(entry.ObjectId, out var placement) ||
            string.IsNullOrWhiteSpace(placement.ObjectFilePath) ||
            !File.Exists(placement.ObjectFilePath) ||
            entry.PathId < 0)
        {
            return Array.Empty<OmsiRouteTracePoint>();
        }

        var paths = GetPaths(placement.ObjectFilePath);
        if (entry.PathId >= paths.Count)
        {
            return Array.Empty<OmsiRouteTracePoint>();
        }

        var path = paths[entry.PathId];
        return path.IsValid
            ? SamplePath(entry.GridX, entry.GridY, placement, path)
            : Array.Empty<OmsiRouteTracePoint>();
    }

    public static IReadOnlyList<IReadOnlyList<OmsiSceneryRoadPoint>> ReadAllRoadPaths(
        string mapDirectory,
        string tilePath)
    {
        const int maxPathsPerTile = 2048;
        var result = new List<IReadOnlyList<OmsiSceneryRoadPoint>>();
        foreach (var placement in GetObjects(mapDirectory, tilePath).Values)
        {
            if (string.IsNullOrWhiteSpace(placement.ObjectFilePath) ||
                !File.Exists(placement.ObjectFilePath))
            {
                continue;
            }

            foreach (var path in GetPaths(placement.ObjectFilePath))
            {
                if (!path.IsValid)
                {
                    continue;
                }

                var sampled = SampleRoadPath3D(placement, path);
                if (sampled.Count >= 2)
                {
                    result.Add(sampled);
                    if (result.Count >= maxPathsPerTile)
                    {
                        return result;
                    }
                }
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<int, SceneryPlacement> GetObjects(
        string mapDirectory,
        string tilePath)
    {
        lock (CacheLock)
        {
            if (ObjectCache.TryGetValue(tilePath, out var cached))
            {
                return cached;
            }
        }

        var result = new Dictionary<int, SceneryPlacement>();
        try
        {
            var omsiRoot = TryGetOmsiRoot(mapDirectory);
            var lines = File.ReadAllLines(tilePath);

            for (var i = 0; i < lines.Length - 7; i++)
            {
                if (!string.Equals(lines[i].Trim(), "[object]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // [object] common layout:
                // metadata, SCO file, object ID, X, Y, Z, rotation, pitch, bank...
                if (!int.TryParse(lines[i + 3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
                    !TryParseDouble(lines[i + 4], out var x) ||
                    !TryParseDouble(lines[i + 5], out var y) ||
                    !TryParseDouble(lines[i + 6], out var z) ||
                    !TryParseDouble(lines[i + 7], out var rotation) ||
                    !double.IsFinite(x) ||
                    !double.IsFinite(y) ||
                    !double.IsFinite(z) ||
                    !double.IsFinite(rotation))
                {
                    continue;
                }

                var objectFile = ResolveOmsiFile(omsiRoot, lines[i + 2].Trim());
                result.TryAdd(id, new SceneryPlacement(x, y, z, rotation, objectFile));
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        lock (CacheLock)
        {
            ObjectCache[tilePath] = result;
        }

        return result;
    }

    private static IReadOnlyList<SceneryPath> GetPaths(string objectFilePath)
    {
        lock (CacheLock)
        {
            if (PathCache.TryGetValue(objectFilePath, out var cached))
            {
                return cached;
            }
        }

        var result = new List<SceneryPath>();
        try
        {
            var lines = File.ReadAllLines(objectFilePath);
            for (var i = 0; i < lines.Length; i++)
            {
                var keyword = lines[i].Trim();
                if (!string.Equals(keyword, "[path]", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(keyword, "[path_2]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Both path formats share the geometry prefix:
                // X, Y, Z, heading, radius, length, gradients...
                var x = 0d;
                var y = 0d;
                var z = 0d;
                var heading = 0d;
                var radius = 0d;
                var length = 0d;
                var valid = i + 6 < lines.Length &&
                            TryParseDouble(lines[i + 1], out x) &&
                            TryParseDouble(lines[i + 2], out y) &&
                            TryParseDouble(lines[i + 3], out z) &&
                            TryParseDouble(lines[i + 4], out heading) &&
                            TryParseDouble(lines[i + 5], out radius) &&
                            TryParseDouble(lines[i + 6], out length) &&
                            double.IsFinite(x) &&
                            double.IsFinite(y) &&
                            double.IsFinite(z) &&
                            double.IsFinite(heading) &&
                            double.IsFinite(radius) &&
                            double.IsFinite(length) &&
                            length > 0d &&
                            length <= 10000d;

                // Add an entry even when malformed so zero-based PathId indexes
                // stay aligned with OMSI's path list.
                result.Add(valid
                    ? new SceneryPath(x, y, z, heading, radius, length, true)
                    : new SceneryPath(0d, 0d, 0d, 0d, 0d, 0d, false));
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        lock (CacheLock)
        {
            PathCache[objectFilePath] = result;
        }

        return result;
    }

    private static IReadOnlyList<OmsiRouteTracePoint> SamplePath(
        int gridX,
        int gridY,
        SceneryPlacement placement,
        SceneryPath path)
    {
        var sampleCount = Math.Clamp(
            (int)Math.Ceiling(path.Length / SampleSpacingMeters),
            2,
            MaxSamplesPerPath);
        var result = new List<OmsiRouteTracePoint>(sampleCount + 1);

        var pathHeading = path.HeadingDegrees * Math.PI / 180d;
        var pathSin = Math.Sin(pathHeading);
        var pathCos = Math.Cos(pathHeading);
        var objectRotation = placement.RotationDegrees * Math.PI / 180d;
        var objectSin = Math.Sin(objectRotation);
        var objectCos = Math.Cos(objectRotation);

        for (var index = 0; index <= sampleCount; index++)
        {
            var distance = path.Length * index / sampleCount;
            double curveX;
            double curveY;

            if (Math.Abs(path.Radius) > 0.001d)
            {
                var angle = distance / path.Radius;
                curveX = path.Radius * (1d - Math.Cos(angle));
                curveY = path.Radius * Math.Sin(angle);
            }
            else
            {
                curveX = 0d;
                curveY = distance;
            }

            // First transform from the path's local frame into SCO coordinates.
            var objectLocalX = path.X + curveX * pathCos + curveY * pathSin;
            var objectLocalY = path.Y - curveX * pathSin + curveY * pathCos;

            // Then apply the scenery object's placement inside the OMSI tile.
            var tileX = placement.X + objectLocalX * objectCos + objectLocalY * objectSin;
            var tileY = placement.Y - objectLocalX * objectSin + objectLocalY * objectCos;

            if (double.IsFinite(tileX) && double.IsFinite(tileY))
            {
                result.Add(new OmsiRouteTracePoint(gridX, gridY, tileX, tileY));
            }
        }

        return result;
    }

    private static IReadOnlyList<OmsiSceneryRoadPoint> SampleRoadPath3D(
        SceneryPlacement placement,
        SceneryPath path)
    {
        var sampleCount = Math.Clamp(
            (int)Math.Ceiling(path.Length / SampleSpacingMeters),
            2,
            MaxSamplesPerPath);
        var result = new List<OmsiSceneryRoadPoint>(sampleCount + 1);
        var pathHeading = path.HeadingDegrees * Math.PI / 180d;
        var pathSin = Math.Sin(pathHeading);
        var pathCos = Math.Cos(pathHeading);
        var objectRotation = placement.RotationDegrees * Math.PI / 180d;
        var objectSin = Math.Sin(objectRotation);
        var objectCos = Math.Cos(objectRotation);

        for (var index = 0; index <= sampleCount; index++)
        {
            var distance = path.Length * index / sampleCount;
            double curveX;
            double curveY;
            if (Math.Abs(path.Radius) > 0.001d)
            {
                var angle = distance / path.Radius;
                curveX = path.Radius * (1d - Math.Cos(angle));
                curveY = path.Radius * Math.Sin(angle);
            }
            else
            {
                curveX = 0d;
                curveY = distance;
            }

            var objectLocalX = path.X + curveX * pathCos + curveY * pathSin;
            var objectLocalY = path.Y - curveX * pathSin + curveY * pathCos;
            var tileX = placement.X + objectLocalX * objectCos + objectLocalY * objectSin;
            var tileY = placement.Y - objectLocalX * objectSin + objectLocalY * objectCos;
            var z = placement.Z + path.Z;
            if (double.IsFinite(tileX) && double.IsFinite(tileY) && double.IsFinite(z))
            {
                result.Add(new OmsiSceneryRoadPoint(tileX, tileY, z));
            }
        }

        return result;
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

    private static string? ResolveOmsiFile(string? omsiRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            var normalizedPath = relativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
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

    private static bool TryParseDouble(string value, out double result) =>
        double.TryParse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
}
