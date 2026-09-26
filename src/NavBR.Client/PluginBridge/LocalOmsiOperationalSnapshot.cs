using NavBR.Shared.PluginBridge;

namespace NavBR.Client.PluginBridge;

public sealed record LocalOmsiOperationalSnapshot(
    DateTimeOffset CapturedAtUtc,
    double? CabinTemperatureC,
    int? PassengerCount,
    bool? ScheduleActive,
    double? SimulationTime,
    int? SimulationDay,
    int? SimulationMonth,
    int? SimulationYear,
    bool? SimulationPaused,
    string? IbisLineCourse,
    string? IbisRouteCode,
    string? IbisTerminusName,
    string? IbisDelayMinutes,
    string? IbisDelaySeconds,
    string? IbisDelayState)
{
    public static LocalOmsiOperationalSnapshot FromMessage(
        PluginBridgeMessage message)
    {
        var capturedAt = message.TimestampUnixMilliseconds is long ms
            ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
            : DateTimeOffset.UtcNow;

        return new LocalOmsiOperationalSnapshot(
            capturedAt,
            message.CabinTemperatureC,
            message.PassengerCount,
            message.ScheduleActive,
            message.SimulationTime,
            message.SimulationDay,
            message.SimulationMonth,
            message.SimulationYear,
            message.SimulationPaused,
            message.IbisLineCourse,
            message.IbisRouteCode,
            message.IbisTerminusName,
            message.IbisDelayMinutes,
            message.IbisDelaySeconds,
            message.IbisDelayState);
    }
}

public static class LocalOmsiOperationalSnapshotStore
{
    private static LocalOmsiOperationalSnapshot? _latest;

    public static LocalOmsiOperationalSnapshot? Latest =>
        Volatile.Read(ref _latest);

    public static void Update(PluginBridgeMessage message) =>
        Volatile.Write(
            ref _latest,
            LocalOmsiOperationalSnapshot.FromMessage(message));

    public static void Clear() =>
        Volatile.Write(ref _latest, null);
}
