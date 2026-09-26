using System.Globalization;
using System.IO;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Maps;

internal readonly record struct OmsiPhysicalRoadAnchor(
    int GridX,
    int GridY,
    double LocalX,
    double LocalY,
    double LocalZ,
    double RotationX,
    double RotationY,
    double RotationZ,
    double RotationW,
    double HeadingDegrees,
    double DistanceMeters,
    string Source,
    string? Name);

/// <summary>
/// Resolves a safe physical-bus bootstrap/road pose from the local OMSI map.
/// Entrypoints come from global.cfg; road snaps come from the actual tile
/// spline geometry. No synthetic road height is invented.
/// </summary>
internal sealed class OmsiPhysicalRoadAnchorResolver
{
    private const double MaxRoadSnapDistanceMeters = 35d;
    private const double MaxEntrypointDistanceMeters = 500d;

    private readonly Func<string?> _omsiInstallDirectorySource;
    private readonly object _sync = new();
    private readonly Dictionary<string, MapData> _mapData =
        new(StringComparer.OrdinalIgnoreCase);
    private string? _catalogRoot;
    private IReadOnlyList<OmsiMapInfo> _maps = Array.Empty<OmsiMapInfo>();

    private sealed record TileRef(
        int Index,
        int GridX,
        int GridY,
        string Path);

    private sealed record Entrypoint(
        int GridX,
        int GridY,
        double LocalX,
        double LocalY,
        double LocalZ,
        double RotationX,
        double RotationY,
        double RotationZ,
        double RotationW,
        string Name);

    private sealed record SplinePlacement(
        double LocalX,
        double HeightY,
        double LocalZ,
        double RotationDegrees,
        double Length,
        double Radius,
        double GradientStartPercent,
        double GradientEndPercent,
        double? DeltaH);

    private sealed record MapData(
        double TileSize,
        IReadOnlyDictionary<(int GridX, int GridY), TileRef> TilesByGrid,
        IReadOnlyDictionary<int, TileRef> TilesByIndex,
        IReadOnlyList<Entrypoint> Entrypoints,
        Dictionary<string, IReadOnlyList<SplinePlacement>> Splines);

    public OmsiPhysicalRoadAnchorResolver(
        Func<string?> omsiInstallDirectorySource)
    {
        _omsiInstallDirectorySource = omsiInstallDirectorySource;
    }

