namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiTeleMatrixState(
    bool Available,
    string? Line,
    string? Destination,
    string? NextStop,
    int? StopNumber,
    int? StopCount,
    int? DelaySeconds,
    double? NextArrivalSeconds,
    double? NextDepartureSeconds,
    string PunctualityState);

internal static class OpenOmsiTeleMatrixRuntime
{
    public static OpenOmsiTeleMatrixState Build(
        OpenOmsiLuaSnapshot? snapshot,
        OpenOmsiTimetableTrip? trip)
    {
        if (snapshot is null)
        {
            return new(
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "unknown");
        }

        var delay = snapshot.DelaySeconds;
        var punctuality = delay switch
        {
            null => "unknown",
            > 120 => "late",
            < -120 => "early",
            _ => "on-time"
        };

        return new(
            Available: !string.IsNullOrWhiteSpace(snapshot.Line) ||
                       !string.IsNullOrWhiteSpace(snapshot.NextStop) ||
                       trip is not null,
            Line: snapshot.Line ?? trip?.Line,
            Destination: snapshot.Terminus ?? trip?.Terminus,
            NextStop: snapshot.NextStop,
            StopNumber: snapshot.NextStopNumber,
            StopCount: trip?.Stops.Length,
            DelaySeconds: delay,
            NextArrivalSeconds: snapshot.NextStopArrival,
            NextDepartureSeconds: snapshot.NextStopDeparture,
            PunctualityState: punctuality);
    }
}
