using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiRouteRuntimeState(
    bool RouteLoaded,
    string? RouteKey,
    int RoutePointCount,
    double? DistanceFromRouteMeters,
    bool OffRoute,
    int? NearestRoutePointIndex,
    int? RejoinRoutePointIndex,
    double? RejoinTargetX,
    double? RejoinTargetY);

internal static class OpenOmsiRouteRuntime
{
    private const double OffRouteThresholdMeters = 45d;
    private const int RejoinLookAheadPoints = 3;
    private static readonly object Sync = new();
    private static OpenOmsiRoutePoint[] _route = [];
    private static string? _routeKey;

    public static void SetRoute(
        OpenOmsiRoutePoint[]? route,
        string? routeKey = null)
    {
        lock (Sync)
        {
            _route = route?
                .Where(point =>
                    double.IsFinite(point.X) &&
                    double.IsFinite(point.Y))
                .ToArray()
                ?? [];
            _routeKey = _route.Length == 0
                ? null
                : NormalizeRouteKey(routeKey);
        }
    }

    public static void SetRoute(PluginBridgeMessage message) =>
        SetRoute(
            message.OpenOmsiRoutePoints,
            BuildRouteKey(
                message.MapName,
                message.Line,
                message.Route,
                message.OpenOmsiTripIndex));

    public static void Clear()
    {
        lock (Sync)
        {
            _route = [];
            _routeKey = null;
        }
    }

    public static OpenOmsiRouteRuntimeState Build(OpenOmsiLuaSnapshot? snapshot)
    {
        OpenOmsiRoutePoint[] route;
        string? routeKey;
        lock (Sync)
        {
            route = _route;
            routeKey = _routeKey;
        }

        if (route.Length == 0)
        {
            return Empty();
        }

        var liveKey = BuildRouteKey(snapshot);
        if (routeKey is not null &&
            liveKey is not null &&
            !string.Equals(routeKey, liveKey, StringComparison.Ordinal))
        {
            Clear();
            return Empty();
        }

        if (snapshot?.HasPosition != true ||
            snapshot.X is null ||
            snapshot.Y is null)
        {
            return new(true, routeKey, route.Length, null, false, null, null, null, null);
        }

        var nearestIndex = 0;
        var nearestDistance = double.MaxValue;
        for (var i = 0; i < route.Length; i++)
        {
            var distance = Distance2D(
                snapshot.X.Value,
                snapshot.Y.Value,
                route[i].X,
                route[i].Y);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        var offRoute = nearestDistance > OffRouteThresholdMeters;
        var rejoinIndex = offRoute
            ? Math.Min(nearestIndex + RejoinLookAheadPoints, route.Length - 1)
            : nearestIndex;
        var target = route[rejoinIndex];

        return new(
            RouteLoaded: true,
            RouteKey: routeKey,
            RoutePointCount: route.Length,
            DistanceFromRouteMeters: Math.Round(nearestDistance, 1),
            OffRoute: offRoute,
            NearestRoutePointIndex: nearestIndex,
            RejoinRoutePointIndex: rejoinIndex,
            RejoinTargetX: target.X,
            RejoinTargetY: target.Y);
    }

    internal static string? BuildRouteKey(OpenOmsiLuaSnapshot? snapshot) =>
        snapshot is null
            ? null
            : BuildRouteKey(
                snapshot.MapName,
                snapshot.Line,
                snapshot.Tour,
                snapshot.TripIndex);

    internal static string? BuildRouteKey(
        string? map,
        string? line,
        string? tour,
        int? tripIndex)
    {
        if (string.IsNullOrWhiteSpace(map) ||
            string.IsNullOrWhiteSpace(line) ||
            string.IsNullOrWhiteSpace(tour) ||
            tripIndex is null)
        {
            return null;
        }

        return string.Join(
            "|",
            NormalizePart(map),
            NormalizePart(line),
            NormalizePart(tour),
            tripIndex.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static OpenOmsiRouteRuntimeState Empty() =>
        new(false, null, 0, null, false, null, null, null, null);

    private static string? NormalizeRouteKey(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();

    private static string NormalizePart(string value) =>
        value.Trim().Replace('\\', '/').ToLowerInvariant();

    private static double Distance2D(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
