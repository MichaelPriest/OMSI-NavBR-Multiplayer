using NavBR.Client.Maps;
using NavBR.Shared.Telemetry;

namespace NavBR.Client;

public partial class MainWindow
{
    private readonly NavBRNavigationEtaEstimator _webNavigationEta = new();
    private string? _webNavigationMapKey;
    private string? _webNavigationRouteKey;
    private OmsiMapLayout? _webNavigationLayout;
    private IReadOnlyList<OmsiRouteTracePoint> _webNavigationRoute = Array.Empty<OmsiRouteTracePoint>();
    private IReadOnlyList<OmsiBusStopPoint> _webNavigationBusStops = Array.Empty<OmsiBusStopPoint>();
    private OmsiOrderedRouteStops _webNavigationOrderedStops = new(false, Array.Empty<string>());

    private object BuildWebNavigationState()
    {
        var telemetry = _lastTelemetry;
        var map = GetActiveMapForOperations();
        if (telemetry is null || map is null || !telemetry.IsInGame)
        {
            _webNavigationEta.Reset();
            return BuildUnavailableWebNavigation(telemetry, map);
        }

        EnsureWebNavigationMapData(map);
        EnsureWebNavigationRouteData(map, telemetry);

        var navigation = NavBRNavigationEngine.Evaluate(
            telemetry,
            _webNavigationLayout,
            _webNavigationRoute,
            _webNavigationBusStops);
        var eta = _webNavigationEta.Observe(navigation, DateTimeOffset.UtcNow);
        var tileSize = _webNavigationLayout?.TileSize;

        var routePoints = tileSize is double resolvedTileSize
            ? DecimateRoute(_webNavigationRoute, 1200)
                .Select(point => new
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

        var stopPoints = tileSize is double stopTileSize
            ? _webNavigationBusStops
                .Where(stop => orderedNameSet.Count == 0 ||
                               orderedNameSet.Contains(OmsiOrderedRouteStopReader.Normalize(stop.Name)))
                .Select(stop => new
                {
                    name = stop.Name,
                    x = stop.GridX * stopTileSize + stop.TileX,
                    y = stop.GridY * stopTileSize + stop.TileY,
                    isNext = StopNamesMatch(stop.Name, telemetry.NextStopName)
                })
                .GroupBy(stop => $"{OmsiOrderedRouteStopReader.Normalize(stop.name)}|{Math.Round(stop.x)}|{Math.Round(stop.y)}")
                .Select(group => group.First())
                .Take(500)
                .ToArray()
            : Array.Empty<object>();

        object? vehicle = null;
        if (tileSize is double vehicleTileSize &&
            telemetry.GridX is int gridX &&
            telemetry.GridY is int gridY &&
            telemetry.TileX is double tileX &&
            telemetry.TileY is double tileY)
        {
            vehicle = new
            {
                x = gridX * vehicleTileSize + tileX,
                y = gridY * vehicleTileSize + tileY,
                headingDegrees = telemetry.HeadingDegrees,
                speedKph = telemetry.SpeedKph
            };
        }

        var ordered = GetOrderedRouteStopsForAlpha12();

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
            routePoints,
            stopPoints,
            vehicle,
            stopSequence = new
            {
                routeResolved = ordered.RouteResolved,
                totalStops = ordered.TotalStops,
                nextStopIndex = ordered.NextStopIndex,
                upcomingStops = ordered.UpcomingStops
            }
        };
    }

    private object BuildUnavailableWebNavigation(VehicleTelemetry? telemetry, OmsiMapInfo? map) => new
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
        tileSize = null as double?,
        routePoints = Array.Empty<object>(),
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

    private void EnsureWebNavigationMapData(OmsiMapInfo map)
    {
        var mapKey = $"{map.DirectoryPath}|{map.GlobalConfigPath}|{map.CompatibilityId}";
        if (string.Equals(mapKey, _webNavigationMapKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _webNavigationMapKey = mapKey;
        _webNavigationRouteKey = null;
        _webNavigationLayout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
        _webNavigationBusStops = OmsiBusStopReader.TryRead(map);
        _webNavigationRoute = Array.Empty<OmsiRouteTracePoint>();
        _webNavigationOrderedStops = new OmsiOrderedRouteStops(false, Array.Empty<string>());
        _webNavigationEta.Reset();
    }

    private void EnsureWebNavigationRouteData(OmsiMapInfo map, VehicleTelemetry telemetry)
    {
        var routeKey = $"{map.DirectoryPath}|{telemetry.Line}|{telemetry.Route}|{telemetry.DestinationName}";
        if (string.Equals(routeKey, _webNavigationRouteKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _webNavigationRouteKey = routeKey;
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
            telemetry.Line);
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
