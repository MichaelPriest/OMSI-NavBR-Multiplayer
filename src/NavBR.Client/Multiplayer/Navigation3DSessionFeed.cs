using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

internal sealed record Navigation3DRemoteVehicle(
    PlayerTelemetryFrame Frame,
    DateTimeOffset ReceivedAtUtc);

internal sealed record Navigation3DRemoteRoleplay(
    RoleplayCharacterFrame Frame,
    DateTimeOffset ReceivedAtUtc);

internal static class Navigation3DSessionFeed
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Navigation3DRemoteVehicle> Vehicles =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Navigation3DRemoteRoleplay> RoleplayCharacters =
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

    public static void UpdateRoleplay(RoleplayCharacterFrame frame)
    {
        if (string.IsNullOrWhiteSpace(frame.Player.PlayerId) ||
            !frame.Character.IsActive)
        {
            return;
        }

        lock (Sync)
        {
            RoleplayCharacters[frame.Player.PlayerId] =
                new Navigation3DRemoteRoleplay(
                    frame,
                    DateTimeOffset.UtcNow);
        }
    }

    public static void RemoveRoleplay(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        lock (Sync)
        {
            RoleplayCharacters.Remove(playerId);
        }
    }

    public static void Remove(string playerId)
    {
        lock (Sync)
        {
            Vehicles.Remove(playerId);
            RoleplayCharacters.Remove(playerId);
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Vehicles.Clear();
            RoleplayCharacters.Clear();
        }
    }

    public static IReadOnlyList<Navigation3DRemoteVehicle> Snapshot()
    {
        lock (Sync)
        {
            PruneStaleLocked();
            return Vehicles.Values
                .OrderBy(value => value.Frame.Player.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    public static IReadOnlyList<Navigation3DRemoteRoleplay> SnapshotRoleplay()
    {
        lock (Sync)
        {
            PruneStaleLocked();
            return RoleplayCharacters.Values
                .OrderBy(value => value.Frame.Player.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    private static void PruneStaleLocked()
    {
        var now = DateTimeOffset.UtcNow;
        var staleVehicles = Vehicles
            .Where(pair => now - pair.Value.ReceivedAtUtc > TimeSpan.FromSeconds(8d))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var playerId in staleVehicles)
        {
            Vehicles.Remove(playerId);
        }

        var staleRoleplay = RoleplayCharacters
            .Where(pair => now - pair.Value.ReceivedAtUtc > TimeSpan.FromSeconds(8d))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var playerId in staleRoleplay)
        {
            RoleplayCharacters.Remove(playerId);
        }
    }
}
