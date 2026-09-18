namespace NavBR.Shared.Multiplayer;

public sealed record RoomSnapshot(
    string RoomId,
    IReadOnlyList<PlayerPresence> Players,
    string? TrafficAuthorityPlayerId = null,
    bool IsPrivate = false,
    string? OwnerPlayerId = null);
