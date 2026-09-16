namespace NavBR.Shared.Multiplayer;

public sealed record ExternalPortProbeResult(
    bool Reachable,
    int Port,
    string Status,
    DateTimeOffset CheckedAtUtc,
    long DurationMilliseconds);
