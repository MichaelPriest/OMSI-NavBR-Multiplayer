namespace NavBR.Shared.Multiplayer;

public sealed record RoomSnapshot(
    string RoomId,
    IReadOnlyList<PlayerPresence> Players);
