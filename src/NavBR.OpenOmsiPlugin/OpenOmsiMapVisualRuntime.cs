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
    private const double BackwardWindowMeters = 600d;
    private const double ForwardWindowMeters = 2400d;
    private const double MinimumVisualSpacingMeters = 12d;
    private const int MaximumTraveledPoints = 72;
    private const int MaximumForwardPoints = 180;

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

        var traveled = BuildWindow(
            route,
            current,
            backwards: true,
            BackwardWindowMeters,
            MaximumTraveledPoints);

        var forward = BuildWindow(
            route,
            current,
            backwards: false,
            ForwardWindowMeters,
            MaximumForwardPoints);

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

    private static OpenOmsiRoutePoint[] BuildWindow(
        OpenOmsiRoutePoint[] route,
        int current,
        bool backwards,
        double maximumDistanceMeters,
        int maximumPoints)
    {
        var raw = new List<OpenOmsiRoutePoint>();
        var distance = 0d;
        var index = current;
        raw.Add(route[index]);

        while (true)
        {
            var next = backwards
                ? index - 1
                : index + 1;
            if (next < 0 || next >= route.Length)
            {
                break;
            }

            distance += Distance(route[index], route[next]);
            if (distance > maximumDistanceMeters)
            {
                break;
            }

            raw.Add(route[next]);
            index = next;
        }

        if (backwards)
        {
            raw.Reverse();
        }

        if (raw.Count <= 2)
        {
            return raw.ToArray();
        }

        var result = new List<OpenOmsiRoutePoint>(
            Math.Min(maximumPoints, raw.Count))
        {
            raw[0]
        };
        var last = raw[0];

        for (var i = 1; i < raw.Count - 1; i++)
        {
            var point = raw[i];
            var important = !string.IsNullOrWhiteSpace(point.StopName);
            if (!important &&
                Distance(last, point) < MinimumVisualSpacingMeters)
            {
                continue;
            }

            result.Add(point);
            last = point;

            if (result.Count >= maximumPoints - 1)
            {
                break;
            }
        }

        var end = raw[^1];
        if (!ReferenceEquals(result[^1], end) &&
            (result[^1].X != end.X ||
             result[^1].Y != end.Y ||
             result[^1].Z != end.Z))
        {
            if (result.Count >= maximumPoints)
            {
                result[^1] = end;
            }
            else
            {
                result.Add(end);
            }
        }

        return result.ToArray();
    }

    private static double Distance(
        OpenOmsiRoutePoint a,
        OpenOmsiRoutePoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static OpenOmsiMapVisualState Empty() =>
        new(false, [], [], [], 0);
}
