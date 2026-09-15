namespace NavBR.Shared.Multiplayer;

/// <summary>
/// Compact road-traffic state replicated by the room authority. The authority
/// is normally the first player in the room (the peer host in the standard
/// NavBR flow). Pedestrians are intentionally excluded from alpha.11 traffic
/// replication because their population is much larger and would multiply
/// network/CPU cost without improving bus-to-bus driving consistency.
/// </summary>
public sealed record TrafficSnapshot(
    string AuthorityPlayerId,
    long Sequence,
    DateTimeOffset TimestampUtc,
    string? MapName,
    string? MapCompatibilityId,
    IReadOnlyList<TrafficVehicleState> Vehicles);

public sealed record TrafficVehicleState(
    string TrafficId,
    string? VehiclePath,
    string? VehicleCompatibilityId,
    double X,
    double Y,
    double Z,
    double LocalX,
    double LocalY,
    double LocalZ,
    double RotationX,
    double RotationY,
    double RotationZ,
    double RotationW,
    double SpeedKph,
    int LightFlags = 0,
    int TurnSignal = 0);
