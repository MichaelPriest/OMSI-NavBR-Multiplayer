namespace NavBR.Shared.Multiplayer;

public sealed record PlayerPresence(
    string PlayerId,
    string DisplayName,
    string RoomId,
    string? MapName,
    DateTimeOffset ConnectedAtUtc);
