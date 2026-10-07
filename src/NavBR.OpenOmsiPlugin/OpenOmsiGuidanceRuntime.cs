using NavBR.Shared.PluginBridge;
namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiGuidanceState(
    bool Available,
    string? Maneuver,
    double? TurnAngleDegrees,
    double? DistanceToManeuverMeters,
    double? TargetX,
    double? TargetY);

internal static class OpenOmsiGuidanceRuntime
{
    private const double MinimumTurnDegrees = 22d;
    private const double SearchAheadMeters = 800d;
    private const double TurnWindowMeters = 24d;

    public static OpenOmsiGuidanceState Build(
        OpenOmsiRoutePoint[] route,
        double? vehicleX,
        double? vehicleY)
    {
        if (route.Length < 3 ||
            vehicleX is null ||
            vehicleY is null)
        {
            return Empty();
        }

        var nearest = NearestSegment(
            route,
            vehicleX.Value,
            vehicleY.Value);
        if (nearest is null)
        {
            return Empty();
        }

        var cumulative = BuildCumulative(route);
        var segmentLength = SegmentLength(
            route[nearest.Value.Segment],
            route[nearest.Value.Segment + 1]);
        var currentAlong =
            cumulative[nearest.Value.Segment] +
            segmentLength * nearest.Value.T;

        for (var i = nearest.Value.Segment + 1;
             i < route.Length - 1;
             i++)
        {
            var at = cumulative[i];
            var distanceAhead = at - currentAlong;
            if (distanceAhead < 0d)
            {
                continue;
            }

            if (distanceAhead > SearchAheadMeters)
            {
                break;
            }

            var before = PointAtDistance(
                route,
                cumulative,
                Math.Max(0d, at - TurnWindowMeters));
            var after = PointAtDistance(
                route,
                cumulative,
                Math.Min(cumulative[^1], at + TurnWindowMeters));
            var pivot = route[i];

            var incoming = Heading(before, pivot);
            var outgoing = Heading(pivot, after);
            var angle = WrapDegrees(outgoing - incoming);
            if (Math.Abs(angle) >= MinimumTurnDegrees)
            {
                return new(
                    Available: true,
                    Maneuver: Classify(angle),
                    TurnAngleDegrees: Math.Round(angle, 1),
                    DistanceToManeuverMeters: Math.Round(distanceAhead, 1),
                    TargetX: pivot.X,
                    TargetY: pivot.Y);
            }
        }

        return new(
            Available: true,
            Maneuver: "continue",
            TurnAngleDegrees: 0d,
            DistanceToManeuverMeters: null,
            TargetX: route[^1].X,
            TargetY: route[^1].Y);
    }

    private readonly record struct Projection(int Segment, double T, double Distance);

    private static Projection? NearestSegment(
        OpenOmsiRoutePoint[] route,
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
            if (best is null || distance < best.Value.Distance)
            {
                best = new(i, t, distance);
            }
        }

        return best;
    }

    private static double[] BuildCumulative(OpenOmsiRoutePoint[] route)
    {
        var cumulative = new double[route.Length];
        for (var i = 1; i < route.Length; i++)
        {
            cumulative[i] =
                cumulative[i - 1] +
                SegmentLength(route[i - 1], route[i]);
        }

        return cumulative;
    }

    private static OpenOmsiRoutePoint PointAtDistance(
        OpenOmsiRoutePoint[] route,
        double[] cumulative,
        double distance)
    {
        if (distance <= 0d)
        {
            return route[0];
        }

        if (distance >= cumulative[^1])
        {
            return route[^1];
        }

        var index = Array.BinarySearch(cumulative, distance);
        if (index >= 0)
        {
            return route[index];
        }

        index = ~index;
        var aIndex = Math.Max(0, index - 1);
        var bIndex = Math.Min(route.Length - 1, index);
        var span = cumulative[bIndex] - cumulative[aIndex];
        var t = span <= 1e-9d
            ? 0d
            : Math.Clamp(
                (distance - cumulative[aIndex]) / span,
                0d,
                1d);

        var a = route[aIndex];
        var b = route[bIndex];
        return new(
            X: a.X + (b.X - a.X) * t,
            Y: a.Y + (b.Y - a.Y) * t,
            Z: a.Z is not null && b.Z is not null
                ? a.Z.Value + (b.Z.Value - a.Z.Value) * t
                : a.Z ?? b.Z,
            StopName: null);
    }

    private static string Classify(double angle)
    {
        var magnitude = Math.Abs(angle);
        if (magnitude >= 150d)
        {
            return "uturn";
        }

        if (angle > 0d)
        {
            return magnitude >= 65d ? "right" : "slight-right";
        }

        return magnitude >= 65d ? "left" : "slight-left";
    }

    private static double Heading(
        OpenOmsiRoutePoint a,
        OpenOmsiRoutePoint b) =>
        Math.Atan2(
            b.X - a.X,
            b.Y - a.Y) *
        180d / Math.PI;

    private static double SegmentLength(
        OpenOmsiRoutePoint a,
        OpenOmsiRoutePoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double WrapDegrees(double value)
    {
        var wrapped = (value + 180d) % 360d;
        if (wrapped < 0d)
        {
            wrapped += 360d;
        }

        return wrapped - 180d;
    }

    private static OpenOmsiGuidanceState Empty() =>
        new(false, null, null, null, null, null);
}
