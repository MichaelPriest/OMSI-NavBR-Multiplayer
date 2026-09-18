namespace NavBR.Shared.Multiplayer;

public sealed record PlayerPresence(
    string PlayerId,
    string DisplayName,
    string RoomId,
    string? MapName,
    DateTimeOffset ConnectedAtUtc,
    string? MapCompatibilityId = null,
    OmsiCompatibilityManifest? Compatibility = null)
{
    public bool? VoiceEnabled { get; init; }

    public int? LatencyMs { get; init; }
}
