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
    private const double MaxRoadSnapDistanceMeters = 18d;
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

    private readonly record struct RoadPath(
        double LateralOffset,
        double HeightOffset,
        int Direction);

    private sealed record SplinePlacement(
        double LocalX,
        double HeightY,
        double LocalZ,
        double RotationDegrees,
        double Length,
        double Radius,
        double GradientStartPercent,
        double GradientEndPercent,
        double? DeltaH,
        IReadOnlyList<RoadPath> RoadPaths);

    private sealed record MapData(
        double TileSize,
        IReadOnlyDictionary<(int GridX, int GridY), TileRef> TilesByGrid,
        IReadOnlyDictionary<int, TileRef> TilesByIndex,
        IReadOnlyList<Entrypoint> Entrypoints,
        Dictionary<string, IReadOnlyList<SplinePlacement>> Splines,
        Dictionary<string, IReadOnlyList<IReadOnlyList<OmsiSceneryRoadPoint>>> SceneryRoadPaths);

    public OmsiPhysicalRoadAnchorResolver(
        Func<string?> omsiInstallDirectorySource)
    {
        _omsiInstallDirectorySource = omsiInstallDirectorySource;
    }

    public bool TryResolveOpenOmsiWorldAnchor(
        VehicleTelemetry telemetry,
        out OmsiPhysicalRoadAnchor anchor)
    {
        anchor = default;
        if (!double.IsFinite(telemetry.X) ||
            !double.IsFinite(telemetry.Y) ||
            !double.IsFinite(telemetry.Z) ||
            !double.IsFinite(telemetry.HeadingDegrees) ||
            !TryResolveMap(telemetry, out var map) ||
            !TryGetMapData(map, out var data) ||
            !double.IsFinite(data.TileSize) ||
            data.TileSize <= 0d)
        {
            return false;
        }

        // openOMSI LAN uses X/Y on the ground plane and Z as vertical.
        // OMSI's physical Kachel space uses X/Z on the ground plane and Y
        // as vertical. The world origin is the same map origin; only the
        // axis convention differs.
        var worldX = telemetry.X;
        var worldGroundZ = telemetry.Y;
        var verticalY = telemetry.Z;

        var gridXDouble =
            Math.Floor(worldX / data.TileSize);
        var gridYDouble =
            Math.Floor(worldGroundZ / data.TileSize);
        if (gridXDouble < int.MinValue ||
            gridXDouble > int.MaxValue ||
            gridYDouble < int.MinValue ||
            gridYDouble > int.MaxValue)
        {
            return false;
        }

        var gridX = (int)gridXDouble;
        var gridY = (int)gridYDouble;
        if (!data.TilesByGrid.TryGetValue(
                (gridX, gridY),
                out var tile))
        {
            return false;
        }

        var localX =
            worldX -
            gridX * data.TileSize;
        var localZ =
            worldGroundZ -
            gridY * data.TileSize;
        if (!double.IsFinite(localX) ||
            !double.IsFinite(localZ) ||
            localX < -0.01d ||
            localZ < -0.01d ||
            localX > data.TileSize + 0.01d ||
            localZ > data.TileSize + 0.01d)
        {
            return false;
        }

        var heading =
            NormalizeHeading(
                telemetry.HeadingDegrees);
        var headingRadians =
            heading * Math.PI / 180d;
        var half =
            headingRadians * 0.5d;

        anchor = new OmsiPhysicalRoadAnchor(
            gridX,
            gridY,
            Math.Clamp(localX, 0d, data.TileSize),
            verticalY,
            Math.Clamp(localZ, 0d, data.TileSize),
            0d,
            Math.Sin(half),
            0d,
            Math.Cos(half),
            heading,
            0d,
            "openomsi-world",
            Path.GetFileName(tile.Path));
        return true;
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

        var maxDistanceSquared =
            MaxRoadSnapDistanceMeters * MaxRoadSnapDistanceMeters;
        var bestScore = double.PositiveInfinity;
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
                    var roadPaths = spline.RoadPaths.Count > 0
                        ? spline.RoadPaths
                        : [new RoadPath(0d, 0d, 2)];

                    foreach (var roadPath in roadPaths)
                    {
                        if (!TryProjectOntoSpline(
                                gridX,
                                gridY,
                                data.TileSize,
                                spline,
                                roadPath.LateralOffset,
                                targetWorldX,
                                targetWorldZ,
                                out var distanceAlong,
                                out var distanceSquared,
                                out var snappedLocalX,
                                out var snappedLocalZ,
                                out var headingDegrees) ||
                            distanceSquared >= maxDistanceSquared)
                        {
                            continue;
                        }

                        var headingDeltaDegrees = RoadTravelDeltaDegrees(
                            telemetry.HeadingDegrees,
                            headingDegrees,
                            roadPath.Direction);
                        if (headingDeltaDegrees > 70d)
                        {
                            continue;
                        }

                        var headingPenalty = headingDeltaDegrees / 15d;
                        var score =
                            distanceSquared +
                            headingPenalty * headingPenalty;
                        if (!double.IsFinite(score) ||
                            score >= bestScore)
                        {
                            continue;
                        }

                        var heightY =
                            ResolveHeight(spline, distanceAlong) +
                            roadPath.HeightOffset;
                        if (!double.IsFinite(heightY))
                        {
                            continue;
                        }

                        var effectiveHeading = ResolveTravelHeading(
                            telemetry.HeadingDegrees,
                            headingDegrees,
                            roadPath.Direction);
                        var headingRadians =
                            effectiveHeading * Math.PI / 180d;
                        var half = headingRadians * 0.5d;

                        bestScore = score;
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
                            effectiveHeading,
                            Math.Sqrt(distanceSquared),
                            spline.RoadPaths.Count > 0
                                ? "vehicle-path"
                                : "spline-center-fallback",
                            Path.GetFileName(tile.Path));
                        found = true;
                    }
                }

                foreach (var sceneryPath in GetSceneryRoadPaths(
                             data,
                             map.DirectoryPath,
                             tile.Path))
                {
                    for (var pointIndex = 1;
                         pointIndex < sceneryPath.Count;
                         pointIndex++)
                    {
                        var previous = sceneryPath[pointIndex - 1];
                        var current = sceneryPath[pointIndex];
                        if (!TryProjectOntoRoadSegment(
                                gridX,
                                gridY,
                                data.TileSize,
                                previous,
                                current,
                                targetWorldX,
                                targetWorldZ,
                                out var distanceSquared,
                                out var snappedLocalX,
                                out var snappedLocalZ,
                                out var heightY,
                                out var headingDegrees) ||
                            distanceSquared >= maxDistanceSquared)
                        {
                            continue;
                        }

                        var headingDeltaDegrees = RoadTravelDeltaDegrees(
                            telemetry.HeadingDegrees,
                            headingDegrees,
                            previous.Direction);
                        if (headingDeltaDegrees > 70d)
                        {
                            continue;
                        }

                        var headingPenalty = headingDeltaDegrees / 15d;
                        var score =
                            distanceSquared +
                            headingPenalty * headingPenalty;
                        if (!double.IsFinite(score) ||
                            score >= bestScore)
                        {
                            continue;
                        }

                        var effectiveHeading = ResolveTravelHeading(
                            telemetry.HeadingDegrees,
                            headingDegrees,
                            previous.Direction);
                        var headingRadians =
                            effectiveHeading * Math.PI / 180d;
                        var half = headingRadians * 0.5d;

                        bestScore = score;
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
                            effectiveHeading,
                            Math.Sqrt(distanceSquared),
                            "scenery-vehicle-path",
                            Path.GetFileName(tile.Path));
                        found = true;
                    }
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
                StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<IReadOnlyList<OmsiSceneryRoadPoint>>>(
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

    private static IReadOnlyList<IReadOnlyList<OmsiSceneryRoadPoint>>
        GetSceneryRoadPaths(
            MapData data,
            string mapDirectory,
            string tilePath)
    {
        lock (data.SceneryRoadPaths)
        {
            if (data.SceneryRoadPaths.TryGetValue(
                    tilePath,
                    out var cached))
            {
                return cached;
            }
        }

        var parsed =
            OmsiRouteSceneryPathGeometryReader.ReadAllRoadPaths(
                mapDirectory,
                tilePath);

        lock (data.SceneryRoadPaths)
        {
            data.SceneryRoadPaths[tilePath] = parsed;
        }

        return parsed;
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
        var omsiRoot = TryGetOmsiRootFromTile(tilePath);
        var roadPathCache =
            new Dictionary<string, IReadOnlyList<RoadPath>>(
                StringComparer.OrdinalIgnoreCase);
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
                    length > 10_000d ||
                    Math.Abs(gradientStart) > 100d ||
                    Math.Abs(gradientEnd) > 100d)
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

                var splineFile = ResolveOmsiAssetPath(
                    omsiRoot,
                    lines[index + 2].Trim());
                IReadOnlyList<RoadPath> roadPaths =
                    Array.Empty<RoadPath>();
                if (!string.IsNullOrWhiteSpace(splineFile) &&
                    File.Exists(splineFile))
                {
                    if (!roadPathCache.TryGetValue(
                            splineFile,
                            out roadPaths))
                    {
                        roadPaths = ReadRoadVehiclePaths(splineFile);
                        roadPathCache[splineFile] = roadPaths;
                    }

                    if (IsMirrored(lines, index + 14) &&
                        roadPaths.Count > 0)
                    {
                        roadPaths = roadPaths
                            .Select(path => path with
                            {
                                LateralOffset = -path.LateralOffset
                            })
                            .ToArray();
                    }
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
                    deltaH,
                    roadPaths));
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
        double pathOffset,
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
            localCurveX = pathOffset;
            localCurveZ = distanceAlongSpline;
        }
        else
        {
            var radius = spline.Radius;
            var pathRadius = pathOffset - radius;
            if (Math.Abs(pathRadius) <= 0.001d)
            {
                return false;
            }

            var totalAngle = spline.Length / radius;
            if (!double.IsFinite(totalAngle) ||
                Math.Abs(totalAngle) > Math.PI * 2d + 1e-6d)
            {
                return false;
            }

            var rawAngle = Math.Atan2(
                -forward / pathRadius,
                (lateral - radius) / pathRadius);
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
                    pathRadius * Math.Cos(candidateAngle) +
                    radius;
                var pointZ =
                    -pathRadius * Math.Sin(candidateAngle);
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
                    pathRadius * Math.Cos(endpointAngle) +
                    radius;
                var pointZ =
                    -pathRadius * Math.Sin(endpointAngle);
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
                pathRadius * Math.Cos(curveAngle) +
                radius;
            localCurveZ =
                -pathRadius * Math.Sin(curveAngle);
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

    private static IReadOnlyList<RoadPath> ReadRoadVehiclePaths(
        string splineFile)
    {
        var result = new List<RoadPath>();
        try
        {
            var lines = File.ReadAllLines(
                splineFile,
                System.Text.Encoding.Latin1);
            for (var index = 0; index < lines.Length; index++)
            {
                var keyword = lines[index].Trim();
                if (!string.Equals(
                        keyword,
                        "[path]",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        keyword,
                        "[path_2]",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 5 >= lines.Length ||
                    !int.TryParse(
                        lines[index + 1].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var pathType) ||
                    pathType != 0 ||
                    !TryParseDouble(
                        lines[index + 2],
                        out var lateralOffset) ||
                    !TryParseDouble(
                        lines[index + 3],
                        out var heightOffset) ||
                    !int.TryParse(
                        lines[index + 5].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var direction) ||
                    direction is < 0 or > 2 ||
                    Math.Abs(lateralOffset) > 50d ||
                    Math.Abs(heightOffset) > 10d)
                {
                    continue;
                }

                result.Add(
                    new RoadPath(
                        lateralOffset,
                        heightOffset,
                        direction));
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return result
            .Distinct()
            .ToArray();
    }

    private static string? TryGetOmsiRootFromTile(string tilePath)
    {
        try
        {
            var mapDirectory =
                Directory.GetParent(tilePath);
            var mapsDirectory =
                mapDirectory?.Parent;
            return mapsDirectory?.Parent?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveOmsiAssetPath(
        string? omsiRoot,
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(omsiRoot) ||
            string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            var normalized = relativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(normalized)
                ? normalized
                : Path.GetFullPath(
                    Path.Combine(omsiRoot, normalized));
        }
        catch
        {
            return null;
        }
    }

    private static bool IsMirrored(
        string[] lines,
        int startIndex)
    {
        var endIndex =
            Math.Min(lines.Length, startIndex + 20);
        for (var index = startIndex;
             index < endIndex;
             index++)
        {
            var value = lines[index].Trim();
            if (value.StartsWith(
                    "[",
                    StringComparison.Ordinal))
            {
                break;
            }

            if (string.Equals(
                    value,
                    "mirror",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryProjectOntoRoadSegment(
        int gridX,
        int gridY,
        double tileSize,
        OmsiSceneryRoadPoint start,
        OmsiSceneryRoadPoint end,
        double targetWorldX,
        double targetWorldZ,
        out double distanceSquared,
        out double snappedLocalX,
        out double snappedLocalZ,
        out double heightY,
        out double headingDegrees)
    {
        distanceSquared = double.PositiveInfinity;
        snappedLocalX = 0d;
        snappedLocalZ = 0d;
        heightY = 0d;
        headingDegrees = 0d;

        var ax = gridX * tileSize + start.TileX;
        var az = gridY * tileSize + start.TileY;
        var bx = gridX * tileSize + end.TileX;
        var bz = gridY * tileSize + end.TileY;
        var vx = bx - ax;
        var vz = bz - az;
        var lengthSquared = vx * vx + vz * vz;
        if (!double.IsFinite(lengthSquared) ||
            lengthSquared < 0.0001d)
        {
            return false;
        }

        var t = Math.Clamp(
            ((targetWorldX - ax) * vx +
             (targetWorldZ - az) * vz) /
            lengthSquared,
            0d,
            1d);
        var worldX = ax + vx * t;
        var worldZ = az + vz * t;
        var dx = worldX - targetWorldX;
        var dz = worldZ - targetWorldZ;

        distanceSquared = dx * dx + dz * dz;
        snappedLocalX = worldX - gridX * tileSize;
        snappedLocalZ = worldZ - gridY * tileSize;
        heightY = start.Z + (end.Z - start.Z) * t;
        headingDegrees = NormalizeHeading(
            Math.Atan2(vx, vz) *
            180d /
            Math.PI);

        return
            double.IsFinite(distanceSquared) &&
            double.IsFinite(snappedLocalX) &&
            double.IsFinite(snappedLocalZ) &&
            double.IsFinite(heightY) &&
            double.IsFinite(headingDegrees);
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

    private static double RoadTravelDeltaDegrees(
        double vehicleHeading,
        double pathHeading,
        int direction) =>
        HeadingDeltaDegrees(
            vehicleHeading,
            ResolveTravelHeading(
                vehicleHeading,
                pathHeading,
                direction));

    private static double ResolveTravelHeading(
        double vehicleHeading,
        double pathHeading,
        int direction)
    {
        var forward = NormalizeHeading(pathHeading);
        var reverse = NormalizeHeading(pathHeading + 180d);

        return direction switch
        {
            0 => forward,
            1 => reverse,
            _ => HeadingDeltaDegrees(vehicleHeading, forward) <=
                 HeadingDeltaDegrees(vehicleHeading, reverse)
                ? forward
                : reverse
        };
    }

    private static double HeadingDeltaDegrees(
        double left,
        double right)
    {
        var delta = Math.Abs(
            NormalizeHeading(left) -
            NormalizeHeading(right));
        return delta > 180d
            ? 360d - delta
            : delta;
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
