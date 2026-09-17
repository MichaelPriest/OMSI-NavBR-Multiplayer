using System.Globalization;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Maps;

public enum NavBRManeuverKind
{
    None = 0,
    SlightLeft,
    Left,
    SharpLeft,
    SlightRight,
    Right,
    SharpRight,
    RejoinRoute
}

public sealed record NavBRNavigationSnapshot(
    bool RouteAvailable,
    bool IsOnRoute,
    double OffRouteDistanceMeters,
    double RouteProgressPercent,
    double DistanceRemainingMeters,
    double? DistanceToNextStopMeters,
    NavBRManeuverKind Maneuver,
    double? DistanceToManeuverMeters,
    string? NextStopName,
    string? DestinationName,
    string? CurrentStreetName,
    int? CurrentStopIndex)
{
    public static NavBRNavigationSnapshot Unavailable(VehicleTelemetry? telemetry = null) => new(
        RouteAvailable: false,
        IsOnRoute: false,
        OffRouteDistanceMeters: 0d,
        RouteProgressPercent: 0d,
        DistanceRemainingMeters: 0d,
        DistanceToNextStopMeters: null,
        Maneuver: NavBRManeuverKind.None,
        DistanceToManeuverMeters: null,
        NextStopName: telemetry?.NextStopName,
        DestinationName: telemetry?.DestinationName,
        CurrentStreetName: telemetry?.CurrentStreetName,
        CurrentStopIndex: telemetry?.CurrentStopIndex);
}

/// <summary>
/// Shared GPS/navigation calculation for the desktop map and HUD. It only uses
/// route geometry and stop data read from the installed OMSI map; when those
/// data are incomplete it fails closed instead of inventing a route.
/// </summary>
public static class NavBRNavigationEngine
{
    private const double OnRouteThresholdMeters = 65d;
    private const double RejoinThresholdMeters = 90d;
    private const double StopRouteToleranceMeters = 85d;
    private const double MaxManeuverLookAheadMeters = 650d;

    public static NavBRNavigationSnapshot Evaluate(
        VehicleTelemetry? telemetry,
        OmsiMapLayout? layout,
        IReadOnlyList<OmsiRouteTracePoint>? routeTrace,
        IReadOnlyList<OmsiBusStopPoint>? busStops = null)
    {
        if (telemetry is null ||
            layout?.TileSize is not double tileSize ||
            routeTrace is null ||
            routeTrace.Count < 2 ||
            telemetry.GridX is not int gridX ||
            telemetry.GridY is not int gridY ||
            telemetry.TileX is not double tileX ||
            telemetry.TileY is not double tileY)
        {
            return NavBRNavigationSnapshot.Unavailable(telemetry);
        }

        var route = BuildRoute(routeTrace, tileSize);
        if (route.Points.Count < 2 || route.TotalLength <= 1d)
        {
            return NavBRNavigationSnapshot.Unavailable(telemetry);
        }

        var vehicle = new WorldPoint(
            gridX * tileSize + tileX,
            gridY * tileSize + tileY);
        var projection = ProjectOntoRoute(vehicle, route);
        if (!projection.IsValid)
        {
            return NavBRNavigationSnapshot.Unavailable(telemetry);
        }

        var offRouteDistance = Math.Sqrt(projection.DistanceSquared);
        var progress = Math.Clamp(
            projection.RouteDistance / route.TotalLength * 100d,
            0d,
            100d);
        var remaining = Math.Max(0d, route.TotalLength - projection.RouteDistance);
        var onRoute = offRouteDistance <= OnRouteThresholdMeters;

        var maneuver = NavBRManeuverKind.None;
        double? maneuverDistance = null;
        if (offRouteDistance >= RejoinThresholdMeters)
        {
            maneuver = NavBRManeuverKind.RejoinRoute;
            maneuverDistance = offRouteDistance;
        }
        else
        {
            (maneuver, maneuverDistance) = FindNextManeuver(route, projection.RouteDistance);
        }

        var distanceToNextStop = FindDistanceToNextStop(
            telemetry.NextStopName,
            busStops,
            route,
            projection.RouteDistance);

        return new NavBRNavigationSnapshot(
            RouteAvailable: true,
            IsOnRoute: onRoute,
            OffRouteDistanceMeters: offRouteDistance,
            RouteProgressPercent: progress,
            DistanceRemainingMeters: remaining,
            DistanceToNextStopMeters: distanceToNextStop,
            Maneuver: maneuver,
            DistanceToManeuverMeters: maneuverDistance,
            NextStopName: telemetry.NextStopName,
            DestinationName: telemetry.DestinationName,
            CurrentStreetName: telemetry.CurrentStreetName,
            CurrentStopIndex: telemetry.CurrentStopIndex);
    }

    private static (NavBRManeuverKind Kind, double? Distance) FindNextManeuver(
        RouteGeometry route,
        double currentDistance)
    {
        if (route.TotalLength - currentDistance < 35d)
        {
            return (NavBRManeuverKind.None, null);
        }

        var maxDistance = Math.Min(
            route.TotalLength - 20d,
            currentDistance + MaxManeuverLookAheadMeters);

        for (var candidate = currentDistance + 35d; candidate <= maxDistance; candidate += 10d)
        {
            var before = Math.Max(0d, candidate - 20d);
            var after = Math.Min(route.TotalLength, candidate + 20d);
            var beforePoint = PointAtDistance(route, before);
            var centrePoint = PointAtDistance(route, candidate);
            var afterPoint = PointAtDistance(route, after);

            var incoming = BearingDegrees(beforePoint, centrePoint);
            var outgoing = BearingDegrees(centrePoint, afterPoint);
            var delta = NormalizeSignedAngle(outgoing - incoming);
            var magnitude = Math.Abs(delta);
            if (magnitude < 28d)
            {
                continue;
            }

            var kind = delta > 0d
                ? magnitude switch
                {
                    < 48d => NavBRManeuverKind.SlightRight,
                    < 105d => NavBRManeuverKind.Right,
                    _ => NavBRManeuverKind.SharpRight
                }
                : magnitude switch
                {
                    < 48d => NavBRManeuverKind.SlightLeft,
                    < 105d => NavBRManeuverKind.Left,
                    _ => NavBRManeuverKind.SharpLeft
                };

            return (kind, Math.Max(0d, candidate - currentDistance));
        }

        return (NavBRManeuverKind.None, null);
    }