    public bool TryResolveRoadAnchor(
        VehicleTelemetry telemetry,
        out OmsiPhysicalRoadAnchor anchor)
    {
        anchor = default;
        if (!TryResolveMapAndTarget(
                telemetry,
                out var map,
                out var data,
                out var targetGridX,
                out var targetGridY,
                out var targetLocalX,
                out var targetLocalZ,
                out var targetWorldX,
                out var targetWorldZ))
        {
            return false;
        }

        var bestDistanceSquared =
            MaxRoadSnapDistanceMeters * MaxRoadSnapDistanceMeters;
        var found = false;
        OmsiPhysicalRoadAnchor best = default;

        for (var gridX = targetGridX - 1; gridX <= targetGridX + 1; gridX++)
        {
            for (var gridY = targetGridY - 1; gridY <= targetGridY + 1; gridY++)
            {
                if (!data.TilesByGrid.TryGetValue(
                        (gridX, gridY),
                        out var tile))
                {
                    continue;
                }

                foreach (var spline in GetSplines(data, tile.Path))
                {
                    if (!TryProjectOntoSpline(
                            gridX,
                            gridY,
                            data.TileSize,
                            spline,
                            targetWorldX,
                            targetWorldZ,
                            out var distanceAlong,
                            out var distanceSquared,
                            out var snappedLocalX,
                            out var snappedLocalZ,
                            out var headingDegrees) ||
                        distanceSquared >= bestDistanceSquared)
                    {
                        continue;
                    }

                    var heightY = ResolveHeight(spline, distanceAlong);
                    if (!double.IsFinite(heightY))
                    {
                        continue;
                    }

                    var headingRadians =
                        headingDegrees * Math.PI / 180d;
                    var half = headingRadians * 0.5d;

                    bestDistanceSquared = distanceSquared;
                    best = new OmsiPhysicalRoadAnchor(
                        gridX,
                        gridY,
                        snappedLocalX,
                        heightY,
                        snappedLocalZ,
                        0d,
                        Math.Sin(half),
                        0d,
                        Math.Cos(half),
                        NormalizeHeading(headingDegrees),
                        Math.Sqrt(distanceSquared),
                        "spline",
                        Path.GetFileName(tile.Path));
                    found = true;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        anchor = best;
        return true;
    }

    public bool TryResolveEntrypointAnchor(
        VehicleTelemetry telemetry,
        out OmsiPhysicalRoadAnchor anchor)
    {
        anchor = default;
        if (!TryResolveMapAndTarget(
                telemetry,
                out _,
                out var data,
                out _,
                out _,
                out _,
                out _,
                out var targetWorldX,
                out var targetWorldZ) ||
            data.Entrypoints.Count == 0)
        {
            return false;
        }

        var maxDistanceSquared =
            MaxEntrypointDistanceMeters * MaxEntrypointDistanceMeters;
        var bestDistanceSquared = maxDistanceSquared;
        Entrypoint? best = null;

        foreach (var entrypoint in data.Entrypoints)
        {
            var worldX =
                entrypoint.GridX * data.TileSize +
                entrypoint.LocalX;
            var worldZ =
                entrypoint.GridY * data.TileSize +
                entrypoint.LocalZ;
            var dx = worldX - targetWorldX;
            var dz = worldZ - targetWorldZ;
            var distanceSquared = dx * dx + dz * dz;
            if (!double.IsFinite(distanceSquared) ||
                distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            bestDistanceSquared = distanceSquared;
            best = entrypoint;
        }

        if (best is null)
        {
            return false;
        }

        var heading = QuaternionToHeadingDegrees(
            best.RotationX,
            best.RotationY,
            best.RotationZ,
            best.RotationW);
        anchor = new OmsiPhysicalRoadAnchor(
            best.GridX,
            best.GridY,
            best.LocalX,
            best.LocalY,
            best.LocalZ,
            best.RotationX,
            best.RotationY,
            best.RotationZ,
            best.RotationW,
            heading,
            Math.Sqrt(bestDistanceSquared),
            "entrypoint",
            best.Name);
        return true;
    }

    private bool TryResolveMapAndTarget(
        VehicleTelemetry telemetry,
        out OmsiMapInfo map,
        out MapData data,
        out int targetGridX,
        out int targetGridY,
        out double targetLocalX,
        out double targetLocalZ,
        out double targetWorldX,
        out double targetWorldZ)
    {
        map = null!;
        data = null!;
        targetGridX = 0;
        targetGridY = 0;
        targetLocalX = 0d;
        targetLocalZ = 0d;
        targetWorldX = 0d;
        targetWorldZ = 0d;

        var resolvedGridX =
            telemetry.PhysicalGridX ?? telemetry.GridX;
        var resolvedGridY =
            telemetry.PhysicalGridY ?? telemetry.GridY;
        var resolvedLocalX =
            telemetry.LocalX ?? telemetry.TileX;
        // Road plane is X/Z. TileY is the navigation-plane fallback for Z.
        var resolvedLocalZ =
            telemetry.LocalZ ?? telemetry.TileY;

        if (resolvedGridX is not int gridX ||
            resolvedGridY is not int gridY ||
            resolvedLocalX is not double localX ||
            resolvedLocalZ is not double localZ ||
            !double.IsFinite(localX) ||
            !double.IsFinite(localZ) ||
            !TryResolveMap(telemetry, out map) ||
            !TryGetMapData(map, out data))
        {
            return false;
        }

        targetGridX = gridX;
        targetGridY = gridY;
        targetLocalX = localX;
        targetLocalZ = localZ;
        targetWorldX = gridX * data.TileSize + localX;
        targetWorldZ = gridY * data.TileSize + localZ;
        return
            double.IsFinite(targetWorldX) &&
            double.IsFinite(targetWorldZ);
    }

    private bool TryResolveMap(
        VehicleTelemetry telemetry,
        out OmsiMapInfo map)
    {
        map = null!;
        var rootValue = _omsiInstallDirectorySource();
        if (string.IsNullOrWhiteSpace(rootValue))
        {
            return false;
        }

        string root;
        try
        {
            root = Path.GetFullPath(rootValue);
        }
        catch
        {
            return false;
        }

        IReadOnlyList<OmsiMapInfo> maps;
        lock (_sync)
        {
            if (!string.Equals(
                    _catalogRoot,
                    root,
                    StringComparison.OrdinalIgnoreCase))
            {
                _catalogRoot = root;
                _maps = new OmsiMapCatalog().Discover(root);
                _mapData.Clear();
            }

            maps = _maps;
        }

        if (!string.IsNullOrWhiteSpace(telemetry.MapCompatibilityId))
        {
            var exact = maps.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.CompatibilityId,
                    telemetry.MapCompatibilityId,
                    StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                map = exact;
                return true;
            }
        }

        var mapName = telemetry.MapName?.Trim();
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return false;
        }

        var named = maps.FirstOrDefault(candidate =>
            string.Equals(candidate.ConfigName, mapName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.FriendlyName, mapName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.DisplayName, mapName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.FolderName, mapName, StringComparison.OrdinalIgnoreCase));
        if (named is null)
        {
            return false;
        }

        map = named;
        return true;
    }

    private bool TryGetMapData(
        OmsiMapInfo map,
        out MapData data)
    {
        lock (_sync)
        {
            if (_mapData.TryGetValue(map.DirectoryPath, out data!))
            {
                return true;
            }
        }

        var layout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
        if (layout?.TileSize is not double tileSize ||
            !double.IsFinite(tileSize) ||
            tileSize <= 0d)
        {
            data = null!;
            return false;
        }

        try
        {
            var parsed = ReadGlobalMapData(
                map.DirectoryPath,
                map.GlobalConfigPath,
                tileSize);
            if (parsed is null)
            {
                data = null!;
                return false;
            }

            lock (_sync)
            {
                _mapData[map.DirectoryPath] = parsed;
            }

            data = parsed;
            return true;
        }
        catch (IOException)
        {
            data = null!;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            data = null!;
            return false;
        }
    }

    private static MapData? ReadGlobalMapData(
        string mapDirectory,
        string globalConfigPath,
        double tileSize)
    {
        var lines = File.ReadAllLines(globalConfigPath);
        var tilesByGrid =
            new Dictionary<(int GridX, int GridY), TileRef>();
        var tilesByIndex =
            new Dictionary<int, TileRef>();
        var tileIndex = 0;

        for (var index = 0; index < lines.Length - 3; index++)
        {
            if (!string.Equals(
                    lines[index].Trim(),
                    "[map]",
                    StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(
                    lines[index + 1].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var gridX) ||
                !int.TryParse(
                    lines[index + 2].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var gridY))
            {
                continue;
            }

            var relative = lines[index + 3].Trim()
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relative))
            {
                continue;
            }

            var fullPath = Path.IsPathRooted(relative)
                ? relative
                : Path.Combine(mapDirectory, relative);
            var tile = new TileRef(
                tileIndex++,
                gridX,
                gridY,
                fullPath);
            tilesByGrid[(gridX, gridY)] = tile;
            tilesByIndex[tile.Index] = tile;
        }

        if (tilesByGrid.Count == 0)
        {
            return null;
        }

        var entrypoints = ReadEntrypoints(lines, tilesByIndex);
        return new MapData(
            tileSize,
            tilesByGrid,
            tilesByIndex,
            entrypoints,
            new Dictionary<string, IReadOnlyList<SplinePlacement>>(
                StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<Entrypoint> ReadEntrypoints(
        string[] lines,
        IReadOnlyDictionary<int, TileRef> tilesByIndex)
    {
        var headerIndex = Array.FindIndex(
            lines,
            line => string.Equals(
                line.Trim(),
                "[entrypoints]",
                StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0)
        {
            return Array.Empty<Entrypoint>();
        }

        var cursor = headerIndex + 1;
        if (!TryReadValue(lines, ref cursor, out var countText) ||
            !int.TryParse(
                countText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var count) ||
            count is <= 0 or > 10000)
        {
            return Array.Empty<Entrypoint>();
        }

        var result = new List<Entrypoint>(Math.Min(count, 256));
        for (var entryIndex = 0; entryIndex < count; entryIndex++)
        {
            var values = new double[11];
            var valid = true;
            for (var valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                if (!TryReadValue(lines, ref cursor, out var valueText) ||
                    !double.TryParse(
                        valueText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out values[valueIndex]) ||
                    !double.IsFinite(values[valueIndex]))
                {
                    valid = false;
                    break;
                }
            }

            if (!valid ||
                !TryReadValue(lines, ref cursor, out var name))
            {
                break;
            }

            var mapTileIndex = (int)Math.Round(values[10]);
            if (!tilesByIndex.TryGetValue(mapTileIndex, out var tile))
            {
                continue;
            }

            // global.cfg entrypoint pose:
            // object,id,flags,X,Y(vertical),Z,Qx,Qy,Qz,Qw,tileIndex,name
            var entrypoint = new Entrypoint(
                tile.GridX,
                tile.GridY,
                values[3],
                values[4],
                values[5],
                values[6],
                values[7],
                values[8],
                values[9],
                name);
            result.Add(entrypoint);
        }

        return result;
    }

    private static bool TryReadValue(
        string[] lines,
        ref int cursor,
        out string value)
    {
        value = string.Empty;
        while (cursor < lines.Length)
        {
            var candidate = lines[cursor++].Trim();
            if (candidate.Length == 0 ||
                candidate.StartsWith("//", StringComparison.Ordinal) ||
                candidate.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            if (candidate.StartsWith("[", StringComparison.Ordinal))
            {
                return false;
            }

            value = candidate;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<SplinePlacement> GetSplines(
        MapData data,
        string tilePath)
    {
        lock (data.Splines)
        {
            if (data.Splines.TryGetValue(tilePath, out var cached))
            {
                return cached;
            }
        }

        var parsed = ReadSplines(tilePath);
        lock (data.Splines)
        {
            data.Splines[tilePath] = parsed;
        }

        return parsed;
    }

    private static IReadOnlyList<SplinePlacement> ReadSplines(
        string tilePath)
    {
        var result = new List<SplinePlacement>();
        try
        {
            var lines = File.ReadAllLines(tilePath);
            for (var index = 0; index < lines.Length - 13; index++)
            {
                var keyword = lines[index].Trim();
                var isSplineH = string.Equals(
                    keyword,
                    "[spline_h]",
                    StringComparison.OrdinalIgnoreCase);
                if (!isSplineH &&
                    !string.Equals(
                        keyword,
                        "[spline]",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryParseDouble(lines[index + 6], out var x) ||
                    !TryParseDouble(lines[index + 7], out var heightY) ||
                    !TryParseDouble(lines[index + 8], out var z) ||
                    !TryParseDouble(lines[index + 9], out var rotation) ||
                    !TryParseDouble(lines[index + 10], out var length) ||
                    !TryParseDouble(lines[index + 11], out var radius) ||
                    !TryParseDouble(lines[index + 12], out var gradientStart) ||
                    !TryParseDouble(lines[index + 13], out var gradientEnd) ||
                    length <= 0d ||
                    length > 10_000d)
                {
                    continue;
                }

                double? deltaH = null;
                if (isSplineH &&
                    index + 14 < lines.Length &&
                    TryParseDouble(
                        lines[index + 14],
                        out var parsedDeltaH) &&
                    Math.Abs(parsedDeltaH) <= 1_000d)
                {
                    deltaH = parsedDeltaH;
                }

                result.Add(new SplinePlacement(
                    x,
                    heightY,
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

    private static bool TryProjectOntoSpline(
        int gridX,
        int gridY,
        double tileSize,
        SplinePlacement spline,
        double targetWorldX,
        double targetWorldZ,
        out double distanceAlongSpline,
        out double distanceSquared,
        out double snappedLocalX,
        out double snappedLocalZ,
        out double headingDegrees)
    {
        distanceAlongSpline = 0d;
        distanceSquared = double.PositiveInfinity;
        snappedLocalX = 0d;
        snappedLocalZ = 0d;
        headingDegrees = 0d;

        var rotationRadians =
            spline.RotationDegrees * Math.PI / 180d;
        var sinRotation = Math.Sin(rotationRadians);
        var cosRotation = Math.Cos(rotationRadians);
        var originWorldX =
            gridX * tileSize + spline.LocalX;
        var originWorldZ =
            gridY * tileSize + spline.LocalZ;
        var dx = targetWorldX - originWorldX;
        var dz = targetWorldZ - originWorldZ;

        var lateral =
            dx * cosRotation - dz * sinRotation;
        var forward =
            dx * sinRotation + dz * cosRotation;
        if (!double.IsFinite(lateral) ||
            !double.IsFinite(forward))
        {
            return false;
        }

        double localCurveX;
        double localCurveZ;
        double curveAngle = 0d;

        if (Math.Abs(spline.Radius) <= 0.001d)
        {
            distanceAlongSpline =
                Math.Clamp(forward, 0d, spline.Length);
            localCurveX = 0d;
            localCurveZ = distanceAlongSpline;
        }
        else
        {
            var radius = spline.Radius;
            var totalAngle = spline.Length / radius;
            if (!double.IsFinite(totalAngle) ||
                Math.Abs(totalAngle) > Math.PI * 2d + 1e-6d)
            {
                return false;
            }

            var rawAngle = Math.Atan2(
                forward / radius,
                (radius - lateral) / radius);
            var minAngle = Math.Min(0d, totalAngle);
            var maxAngle = Math.Max(0d, totalAngle);
            var middle = (minAngle + maxAngle) * 0.5d;
            var twoPi = Math.PI * 2d;
            var baseTurn =
                (int)Math.Round((middle - rawAngle) / twoPi);

            var best = double.PositiveInfinity;
            var bestDistance = 0d;
            for (var turn = baseTurn - 1;
                 turn <= baseTurn + 1;
                 turn++)
            {
                var angle = rawAngle + turn * twoPi;
                var distance = Math.Clamp(
                    angle * radius,
                    0d,
                    spline.Length);
                var candidateAngle = distance / radius;
                var pointX =
                    radius * (1d - Math.Cos(candidateAngle));
                var pointZ =
                    radius * Math.Sin(candidateAngle);
                var errorX = pointX - lateral;
                var errorZ = pointZ - forward;
                var candidateDistanceSquared =
                    errorX * errorX + errorZ * errorZ;
                if (candidateDistanceSquared < best)
                {
                    best = candidateDistanceSquared;
                    bestDistance = distance;
                }
            }

            foreach (var endpoint in new[] { 0d, spline.Length })
            {
                var endpointAngle = endpoint / radius;
                var pointX =
                    radius * (1d - Math.Cos(endpointAngle));
                var pointZ =
                    radius * Math.Sin(endpointAngle);
                var errorX = pointX - lateral;
                var errorZ = pointZ - forward;
                var candidateDistanceSquared =
                    errorX * errorX + errorZ * errorZ;
                if (candidateDistanceSquared < best)
                {
                    best = candidateDistanceSquared;
                    bestDistance = endpoint;
                }
            }

            distanceAlongSpline = bestDistance;
            curveAngle = bestDistance / radius;
            localCurveX =
                radius * (1d - Math.Cos(curveAngle));
            localCurveZ =
                radius * Math.Sin(curveAngle);
        }

        var roadDx =
            localCurveX * cosRotation +
            localCurveZ * sinRotation;
        var roadDz =
            -localCurveX * sinRotation +
            localCurveZ * cosRotation;
        var snappedWorldX = originWorldX + roadDx;
        var snappedWorldZ = originWorldZ + roadDz;
        var errorWorldX = snappedWorldX - targetWorldX;
        var errorWorldZ = snappedWorldZ - targetWorldZ;

        distanceSquared =
            errorWorldX * errorWorldX +
            errorWorldZ * errorWorldZ;
        snappedLocalX =
            snappedWorldX - gridX * tileSize;
        snappedLocalZ =
            snappedWorldZ - gridY * tileSize;
        headingDegrees =
            NormalizeHeading(
                spline.RotationDegrees +
                curveAngle * 180d / Math.PI);

        return
            double.IsFinite(distanceAlongSpline) &&
            double.IsFinite(distanceSquared) &&
            double.IsFinite(snappedLocalX) &&
            double.IsFinite(snappedLocalZ);
    }

    private static double ResolveHeight(
        SplinePlacement spline,
        double distance)
    {
        var t = Math.Clamp(
            distance / spline.Length,
            0d,
            1d);
        var startSlope =
            spline.GradientStartPercent / 100d;
        var endSlope =
            spline.GradientEndPercent / 100d;

        if (spline.DeltaH is double deltaH)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            var h10 = t3 - 2d * t2 + t;
            var h01 = -2d * t3 + 3d * t2;
            var h11 = t3 - t2;
            return spline.HeightY +
                   h10 * (startSlope * spline.Length) +
                   h01 * deltaH +
                   h11 * (endSlope * spline.Length);
        }

        var gradientDelta = endSlope - startSlope;
        return spline.HeightY +
               startSlope * distance +
               gradientDelta * distance * distance /
               (2d * spline.Length);
    }

    private static double QuaternionToHeadingDegrees(
        double x,
        double y,
        double z,
        double w)
    {
        var length = Math.Sqrt(
            x * x + y * y + z * z + w * w);
        if (!double.IsFinite(length) ||
            length < 0.000001d)
        {
            return 0d;
        }

        x /= length;
        y /= length;
        z /= length;
        w /= length;
        var sinYaw = 2d * (w * y + x * z);
        var cosYaw = 1d - 2d * (y * y + z * z);
        return NormalizeHeading(
            Math.Atan2(sinYaw, cosYaw) *
            (180d / Math.PI));
    }

    private static double NormalizeHeading(double heading)
    {
        var normalized = heading % 360d;
        return normalized < 0d
            ? normalized + 360d
            : normalized;
    }

    private static bool TryParseDouble(
        string value,
        out double result) =>
        double.TryParse(
            value.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result) &&
        double.IsFinite(result);
}
