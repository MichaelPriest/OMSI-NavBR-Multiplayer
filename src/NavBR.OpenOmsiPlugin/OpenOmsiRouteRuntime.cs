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
    private const double RejoinLookAheadMeters = 60d;
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

        var projection = ProjectToRoute(
            route,
            snapshot.X.Value,
            snapshot.Y.Value);
        if (projection is null)
        {
            return new(true, routeKey, route.Length, null, false, null, null, null, null);
        }

        var offRoute = projection.Value.Distance > OffRouteThresholdMeters;
        var target = offRoute
            ? AdvanceAlongRoute(
                route,
                projection.Value.SegmentIndex,
                projection.Value.T,
                RejoinLookAheadMeters)
            : new RejoinTarget(
                projection.Value.NearestPointIndex,
                projection.Value.X,
                projection.Value.Y);

        return new(
            RouteLoaded: true,
            RouteKey: routeKey,
            RoutePointCount: route.Length,
            DistanceFromRouteMeters: Math.Round(projection.Value.Distance, 1),
            OffRoute: offRoute,
            NearestRoutePointIndex: projection.Value.NearestPointIndex,
            RejoinRoutePointIndex: target.Index,
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

    private readonly record struct RouteProjection(
        int SegmentIndex,
        double T,
        double X,
        double Y,
        double Distance,
        int NearestPointIndex);

    private readonly record struct RejoinTarget(
        int Index,
        double X,
        double Y);

    private static RouteProjection? ProjectToRoute(
        OpenOmsiRoutePoint[] route,
        double x,
        double y)
    {
        if (route.Length == 1)
        {
            return new(
                0,
                0d,
                route[0].X,
                route[0].Y,
                Distance2D(x, y, route[0].X, route[0].Y),
                0);
        }

        RouteProjection? best = null;
        for (var i = 0; i < route.Length - 1; i++)
        {
            var a = route[i];
            var b = route[i + 1];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var lengthSquared = dx * dx + dy * dy;
            var t = lengthSquared <= 1e-9d
                ? 0d
                : Math.Clamp(
                    ((x - a.X) * dx + (y - a.Y) * dy) /
                    lengthSquared,
                    0d,
                    1d);

            var px = a.X + dx * t;
            var py = a.Y + dy * t;
            var distance = Distance2D(x, y, px, py);
            var nearestPoint = t < 0.5d ? i : i + 1;
            var candidate = new RouteProjection(
                i,
                t,
                px,
                py,
                distance,
                nearestPoint);

            if (best is null ||
                candidate.Distance < best.Value.Distance)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static RejoinTarget AdvanceAlongRoute(
        OpenOmsiRoutePoint[] route,
        int segmentIndex,
        double segmentT,
        double distanceMeters)
    {
        if (route.Length == 1)
        {
            return new(0, route[0].X, route[0].Y);
        }

        var segment = Math.Clamp(segmentIndex, 0, route.Length - 2);
        var a = route[segment];
        var b = route[segment + 1];
        var segmentLength = Distance2D(a.X, a.Y, b.X, b.Y);
        var remainingOnSegment =
            Math.Max(0d, segmentLength * (1d - Math.Clamp(segmentT, 0d, 1d)));

        if (distanceMeters <= remainingOnSegment &&
            segmentLength > 1e-9d)
        {
            var t = segmentT + distanceMeters / segmentLength;
            return new(
                segment + 1,
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t);
        }

        var remaining = Math.Max(0d, distanceMeters - remainingOnSegment);
        for (var i = segment + 1; i < route.Length - 1; i++)
        {
            a = route[i];
            b = route[i + 1];
            segmentLength = Distance2D(a.X, a.Y, b.X, b.Y);
            if (segmentLength <= 1e-9d)
            {
                continue;
            }

            if (remaining <= segmentLength)
            {
                var t = remaining / segmentLength;
                return new(
                    i + 1,
                    a.X + (b.X - a.X) * t,
                    a.Y + (b.Y - a.Y) * t);
            }

            remaining -= segmentLength;
        }

        var last = route[^1];
        return new(route.Length - 1, last.X, last.Y);
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
