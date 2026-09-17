using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

internal sealed record Navigation3DRemoteVehicle(
    PlayerTelemetryFrame Frame,
    DateTimeOffset ReceivedAtUtc);

internal static class Navigation3DSessionFeed
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Navigation3DRemoteVehicle> Vehicles =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Update(PlayerTelemetryFrame frame)
    {
        if (string.IsNullOrWhiteSpace(frame.Player.PlayerId))
        {
            return;
        }

        lock (Sync)
        {
            Vehicles[frame.Player.PlayerId] = new Navigation3DRemoteVehicle(
                frame,
                DateTimeOffset.UtcNow);
        }
    }

    public static void Remove(string playerId)
    {
        lock (Sync)
        {
            Vehicles.Remove(playerId);
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Vehicles.Clear();
        }
    }

    public static IReadOnlyList<Navigation3DRemoteVehicle> Snapshot()
    {
        lock (Sync)
        {
            var now = DateTimeOffset.UtcNow;
            var stale = Vehicles
                .Where(pair => now - pair.Value.ReceivedAtUtc > TimeSpan.FromSeconds(8d))
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var playerId in stale)
            {
                Vehicles.Remove(playerId);
            }

            return Vehicles.Values
                .OrderBy(value => value.Frame.Player.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }
}
