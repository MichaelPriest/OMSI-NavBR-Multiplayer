using NavBR.Shared.PluginBridge;

namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiMapVisualState(
    bool Available,
    OpenOmsiRoutePoint[] TraveledRoute,
    OpenOmsiRoutePoint[] ForwardRoute,
    OpenOmsiRoutePoint[] RejoinRoute,
    int CurrentRoutePointIndex);

internal static class OpenOmsiMapVisualRuntime
{
    public static OpenOmsiMapVisualState Build(
        OpenOmsiRoutePoint[] route,
        OpenOmsiLuaSnapshot? snapshot,
        OpenOmsiRouteRuntimeState routeState)
    {
        if (route.Length < 2 ||
            snapshot?.HasPosition != true ||
            snapshot.X is null ||
            snapshot.Y is null ||
            routeState.NearestRoutePointIndex is null)
        {
            return Empty();
        }

        var current = Math.Clamp(
            routeState.NearestRoutePointIndex.Value,
            0,
            route.Length - 1);

        var traveled = route
            .Take(current + 1)
            .ToArray();

        var forward = route
            .Skip(current)
            .ToArray();

        OpenOmsiRoutePoint[] rejoin = [];
        if (routeState.OffRoute &&
            routeState.RejoinTargetX is not null &&
            routeState.RejoinTargetY is not null)
        {
            rejoin =
            [
                new OpenOmsiRoutePoint(
                    snapshot.X.Value,
                    snapshot.Y.Value,
                    snapshot.Z),
                new OpenOmsiRoutePoint(
                    routeState.RejoinTargetX.Value,
                    routeState.RejoinTargetY.Value)
            ];
        }

        return new(
            Available: true,
            TraveledRoute: traveled,
            ForwardRoute: forward,
            RejoinRoute: rejoin,
            CurrentRoutePointIndex: current);
    }

    private static OpenOmsiMapVisualState Empty() =>
        new(false, [], [], [], 0);
}
