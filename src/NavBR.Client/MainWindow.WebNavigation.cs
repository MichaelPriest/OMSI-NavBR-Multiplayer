using NavBR.Client.Maps;
using NavBR.Shared.Telemetry;

namespace NavBR.Client;

public partial class MainWindow
{
    private readonly NavBRNavigationEtaEstimator _webNavigationEta = new();
    private readonly OmsiRouteRejoinPathfinder _webNavigationRejoinPathfinder = new();
    private OmsiPhysicalRoadAnchorResolver? _webNavigationRoadAnchorResolver;
    private string? _webNavigationRoadAnchorInstallRoot;
    private string? _webNavigationMapKey;
    private string? _webNavigationRouteKey;
    private OmsiMapLayout? _webNavigationLayout;
    private IReadOnlyList<OmsiRouteTracePoint> _webNavigationRoute = Array.Empty<OmsiRouteTracePoint>();
    private IReadOnlyList<OmsiBusStopPoint> _webNavigationBusStops = Array.Empty<OmsiBusStopPoint>();
    private OmsiOrderedRouteStops _webNavigationOrderedStops = new(false, Array.Empty<string>());
    private DateTimeOffset _webNavigationRouteLastAttemptUtc;

    private object BuildWebNavigationState()
    {
        var telemetry = _lastTelemetry;
        var map = GetActiveMapForOperations();

        if (map is not null)
        {
            EnsureWebNavigationMapData(map);
        }

        if (telemetry is null || map is null || !telemetry.IsInGame)
        {
            _webNavigationEta.Reset();
            return BuildUnavailableWebNavigation(telemetry, map);
        }
        EnsureWebNavigationRouteData(map, telemetry);

        var navigationTelemetry = telemetry;
        var hasDisplayAnchor = TryGetWebNavigationDisplayAnchor(
            telemetry,
            map,
            out var displayGridX,
            out var displayGridY,
            out var displayTileX,
            out var displayTileY);
        if (hasDisplayAnchor)
        {
            navigationTelemetry = telemetry with
            {
                GridX = displayGridX,
                GridY = displayGridY,
                TileX = displayTileX,
                TileY = displayTileY
            };
        }

        var navigation = NavBRNavigationEngine.Evaluate(
            navigationTelemetry,
            _webNavigationLayout,
            _webNavigationRoute,
            _webNavigationBusStops);
        var eta = _webNavigationEta.Observe(navigation, DateTimeOffset.UtcNow);
        var tileSize = _webNavigationLayout?.TileSize;
        var roadmapUrl = ResolveWebNavigationRoadmapUrl(map);
        var roadmapFallbackUrl = ResolveWebNavigationRoadmapFallbackUrl(map);
        var roadmapAvailable =
            !string.IsNullOrWhiteSpace(roadmapUrl) ||
            !string.IsNullOrWhiteSpace(roadmapFallbackUrl);
        object? mapBounds = null;
        if (tileSize is double boundsTileSize &&
            _webNavigationLayout?.WorldWidth is double worldWidth &&
            _webNavigationLayout.WorldHeight is double worldHeight)
        {
            var minWorldX = _webNavigationLayout.MinGridX * boundsTileSize;
            var minWorldY = _webNavigationLayout.MinGridY * boundsTileSize;
            mapBounds = new
            {
                minX = minWorldX,
                minY = minWorldY,
                maxX = minWorldX + worldWidth,
                maxY = minWorldY + worldHeight
            };
        }

        OmsiRouteRejoinPath? rejoinPath = null;
        if (!navigation.IsOnRoute &&
            navigation.RouteAvailable &&
            _webNavigationLayout is not null &&
            _webNavigationRoute.Count >= 2)
        {
            rejoinPath = _webNavigationRejoinPathfinder.TryFind(
                map,
                _webNavigationLayout,
                navigationTelemetry,
                _webNavigationRoute);
        }

