using NavBR.Client.Maps;

namespace NavBR.Client;

internal sealed record FigmaOrderedRouteStopsSnapshot(
    bool RouteResolved,
    int TotalStops,
    int? NextStopIndex,
    IReadOnlyList<string> UpcomingStops)
{
    public static FigmaOrderedRouteStopsSnapshot Unavailable() =>
        new(false, 0, null, Array.Empty<string>());
}

public partial class MainWindow
{
    internal FigmaOrderedRouteStopsSnapshot GetOrderedRouteStopsForAlpha12()
    {
        var telemetry = _lastTelemetry;
        var map = GetActiveMapForOperations();
        if (telemetry is null || map is null)
        {
            return FigmaOrderedRouteStopsSnapshot.Unavailable();
        }

        var sequence = OmsiOrderedRouteStopReader.TryRead(
            map,
            telemetry.Route,
            telemetry.Line,
            telemetry.DestinationName);
        if (!sequence.RouteResolved || sequence.StopNames.Count == 0)
        {
            return FigmaOrderedRouteStopsSnapshot.Unavailable();
        }

        var nextIndex = ResolveNextStopIndex(
            sequence.StopNames,
            telemetry.NextStopName,
            telemetry.CurrentStopIndex);
        if (!nextIndex.HasValue)
        {
            return new FigmaOrderedRouteStopsSnapshot(
                true,
                sequence.StopNames.Count,
                null,
                Array.Empty<string>());
        }

        var upcoming = sequence.StopNames
            .Skip(nextIndex.Value)
            .Take(6)
            .ToArray();
        return new FigmaOrderedRouteStopsSnapshot(
            true,
            sequence.StopNames.Count,
            nextIndex,
            upcoming);
    }

    private static int? ResolveNextStopIndex(
        IReadOnlyList<string> stops,
        string? nextStopName,
        int? currentStopIndex)
    {
        var target = OmsiOrderedRouteStopReader.Normalize(nextStopName);
        if (target.Length == 0)
        {
            return null;
        }

        var matches = new List<int>();
        for (var index = 0; index < stops.Count; index++)
        {
            var candidate = OmsiOrderedRouteStopReader.Normalize(stops[index]);
            if (candidate.Length > 0 &&
                (candidate == target ||
                 candidate.Contains(target, StringComparison.Ordinal) ||
                 target.Contains(candidate, StringComparison.Ordinal)))
            {
                matches.Add(index);
            }
        }

        if (matches.Count == 0)
        {
            return null;
        }

        if (currentStopIndex is int hint)
        {
            // OMSI map/mod variants do not all expose CurrentStopIndex with the
            // same base. Treat it only as a disambiguation hint for duplicate
            // stop names, never as authoritative sequence position.
            var clamped = Math.Clamp(hint, 0, Math.Max(0, stops.Count - 1));
            return matches.FirstOrDefault(index => index >= clamped, matches[0]);
        }

        return matches[0];
    }
}
