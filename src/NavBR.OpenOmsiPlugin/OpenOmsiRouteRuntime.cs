using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiRouteRuntimeState(
    bool RouteLoaded,
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

    public static void SetRoute(OpenOmsiRoutePoint[]? route)
    {
        lock (Sync)
        {
            _route = route?
                .Where(point =>
                    double.IsFinite(point.X) &&
                    double.IsFinite(point.Y))
                .ToArray()
                ?? [];
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            _route = [];
        }
    }

    public static OpenOmsiRouteRuntimeState Build(OpenOmsiLuaSnapshot? snapshot)
    {
        OpenOmsiRoutePoint[] route;
        lock (Sync)
        {
            route = _route;
        }

        if (route.Length == 0)
        {
            return new(false, 0, null, false, null, null, null, null);
        }

        if (snapshot?.HasPosition != true ||
            snapshot.X is null ||
            snapshot.Y is null)
        {
            return new(true, route.Length, null, false, null, null, null, null);
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
            RoutePointCount: route.Length,
            DistanceFromRouteMeters: Math.Round(nearestDistance, 1),
            OffRoute: offRoute,
            NearestRoutePointIndex: nearestIndex,
            RejoinRoutePointIndex: rejoinIndex,
            RejoinTargetX: target.X,
            RejoinTargetY: target.Y);
    }

    private static double Distance2D(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