        IReadOnlyList<object> rejoinPoints = rejoinPath is null
            ? Array.Empty<object>()
            : rejoinPath.Points
                .Select(point => (object)new
                {
                    x = point.X,
                    y = point.Y
                })
                .ToArray();

        IReadOnlyList<object> routePoints = tileSize is double resolvedTileSize
            ? DecimateRoute(_webNavigationRoute, 1200)
                .Select(point => (object)new
                {
                    x = point.GridX * resolvedTileSize + point.TileX,
                    y = point.GridY * resolvedTileSize + point.TileY
                })
                .ToArray()
            : Array.Empty<object>();

        var orderedStopNames = _webNavigationOrderedStops.RouteResolved
            ? _webNavigationOrderedStops.StopNames
            : Array.Empty<string>();
        var orderedNameSet = orderedStopNames
            .Select(OmsiOrderedRouteStopReader.Normalize)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlyList<object> stopPoints = tileSize is double stopTileSize && orderedNameSet.Count > 0
            ? _webNavigationBusStops
                .Where(stop => orderedNameSet.Contains(OmsiOrderedRouteStopReader.Normalize(stop.Name)))
                .Select(stop => new
                {
                    name = stop.Name,
                    x = stop.GridX * stopTileSize + stop.TileX,
                    y = stop.GridY * stopTileSize + stop.TileY,
                    isNext = StopNamesMatch(stop.Name, telemetry.NextStopName)
                })
                .GroupBy(stop => $"{OmsiOrderedRouteStopReader.Normalize(stop.name)}|{Math.Round(stop.x)}|{Math.Round(stop.y)}")
                .Select(group => (object)group.First())
                .Take(500)
                .ToArray()
            : Array.Empty<object>();

        object? vehicle = null;
        if (tileSize is double vehicleTileSize &&
            navigationTelemetry.GridX is int gridX &&
            navigationTelemetry.GridY is int gridY &&
            navigationTelemetry.TileX is double tileX &&
            navigationTelemetry.TileY is double tileY)
        {
            vehicle = new
            {
                x = gridX * vehicleTileSize + tileX,
                y = gridY * vehicleTileSize + tileY,
                headingDegrees = navigationTelemetry.HeadingDegrees,
                speedKph = navigationTelemetry.SpeedKph,
                snappedToRoad = hasDisplayAnchor &&
                    (gridX != telemetry.GridX ||
                     gridY != telemetry.GridY ||
                     tileX != telemetry.TileX ||
                     tileY != telemetry.TileY)
            };
        }

        var routeDiagnostics = OmsiRouteTraceReader.LastDiagnostics;
        object? routeDiagnostic =
            routeDiagnostics is not null &&
            string.Equals(
                routeDiagnostics.MapFolder,
                map.FolderName,
                StringComparison.OrdinalIgnoreCase)
                ? new
                {
                    mode = routeDiagnostics.Mode,
                    trackName = routeDiagnostics.TrackName,
                    line = routeDiagnostics.ActiveLine,
                    lookupValue = routeDiagnostics.LookupValue,
                    entryCount = routeDiagnostics.EntryCount,
                    pointCount = routeDiagnostics.PointCount
                }
                : null;

        var nextStopIndex = _webNavigationOrderedStops.RouteResolved
            ? ResolveNextStopIndex(
                _webNavigationOrderedStops.StopNames,
                telemetry.NextStopName,
                telemetry.CurrentStopIndex)
            : null;
        var upcomingStops = nextStopIndex is int resolvedStopIndex
            ? _webNavigationOrderedStops.StopNames
                .Skip(resolvedStopIndex)
                .Take(6)
                .ToArray()
            : Array.Empty<string>();

