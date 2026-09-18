namespace NavBR.Shared.Multiplayer;

public sealed record PublicRoomSummary(
    string RoomId,
    int PlayerCount,
    string? MapName,
    string? MapCompatibilityId,
    DateTimeOffset UpdatedAtUtc,
    string? OmsiVersion = null,
    string? NavBRVersion = null,
    string? VehiclePath = null,
    string? VehicleCompatibilityId = null,
    string? HofName = null,
    string? HofCompatibilityId = null,
    int PluginProtocolVersion = 0);
