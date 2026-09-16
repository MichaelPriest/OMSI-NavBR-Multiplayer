namespace NavBR.Shared.Multiplayer;

public sealed record PublicRoomSummary(
    string RoomId,
    int PlayerCount,
    string? MapName,
    string? MapCompatibilityId,
    DateTimeOffset UpdatedAtUtc);
