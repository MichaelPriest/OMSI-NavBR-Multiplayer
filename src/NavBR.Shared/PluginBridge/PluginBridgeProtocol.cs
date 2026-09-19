using NavBR.Shared.Multiplayer;

namespace NavBR.Shared.PluginBridge;

public static class PluginBridgeProtocol
{
    public const int Version = 3;
    public const string PipeName = "OMSI.NavBR.Multiplayer.Plugin.v3";
    public const int MaxMessageChars = 32_768;

    public const string PluginHello = "plugin-hello";
    public const string PluginStatus = "plugin-status";
    public const string PluginCapabilities = "plugin-capabilities";
    public const string ClientHello = "client-hello";
    public const string LocalVehicleState = "local-vehicle-state";
    public const string RemoteVehicleState = "remote-vehicle-state";
    public const string RemoteVehicleRemoved = "remote-vehicle-removed";
    public const string ClearRemoteVehicles = "clear-remote-vehicles";
    public const string TrafficSnapshotState = "traffic-snapshot-state";
    public const string ClearTrafficVehicles = "clear-traffic-vehicles";

    // Alpha.11 experimental write-side commands. These messages are accepted only
    // when the plugin reports the corresponding capability and experimental writes
    // are explicitly enabled by the user.
    public const string SpawnRemoteVehicle = "spawn-remote-vehicle";
    public const string UpdateRemoteVehicle = "update-remote-vehicle";
    public const string DespawnRemoteVehicle = "despawn-remote-vehicle";
    public const string SpawnGhostVehicle = "spawn-ghost-vehicle";
    public const string UpdateGhostVehicle = "update-ghost-vehicle";
    public const string DespawnGhostVehicle = "despawn-ghost-vehicle";
    public const string AcquireRoleplayCharacter = "acquire-roleplay-character";
    public const string UpdateRoleplayCharacter = "update-roleplay-character";
    public const string ReleaseRoleplayCharacter = "release-roleplay-character";
    public const string TriggerRoleplayVehicle = "trigger-roleplay-vehicle";
    public const string CommandResult = "command-result";

    public const string CapabilityAdvancedTelemetry = "advanced-telemetry";
    public const string CapabilityGhostReplay = "ghost-replay";
    public const string CapabilityVehicleSpawn = "vehicle-spawn";
    public const string CapabilityVehicleTransform = "vehicle-transform";
    public const string CapabilityVehicleVisualState = "vehicle-visual-state";
    public const string CapabilityVehicleInterpolation = "vehicle-interpolation";
    public const string CapabilityTimetableState = "timetable-state";
    public const string CapabilityTrafficSync = "traffic-sync";
    public const string CapabilityCharacterPossession = "character-possession";
    public const string CapabilityCharacterTransform = "character-transform";
    public const string CapabilityCharacterInteraction = "character-interaction";
}

public sealed record PluginBridgeMessage(
    string Type,
    int ProtocolVersion,
    int? ProcessId = null,
    string? ComponentVersion = null,
    string? PlayerId = null,
    string? DisplayName = null,
    string? MapName = null,
    string? MapCompatibilityId = null,
    long? TimestampUnixMilliseconds = null,
    double? X = null,
    double? Y = null,
    double? Z = null,
    int? GridX = null,
    int? GridY = null,
    double? TileX = null,
    double? TileY = null,
    double? HeadingDegrees = null,
    double? SpeedKph = null,
    bool? IsInGame = null,
    long? SystemVariableCallbacks = null,
    int? RemoteVehicleCount = null,
    int? CompatibleRemoteVehicleCount = null,
    int? StaleRemovedCount = null,
    int? LastSystemVariableIndex = null,
    string? CommandId = null,
    string? VehicleInstanceId = null,
    string? VehiclePath = null,
    string? VehicleName = null,
    string? VehicleCompatibilityId = null,
    string? HofName = null,
    string? HofCompatibilityId = null,
    string? Line = null,
    string? Route = null,
    string? NextStopName = null,
    string? DestinationName = null,
    double? AccelerationMps2 = null,
    double? FuelPercent = null,
    double? ThrottlePercent = null,
    double? BrakePercent = null,
    double? SteeringDegrees = null,
    int? DelaySeconds = null,
    int? CurrentStopIndex = null,
    bool? StopRequested = null,
    int? DoorFlags = null,
    int? LightFlags = null,
    int? TurnSignal = null,
    bool? HornActive = null,
    bool? WipersActive = null,
    bool? ParkingBrakeActive = null,
    bool? ReverseGear = null,
    bool? ExperimentalWritesEnabled = null,
    bool? Success = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string[]? Capabilities = null,
    double? LocalX = null,
    double? LocalY = null,
    double? LocalZ = null,
    double? RotationX = null,
    double? RotationY = null,
    double? RotationZ = null,
    double? RotationW = null,
    string? CharacterInstanceId = null,
    int? CharacterHumanIndex = null,
    int? CharacterDefinitionPointer = null,
    string? CharacterActivity = null,
    double? SpeedMps = null,
    int? CharacterAiMode = null,
    int? CharacterAiModeEx = null,
    int? CharacterAiSubMode = null,
    double? CharacterSollSpeedMps = null,
    double? CharacterActSpeedMps = null,
    double? CharacterLastMovedDistanceMeters = null,
    double? CharacterAnimationState = null,
    int? CharacterActivityLegRaw = null,
    int? CharacterActivityArmUmbrellaRaw = null,
    int? CharacterActivityArmKiRaw = null,
    int? CharacterActivityHeadKiRaw = null,
    bool? CharacterActive = null,
    string? TriggerName = null,
    bool? TriggerActive = null,
    string? AuthorityPlayerId = null,
    long? Sequence = null,
    TrafficVehicleState[]? TrafficVehicles = null);
