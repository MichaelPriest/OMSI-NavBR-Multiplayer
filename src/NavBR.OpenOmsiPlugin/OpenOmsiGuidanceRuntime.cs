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

        var accumulated = Math.Max(
            0d,
            SegmentLength(
                route[nearest.Value.Segment],
                route[nearest.Value.Segment + 1]) *
            (1d - nearest.Value.T));

        for (var i = nearest.Value.Segment + 1;
             i < route.Length - 1 && accumulated <= SearchAheadMeters;
             i++)
        {
            var a = route[i - 1];
            var b = route[i];
            var c = route[i + 1];

            var incoming = Heading(a, b);
            var outgoing = Heading(b, c);
            var angle = WrapDegrees(outgoing - incoming);
            if (Math.Abs(angle) >= MinimumTurnDegrees)
            {
                return new(
                    Available: true,
                    Maneuver: Classify(angle),
                    TurnAngleDegrees: Math.Round(angle, 1),
                    DistanceToManeuverMeters: Math.Round(accumulated, 1),
                    TargetX: b.X,
                    TargetY: b.Y);
            }

            accumulated += SegmentLength(b, c);
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
