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

    /// <summary>
    /// Number of remote buses that this client has successfully materialized
    /// through OMSI MakeVehicle. Null means the client has not reported it.
    /// </summary>
    public int? PhysicalVehicleCount { get; init; }
}