        return new
        {
            available = navigation.RouteAvailable,
            mapName = map.DisplayName,
            mapFolder = map.FolderName,
            line = telemetry.Line,
            route = telemetry.Route,
            destinationName = navigation.DestinationName,
            nextStopName = navigation.NextStopName,
            currentStreetName = navigation.CurrentStreetName,
            currentStopIndex = navigation.CurrentStopIndex,
            isOnRoute = navigation.IsOnRoute,
            offRouteDistanceMeters = navigation.OffRouteDistanceMeters,
            routeProgressPercent = navigation.RouteProgressPercent,
            distanceRemainingMeters = navigation.DistanceRemainingMeters,
            distanceToNextStopMeters = navigation.DistanceToNextStopMeters,
            maneuver = navigation.Maneuver.ToString(),
            distanceToManeuverMeters = navigation.DistanceToManeuverMeters,
            etaToNextStopSeconds = eta.ToNextStop?.TotalSeconds,
            etaToRouteEndSeconds = eta.ToRouteEnd?.TotalSeconds,
            paceMetersPerSecond = eta.PaceMetersPerSecond,
            usesWorldCoordinates = _webNavigationLayout?.UsesWorldCoordinates ?? false,
            tileSize = _webNavigationLayout?.TileSize,
            roadmapAvailable,
            roadmapUrl,
            roadmapFallbackUrl,
            bounds = mapBounds,
            routePoints,
            routeDiagnostic,
            rejoinAvailable = rejoinPath is not null,
            rejoinDistanceMeters = rejoinPath?.DistanceMeters,
            rejoinPoints,
            rejoinPoint = rejoinPath is null
                ? null
                : new
                {
                    x = rejoinPath.RejoinPoint.X,
                    y = rejoinPath.RejoinPoint.Y
                },
            stopPoints,
            vehicle,
            stopSequence = new
            {
                routeResolved = _webNavigationOrderedStops.RouteResolved,
                totalStops = _webNavigationOrderedStops.StopNames.Count,
                nextStopIndex,
                upcomingStops
            }
        };
    }

    private object BuildUnavailableWebNavigation(VehicleTelemetry? telemetry, OmsiMapInfo? map)
    {
        var roadmapPath = map is null
            ? null
            : ResolveWebNavigationRoadmapPath(map);
        var roadmapUrl = map is null
            ? null
            : ResolveWebNavigationRoadmapUrl(map);
        var roadmapFallbackUrl = map is null
            ? null
            : ResolveWebNavigationRoadmapFallbackUrl(map);
        var roadmapAvailable =
            !string.IsNullOrWhiteSpace(roadmapUrl) ||
            !string.IsNullOrWhiteSpace(roadmapFallbackUrl);

        object? mapBounds = null;
        if (_webNavigationLayout?.TileSize is double tileSize &&
            _webNavigationLayout.WorldWidth is double worldWidth &&
            _webNavigationLayout.WorldHeight is double worldHeight)
        {
            var minWorldX = _webNavigationLayout.MinGridX * tileSize;
            var minWorldY = _webNavigationLayout.MinGridY * tileSize;
            mapBounds = new
            {
                minX = minWorldX,
                minY = minWorldY,
                maxX = minWorldX + worldWidth,
                maxY = minWorldY + worldHeight
            };
        }

        return new
        {
        available = false,
        mapName = map?.DisplayName ?? telemetry?.MapName,
        mapFolder = map?.FolderName,
        line = telemetry?.Line,
        route = telemetry?.Route,
        destinationName = telemetry?.DestinationName,
        nextStopName = telemetry?.NextStopName,
        currentStreetName = telemetry?.CurrentStreetName,
        currentStopIndex = telemetry?.CurrentStopIndex,
        isOnRoute = false,
        offRouteDistanceMeters = 0d,
        routeProgressPercent = 0d,
        distanceRemainingMeters = 0d,
        distanceToNextStopMeters = null as double?,
        maneuver = NavBRManeuverKind.None.ToString(),
        distanceToManeuverMeters = null as double?,
        etaToNextStopSeconds = null as double?,
        etaToRouteEndSeconds = null as double?,
        paceMetersPerSecond = null as double?,
        usesWorldCoordinates = false,
        tileSize = _webNavigationLayout?.TileSize,
        roadmapAvailable,
        roadmapUrl,
        roadmapFallbackUrl,
        bounds = mapBounds,
        routePoints = Array.Empty<object>(),
        routeDiagnostic = null as object,
        rejoinAvailable = false,
        rejoinDistanceMeters = null as double?,
        rejoinPoints = Array.Empty<object>(),
        rejoinPoint = null as object,
        stopPoints = Array.Empty<object>(),
        vehicle = null as object,
        stopSequence = new
        {
            routeResolved = false,
            totalStops = 0,
            nextStopIndex = null as int?,
            upcomingStops = Array.Empty<string>()
        }
        };
    }

