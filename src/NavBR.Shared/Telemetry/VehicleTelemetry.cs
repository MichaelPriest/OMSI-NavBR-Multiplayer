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
    string? NextStopName = null,
    string? DestinationName = null,
    string? VehiclePath = null,
    string? HofName = null,
    double? AccelerationMps2 = null,
    double? FuelPercent = null,
    double? ThrottlePercent = null,
    double? BrakePercent = null,
    double? SteeringDegrees = null,
    int? DelaySeconds = null,
    int? CurrentStopIndex = null,
    VehicleDoorFlags Doors = VehicleDoorFlags.None,
    VehicleLightFlags Lights = VehicleLightFlags.None,
    TurnSignalState TurnSignal = TurnSignalState.Off,
    bool HornActive = false,
    bool WipersActive = false,
    bool ParkingBrakeActive = false,
    bool ReverseGear = false,
    string? VehicleCompatibilityId = null,
    string? HofCompatibilityId = null,
    double? LocalX = null,
    double? LocalY = null,
    double? LocalZ = null,
    double? RotationX = null,
    double? RotationY = null,
    double? RotationZ = null,
    double? RotationW = null);

[Flags]
public enum VehicleDoorFlags
{
    None = 0,
    Front = 1 << 0,
    Middle = 1 << 1,
    Rear = 1 << 2,
    Extra1 = 1 << 3,
    Extra2 = 1 << 4
}

[Flags]
public enum VehicleLightFlags
{
    None = 0,
    Position = 1 << 0,
    LowBeam = 1 << 1,
    HighBeam = 1 << 2,
    Fog = 1 << 3,
    Brake = 1 << 4,
    Reverse = 1 << 5,
    Interior = 1 << 6,
    Hazard = 1 << 7
}

public enum TurnSignalState
{
    Off = 0,
    Left = 1,
    Right = 2,
    Hazard = 3
}
