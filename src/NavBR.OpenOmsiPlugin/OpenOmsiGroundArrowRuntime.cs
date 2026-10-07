using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiGroundArrowState(
    double X,
    double Y,
    double? Z,
    double HeadingDegrees,
    double DistanceAheadMeters,
    string Kind);

internal static class OpenOmsiGroundArrowRuntime
{
    private const double NearArrowLimitMeters = 220d;

    public static OpenOmsiGroundArrowState[] Build(
        OpenOmsiGuidanceWaypointState[] waypoints,
        OpenOmsiGuidanceState guidance,
        OpenOmsiRouteRuntimeState route)
    {
        if (waypoints.Length == 0)
        {
            return [];
        }

        var maneuverDistance = guidance.DistanceToManeuverMeters;
        return waypoints
            .Where(point => point.DistanceAheadMeters <= NearArrowLimitMeters)
            .Select(point =>
            {
                var kind = route.OffRoute
                    ? "rejoin"
                    : maneuverDistance is not null &&
                      Math.Abs(point.DistanceAheadMeters - maneuverDistance.Value) <= 22d
                        ? guidance.Maneuver ?? "route"
                        : "route";

                return new OpenOmsiGroundArrowState(
                    X: point.X,
                    Y: point.Y,
                    Z: point.Z,
                    HeadingDegrees: point.HeadingDegrees,
                    DistanceAheadMeters: point.DistanceAheadMeters,
                    Kind: kind);
            })
            .ToArray();
    }
}