    private bool TryGetWebNavigationDisplayAnchor(
        VehicleTelemetry telemetry,
        OmsiMapInfo map,
        out int gridX,
        out int gridY,
        out double tileX,
        out double tileY)
    {
        gridX = 0;
        gridY = 0;
        tileX = 0d;
        tileY = 0d;

        var hasGridTelemetry = false;
        if (telemetry.PhysicalGridX is int physicalGridX &&
            telemetry.PhysicalGridY is int physicalGridY &&
            telemetry.LocalX is double physicalLocalX &&
            telemetry.LocalZ is double physicalLocalZ)
        {
            hasGridTelemetry = true;
            gridX = physicalGridX;
            gridY = physicalGridY;
            tileX = physicalLocalX;
            tileY = physicalLocalZ;
        }
        else if (telemetry.GridX is int rawGridX &&
                 telemetry.GridY is int rawGridY &&
                 telemetry.TileX is double rawTileX &&
                 telemetry.TileY is double rawTileY)
        {
            hasGridTelemetry = true;
            gridX = rawGridX;
            gridY = rawGridY;
            tileX = rawTileX;
            tileY = rawTileY;
        }

        var installRoot = ResolveWebNavigationOmsiInstallRoot(map.DirectoryPath);
        if (string.IsNullOrWhiteSpace(installRoot))
        {
            return hasGridTelemetry;
        }

        if (_webNavigationRoadAnchorResolver is null ||
            !string.Equals(
                _webNavigationRoadAnchorInstallRoot,
                installRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            _webNavigationRoadAnchorInstallRoot = installRoot;
            var capturedRoot = installRoot;
            _webNavigationRoadAnchorResolver =
                new OmsiPhysicalRoadAnchorResolver(() => capturedRoot);
        }

        var anchorTelemetry = telemetry;
        if (!hasGridTelemetry)
        {
            if (!_webNavigationRoadAnchorResolver.TryResolveOpenOmsiWorldAnchor(
                    telemetry,
                    out var worldAnchor))
            {
                return false;
            }

            gridX = worldAnchor.GridX;
            gridY = worldAnchor.GridY;
            tileX = worldAnchor.LocalX;
            tileY = worldAnchor.LocalZ;

            anchorTelemetry = telemetry with
            {
                GridX = gridX,
                GridY = gridY,
                TileX = tileX,
                TileY = tileY,
                PhysicalGridX = gridX,
                PhysicalGridY = gridY,
                LocalX = worldAnchor.LocalX,
                LocalY = worldAnchor.LocalY,
                LocalZ = worldAnchor.LocalZ
            };
        }

        if (_webNavigationRoadAnchorResolver.TryResolveRoadAnchor(
                anchorTelemetry,
                out var roadAnchor) &&
            roadAnchor.DistanceMeters <= 18d)
        {
            gridX = roadAnchor.GridX;
            gridY = roadAnchor.GridY;
            tileX = roadAnchor.LocalX;
            tileY = roadAnchor.LocalZ;
        }

        return true;
    }

    private static string? ResolveWebNavigationOmsiInstallRoot(string? mapDirectory)
    {
        if (string.IsNullOrWhiteSpace(mapDirectory))
        {
            return null;
        }

        try
        {
            var map = new System.IO.DirectoryInfo(mapDirectory);
            var maps = map.Parent;
            if (maps is not null &&
                string.Equals(
                    maps.Name,
                    "maps",
                    StringComparison.OrdinalIgnoreCase))
            {
                return maps.Parent?.FullName;
            }
        }
        catch
        {
        }

        return null;
    }

    private void EnsureWebNavigationMapData(OmsiMapInfo map)
    {
        var mapKey = $"{map.DirectoryPath}|{map.GlobalConfigPath}|{map.CompatibilityId}";
        if (string.Equals(mapKey, _webNavigationMapKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _webNavigationMapKey = mapKey;
        _webNavigationRouteKey = null;
        _webNavigationRouteLastAttemptUtc = default;
        _webNavigationLayout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
        _webNavigationBusStops = OmsiBusStopReader.TryRead(map);
        _webNavigationRoute = Array.Empty<OmsiRouteTracePoint>();
        _webNavigationOrderedStops = new OmsiOrderedRouteStops(false, Array.Empty<string>());
        _webNavigationEta.Reset();
    }

    private void EnsureWebNavigationRouteData(OmsiMapInfo map, VehicleTelemetry telemetry)
    {
        var routeKey = $"{map.DirectoryPath}|{telemetry.Line}|{telemetry.Route}|{telemetry.DestinationName}";
        var sameKey = string.Equals(
            routeKey,
            _webNavigationRouteKey,
            StringComparison.OrdinalIgnoreCase);

        if (sameKey && _webNavigationRoute.Count >= 2)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (sameKey &&
            now - _webNavigationRouteLastAttemptUtc < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _webNavigationRouteKey = routeKey;
        _webNavigationRouteLastAttemptUtc = now;
        _webNavigationEta.Reset();

        if (_webNavigationLayout is null)
        {
            _webNavigationRoute = Array.Empty<OmsiRouteTracePoint>();
            _webNavigationOrderedStops = new OmsiOrderedRouteStops(false, Array.Empty<string>());
            return;
        }

        var lookupTarget = !string.IsNullOrWhiteSpace(telemetry.Route)
            ? telemetry.Route
            : telemetry.DestinationName;
        _webNavigationRoute = OmsiRouteTraceReader.TryRead(
            map,
            _webNavigationLayout,
            lookupTarget,
            telemetry.Line,
            telemetry.DestinationName);

        // Some buses/maps expose a short route/course code (for example "01")
        // instead of the .ttr track name. If that first lookup cannot resolve
        // real route geometry, retry with the active destination. This still
        // goes through TTData/.ttp/.ttr resolution and never fabricates a route.
        if (_webNavigationRoute.Count < 2 &&
            !string.IsNullOrWhiteSpace(telemetry.DestinationName) &&
            !string.Equals(
                telemetry.DestinationName?.Trim(),
                lookupTarget?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            var destinationRoute = OmsiRouteTraceReader.TryRead(
                map,
                _webNavigationLayout,
                telemetry.DestinationName,
                telemetry.Line,
                telemetry.DestinationName);
            if (destinationRoute.Count >= 2)
            {
                _webNavigationRoute = destinationRoute;
            }
        }

        _webNavigationOrderedStops = OmsiOrderedRouteStopReader.TryRead(
            map,
            telemetry.Route,
            telemetry.Line,
            telemetry.DestinationName);
    }

    private static IReadOnlyList<OmsiRouteTracePoint> DecimateRoute(
        IReadOnlyList<OmsiRouteTracePoint> points,
        int maxPoints)
    {
        if (points.Count <= maxPoints)
        {
            return points;
        }

        var result = new List<OmsiRouteTracePoint>(maxPoints);
        var step = (points.Count - 1d) / (maxPoints - 1d);
        for (var index = 0; index < maxPoints; index++)
        {
            result.Add(points[(int)Math.Round(index * step)]);
        }

        return result;
    }

    private static bool StopNamesMatch(string? a, string? b)
    {
        var left = OmsiOrderedRouteStopReader.Normalize(a);
        var right = OmsiOrderedRouteStopReader.Normalize(b);
        return left.Length > 0 &&
               right.Length > 0 &&
               (left == right || left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal));
    }
}
