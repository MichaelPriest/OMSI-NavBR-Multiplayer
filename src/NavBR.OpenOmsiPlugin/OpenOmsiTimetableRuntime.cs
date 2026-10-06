using NavBR.Shared.PluginBridge;
namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiTimetableRuntimeState(
    string? DutyKey,
    OpenOmsiTimetableTrip? Trip,
    OpenOmsiRouteStep[] RouteSteps,
    OpenOmsiRoutePoint[] RoutePoints);

internal static class OpenOmsiTimetableRuntime
{
    private static readonly object Sync = new();
    private static string? _cachedDutyKey;
    private static string? _cachedTimetableDirectory;
    private static OpenOmsiTimetableRuntimeState _cached =
        new(null, null, [], []);

    public static OpenOmsiTimetableRuntimeState Resolve(
        OpenOmsiContentContext content,
        OpenOmsiLuaSnapshot? snapshot)
    {
        var dutyKey = OpenOmsiRouteRuntime.BuildRouteKey(snapshot);
        var directory = content.TimetableDirectory;

        lock (Sync)
        {
            if (string.Equals(
                    _cachedDutyKey,
                    dutyKey,
                    StringComparison.Ordinal) &&
                string.Equals(
                    _cachedTimetableDirectory,
                    directory,
                    StringComparison.OrdinalIgnoreCase))
            {
                return _cached;
            }

            var trip = OpenOmsiTimetableResolver.Resolve(content, snapshot);
            var steps = OpenOmsiRouteStepResolver.Resolve(content, trip);
            var points = OpenOmsiRouteGeometryResolver.Resolve(content, steps);

            if (!string.IsNullOrWhiteSpace(dutyKey) && points.Length >= 2)
            {
                OpenOmsiRouteRuntime.SetRoute(points, dutyKey);
            }

            _cachedDutyKey = dutyKey;
            _cachedTimetableDirectory = directory;
            _cached = new(dutyKey, trip, steps, points);
            return _cached;
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            _cachedDutyKey = null;
            _cachedTimetableDirectory = null;
            _cached = new(null, null, [], []);
        }
    }
}
