namespace NavBR.Shared.Multiplayer;

public sealed record SessionOperationalState(
    string AuthorityPlayerId,
    long Sequence,
    DateTimeOffset ServerTimestampUtc,
    string? MapName,
    string? MapCompatibilityId,
    string? Line,
    string? Route,
    string? DestinationName,
    string? NextStopName);
