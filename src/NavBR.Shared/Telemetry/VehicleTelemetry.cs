namespace NavBR.Shared.Telemetry;

public sealed record VehicleTelemetry(
    string PlayerId,
    DateTimeOffset Timestamp,
    string? MapName,
    string? VehicleName,
    string? Line,
    string? Route,
    double X,
    double Y,
    double Z,
    double HeadingDegrees,
    double SpeedKph,
    bool IsInGame,
    int? GridX = null,
    int? GridY = null,
    double? TileX = null,
    double? TileY = null,
    string? MapCompatibilityId = null,
    string? NextStopName = null);
