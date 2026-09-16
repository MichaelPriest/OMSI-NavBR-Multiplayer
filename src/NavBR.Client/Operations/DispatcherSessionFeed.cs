using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Operations;

internal sealed record DispatcherRemoteDriver(
    string PlayerId,
    string DisplayName,
    string RoomId,
    string? MapName,
    string? VehicleName,
    string? Line,
    string? Route,
    string? Destination,
    string? NextStop,
    double SpeedKph,
    int? DelaySeconds,
    DateTimeOffset TelemetryTimestamp,
    DateTimeOffset ReceivedAtUtc);

internal sealed record DispatcherSessionSnapshot(
    bool Connected,
    IReadOnlyList<DispatcherRemoteDriver> RemoteDrivers,
    DateTimeOffset UpdatedAt);

internal static class DispatcherSessionFeed
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, DispatcherRemoteDriver> Drivers =
        new(StringComparer.OrdinalIgnoreCase);
    private static bool _connected;
    private static DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;

    public static void SetConnected(bool connected)
    {
        lock (Sync)
        {
            _connected = connected;
            _updatedAt = DateTimeOffset.UtcNow;
            if (!connected)
            {
                Drivers.Clear();
            }
        }
    }

    public static void Update(PlayerTelemetryFrame frame)
    {
        var telemetry = frame.Telemetry;
        var receivedAt = DateTimeOffset.UtcNow;
        var driver = new DispatcherRemoteDriver(
            frame.Player.PlayerId,
            frame.Player.DisplayName,
            frame.Player.RoomId,
            telemetry.MapName ?? frame.Player.MapName,
            telemetry.VehicleName,
            telemetry.Line,
            telemetry.Route,
            telemetry.DestinationName,
            telemetry.NextStopName,
            Math.Clamp(double.IsFinite(telemetry.SpeedKph) ? telemetry.SpeedKph : 0d, 0d, 220d),
            telemetry.DelaySeconds,
            telemetry.Timestamp,
            receivedAt);

        lock (Sync)
        {
            Drivers[driver.PlayerId] = driver;
            _connected = true;
            _updatedAt = receivedAt;
        }
    }

    public static void Remove(string playerId)
    {
        lock (Sync)
        {
            Drivers.Remove(playerId);
            _updatedAt = DateTimeOffset.UtcNow;
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Drivers.Clear();
            _updatedAt = DateTimeOffset.UtcNow;
        }
    }

    public static DispatcherSessionSnapshot Snapshot()
    {
        lock (Sync)
        {
            var now = DateTimeOffset.UtcNow;
            var stale = Drivers
                .Where(pair => now - pair.Value.ReceivedAtUtc > TimeSpan.FromSeconds(10d))
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var playerId in stale)
            {
                Drivers.Remove(playerId);
            }

            return new DispatcherSessionSnapshot(
                _connected,
                Drivers.Values
                    .OrderBy(driver => driver.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray(),
                _updatedAt);
        }
    }
}