    private static double? FindDistanceToNextStop(
        string? nextStopName,
        IReadOnlyList<OmsiBusStopPoint>? busStops,
        RouteGeometry route,
        double currentRouteDistance)
    {
        if (string.IsNullOrWhiteSpace(nextStopName) || busStops is null || busStops.Count == 0)
        {
            return null;
        }

        var target = NormalizeName(nextStopName);
        if (target.Length == 0)
        {
            return null;
        }

        double? best = null;
        foreach (var stop in busStops)
        {
            var stopName = NormalizeName(stop.Name);
            if (!NamesMatch(stopName, target))
            {
                continue;
            }

            var stopPoint = new WorldPoint(
                stop.GridX * route.TileSize + stop.TileX,
                stop.GridY * route.TileSize + stop.TileY);
            var projection = ProjectOntoRoute(stopPoint, route);
            if (!projection.IsValid ||
                Math.Sqrt(projection.DistanceSquared) > StopRouteToleranceMeters)
            {
                continue;
            }

            var distance = projection.RouteDistance - currentRouteDistance;
            if (distance < -25d)
            {
                continue;
            }

            distance = Math.Max(0d, distance);
            if (best is null || distance < best.Value)
            {
                best = distance;
            }
        }

        return best;
    }

    private static RouteGeometry BuildRoute(
        IReadOnlyList<OmsiRouteTracePoint> trace,
        double tileSize)
    {
        var points = new List<WorldPoint>(trace.Count);
        foreach (var point in trace)
        {
            var world = new WorldPoint(
                point.GridX * tileSize + point.TileX,
                point.GridY * tileSize + point.TileY);
            if (points.Count == 0 || DistanceSquared(points[^1], world) > 0.01d)
            {
                points.Add(world);
            }
        }

        var cumulative = new double[points.Count];
        for (var index = 1; index < points.Count; index++)
        {
            cumulative[index] = cumulative[index - 1] + Distance(points[index - 1], points[index]);
        }

        return new RouteGeometry(points, cumulative, cumulative.Length == 0 ? 0d : cumulative[^1], tileSize);
    }

    private static RouteProjection ProjectOntoRoute(WorldPoint point, RouteGeometry route)
    {
        var best = RouteProjection.Invalid;
        for (var index = 1; index < route.Points.Count; index++)
        {
            var a = route.Points[index - 1];
            var b = route.Points[index];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= 0.000001d)
            {
                continue;
            }

            var t = ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / lengthSquared;
            t = Math.Clamp(t, 0d, 1d);
            var projected = new WorldPoint(a.X + t * dx, a.Y + t * dy);
            var distanceSquared = DistanceSquared(point, projected);
            if (distanceSquared >= best.DistanceSquared)
            {
                continue;
            }

            var segmentLength = Math.Sqrt(lengthSquared);
            best = new RouteProjection(
                true,
                distanceSquared,
                route.Cumulative[index - 1] + t * segmentLength,
                projected);
        }

        return best;
    }

    private static WorldPoint PointAtDistance(RouteGeometry route, double distance)
    {
        distance = Math.Clamp(distance, 0d, route.TotalLength);
        if (distance <= 0d)
        {
            return route.Points[0];
        }

        for (var index = 1; index < route.Cumulative.Length; index++)
        {
            if (route.Cumulative[index] < distance)
            {
                continue;
            }

            var segmentStart = route.Cumulative[index - 1];
            var segmentLength = route.Cumulative[index] - segmentStart;
            if (segmentLength <= 0.000001d)
            {
                return route.Points[index];
            }

            var t = (distance - segmentStart) / segmentLength;
            var a = route.Points[index - 1];
            var b = route.Points[index];
            return new WorldPoint(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t);
        }

        return route.Points[^1];
    }

    private static double BearingDegrees(WorldPoint from, WorldPoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return Math.Atan2(dx, dy) * 180d / Math.PI;
    }

    private static double NormalizeSignedAngle(double angle)
    {
        angle %= 360d;
        if (angle > 180d)
        {
            angle -= 360d;
        }
        else if (angle < -180d)
        {
            angle += 360d;
        }
        return angle;
    }

    private static double Distance(WorldPoint a, WorldPoint b) => Math.Sqrt(DistanceSquared(a, b));

    private static double DistanceSquared(WorldPoint a, WorldPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return dx * dx + dy * dy;
    }

    private static bool NamesMatch(string a, string b) =>
        a.Length > 0 && b.Length > 0 &&
        (string.Equals(a, b, StringComparison.Ordinal) ||
         a.Contains(b, StringComparison.Ordinal) ||
         b.Contains(a, StringComparison.Ordinal));

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => char.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private sealed record RouteGeometry(
        IReadOnlyList<WorldPoint> Points,
        double[] Cumulative,
        double TotalLength,
        double TileSize);

    private readonly record struct WorldPoint(double X, double Y);

    private readonly record struct RouteProjection(
        bool IsValid,
        double DistanceSquared,
        double RouteDistance,
        WorldPoint Point)
    {
        public static RouteProjection Invalid => new(
            false,
            double.MaxValue,
            0d,
            default);
    }
}
