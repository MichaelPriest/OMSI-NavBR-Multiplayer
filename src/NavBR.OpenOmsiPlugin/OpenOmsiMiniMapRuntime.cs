using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiMiniMapRuntimeState(
    bool Available,
    double RouteLengthMeters,
    double ProgressMeters,
    double RemainingMeters,
    double ProgressPercent,
    OpenOmsiGuidanceWaypointState[] GuidanceWaypoints);

internal static class OpenOmsiMiniMapRuntime
{
    private const double WaypointSpacingMeters = 18d;
    private const double WaypointLookAheadMeters = 260d;
    private const int MaxWaypoints = 24;

    public static OpenOmsiMiniMapRuntimeState Build(
        OpenOmsiRoutePoint[] route,
        double? vehicleX,
        double? vehicleY)
    {
        if (route.Length < 2 ||
            vehicleX is null ||
            vehicleY is null)
        {
            return Empty();
        }

        var cumulative = BuildCumulative(route);
        var total = cumulative[^1];
        var projection = Project(route, cumulative, vehicleX.Value, vehicleY.Value);
        if (projection is null)
        {
            return Empty();
        }

        var progress = Math.Clamp(projection.Value.DistanceAlongRoute, 0d, total);
        var remaining = Math.Max(0d, total - progress);
        var percent = total <= 1e-6d
            ? 100d
            : Math.Clamp(progress / total * 100d, 0d, 100d);

        var waypoints = new List<OpenOmsiGuidanceWaypointState>();
        var target = progress + WaypointSpacingMeters;
        var maxTarget = Math.Min(total, progress + WaypointLookAheadMeters);

        while (target <= maxTarget + 1e-6d &&
               waypoints.Count < MaxWaypoints)
        {
            var point = PointAtDistance(route, cumulative, target);
            if (point is null)
            {
                break;
            }

            waypoints.Add(new(
                X: point.Value.X,
                Y: point.Value.Y,
                Z: point.Value.Z,
                DistanceAheadMeters: Math.Round(target - progress, 1),
                HeadingDegrees: Math.Round(point.Value.Heading, 1)));

            target += WaypointSpacingMeters;
        }

        return new(
            Available: true,
            RouteLengthMeters: Math.Round(total, 1),
            ProgressMeters: Math.Round(progress, 1),
            RemainingMeters: Math.Round(remaining, 1),
            ProgressPercent: Math.Round(percent, 1),
            GuidanceWaypoints: [.. waypoints]);
    }

    private readonly record struct Projection(double DistanceAlongRoute, double Distance);
    private readonly record struct Sample(double X, double Y, double? Z, double Heading);

    private static double[] BuildCumulative(OpenOmsiRoutePoint[] route)
    {
        var cumulative = new double[route.Length];
        for (var i = 1; i < route.Length; i++)
        {
            cumulative[i] = cumulative[i - 1] + SegmentLength(route[i - 1], route[i]);
        }

        return cumulative;
    }

    private static Projection? Project(
        OpenOmsiRoutePoint[] route,
        double[] cumulative,
        double x,
        double y)
    {
        Projection? best = null;
        for (var i = 0; i < route.Length - 1; i++)
        {
            var a = route[i];
            var b = route[i + 1];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len2 = dx * dx + dy * dy;
            var t = len2 <= 1e-9d
                ? 0d
                : Math.Clamp(
                    ((x - a.X) * dx + (y - a.Y) * dy) / len2,
                    0d,
                    1d);

            var px = a.X + dx * t;
            var py = a.Y + dy * t;
            var ddx = x - px;
            var ddy = y - py;
            var distance = Math.Sqrt(ddx * ddx + ddy * ddy);
            var along = cumulative[i] + Math.Sqrt(len2) * t;
            var candidate = new Projection(along, distance);
            if (best is null || candidate.Distance < best.Value.Distance)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static Sample? PointAtDistance(
        OpenOmsiRoutePoint[] route,
        double[] cumulative,
        double distance)
    {
        if (route.Length < 2)
        {
            return null;
        }

        if (distance <= 0d)
        {
            return FromSegment(route[0], route[1], 0d);
        }

        for (var i = 0; i < route.Length - 1; i++)
        {
            var start = cumulative[i];
            var end = cumulative[i + 1];
            if (distance > end && i < route.Length - 2)
            {
                continue;
            }

            var length = Math.Max(end - start, 1e-9d);
            var t = Math.Clamp((distance - start) / length, 0d, 1d);
            return FromSegment(route[i], route[i + 1], t);
        }

        return FromSegment(route[^2], route[^1], 1d);
    }

    private static Sample FromSegment(
        OpenOmsiRoutePoint a,
        OpenOmsiRoutePoint b,
        double t)
    {
        var z = a.Z is not null && b.Z is not null
            ? a.Z.Value + (b.Z.Value - a.Z.Value) * t
            : a.Z ?? b.Z;
        var heading = Math.Atan2(b.X - a.X, b.Y - a.Y) * 180d / Math.PI;
        return new(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            z,
            NormalizeHeading(heading));
    }

    private static double SegmentLength(OpenOmsiRoutePoint a, OpenOmsiRoutePoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double NormalizeHeading(double heading)
    {
        var normalized = heading % 360d;
        return normalized < 0d ? normalized + 360d : normalized;
    }

    private static OpenOmsiMiniMapRuntimeState Empty() =>
        new(false, 0d, 0d, 0d, 0d, []);
}
