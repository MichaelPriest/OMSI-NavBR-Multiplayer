namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiTimetableRuntimeState(
    string? DutyKey,
    OpenOmsiTimetableTrip? Trip,
    OpenOmsiRouteStep[] RouteSteps);

internal static class OpenOmsiTimetableRuntime
{
    private static readonly object Sync = new();
    private static string? _cachedDutyKey;
    private static string? _cachedTimetableDirectory;
    private static OpenOmsiTimetableRuntimeState _cached =
        new(null, null, []);

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

            _cachedDutyKey = dutyKey;
            _cachedTimetableDirectory = directory;
            _cached = new(dutyKey, trip, steps);
            return _cached;
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            _cachedDutyKey = null;
            _cachedTimetableDirectory = null;
            _cached = new(null, null, []);
        }
    }
}
