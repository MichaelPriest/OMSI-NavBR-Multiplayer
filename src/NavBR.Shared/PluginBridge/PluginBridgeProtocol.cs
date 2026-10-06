using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

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
    public const string SetPerformanceProfile = "set-performance-profile";
    public const string SetOpenOmsiHudConfiguration = "set-openomsi-hud-configuration";
    public const string SetOpenOmsiRoutePolyline = "set-openomsi-route-polyline";

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
    public const string TriggerLocalVehicle = "trigger-local-vehicle";
    public const string CommandResult = "command-result";

    public const string CapabilityAdvancedTelemetry = "advanced-telemetry";
    public const string CapabilityGhostReplay = "ghost-replay";
    public const string CapabilityVehicleSpawn = "vehicle-spawn";
    public const string CapabilityVehicleTransform = "vehicle-transform";
    public const string CapabilityVehicleVisualState = "vehicle-visual-state";
    public const string CapabilityVehicleInterpolation = "vehicle-interpolation";
    public const string CapabilityVehicleTileSync = "vehicle-tile-sync";
    // Capability marker for the physical-grid/world-pose multiplayer path
    // introduced with state interop 25. Requiring this on the desktop makes a
    // stale OMSI-loaded plugin fail closed instead of pretending that the
    // legacy vehicle-spawn/transform implementation is compatible.
    public const string CapabilityPhysicalMultiplayerV25 = "physical-multiplayer-v25";
    public const string CapabilityTimetableState = "timetable-state";
    public const string CapabilityTrafficSync = "traffic-sync";
    public const string CapabilityCharacterPossession = "character-possession";
    public const string CapabilityCharacterTransform = "character-transform";
    public const string CapabilityCharacterInteraction = "character-interaction";
    public const string CapabilityLocalVehicleTrigger = "local-vehicle-trigger";
    public const string CapabilityPerformanceGovernor = "performance-governor";
    public const string CapabilityOpenOmsiStandardPlugin = "openomsi-standard-plugin";
    public const string CapabilityOpenOmsiLuaSnapshot = "openomsi-lua-snapshot";
    public const string CapabilityOpenOmsiNearbyVehicles = "openomsi-nearby-vehicles";
    public const string CapabilityOpenOmsiTimetableContext = "openomsi-timetable-context";
    public const string CapabilityOpenOmsiNativeOnFoot = "openomsi-native-on-foot";
    public const string CapabilityOpenOmsiNavigationRuntime = "openomsi-navigation-runtime";
    public const string CapabilityOpenOmsiHudConfiguration = "openomsi-hud-configuration";
    public const string CapabilityOpenOmsiRouteRejoin = "openomsi-route-rejoin";
    public const string CapabilityOpenOmsiTimetableResolver = "openomsi-timetable-resolver";
    public const string CapabilityOpenOmsiRouteSteps = "openomsi-route-steps";
    public const string CapabilityOpenOmsiAutomaticRouteGeometry = "openomsi-automatic-route-geometry";
    public const string CapabilityOpenOmsiTurnGuidance = "openomsi-turn-guidance";
    public const string CapabilityOpenOmsiMiniMapRuntime = "openomsi-minimap-runtime";
    public const string CapabilityOpenOmsiGuidanceWaypoints = "openomsi-guidance-waypoints";
    public const string CapabilityOpenOmsiMapVisualState = "openomsi-map-visual-state";
    public const string CapabilityOpenOmsiMapPresentation = "openomsi-map-presentation";
    public const string CapabilityOpenOmsiTeleMatrixRuntime = "openomsi-telematrix-runtime";
    public const string CapabilityOpenOmsiCompactHud = "openomsi-compact-hud";
    public const string CapabilityOpenOmsiGroundArrows = "openomsi-ground-arrows";

    public const string ErrorMotionReadbackUnavailable = "motion-readback-unavailable";
    public const string ErrorMotionTransformMismatch = "motion-transform-mismatch";
    public const string ErrorMotionTileMismatch = "motion-tile-mismatch";
    public const string ErrorMotionWorldOriginUnavailable = "motion-world-origin-unavailable";

    public static bool IsRecoverablePhysicalMotionError(string? errorCode) =>
        string.Equals(errorCode, ErrorMotionReadbackUnavailable, StringComparison.Ordinal) ||
        string.Equals(errorCode, ErrorMotionTransformMismatch, StringComparison.Ordinal) ||
        string.Equals(errorCode, ErrorMotionTileMismatch, StringComparison.Ordinal) ||
        string.Equals(errorCode, ErrorMotionWorldOriginUnavailable, StringComparison.Ordinal);
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
    double? CabinTemperatureC = null,
    int? PassengerCount = null,
    bool? ScheduleActive = null,
    double? SimulationTime = null,
    int? SimulationDay = null,
    int? SimulationMonth = null,
    int? SimulationYear = null,
    bool? SimulationPaused = null,
    string? IbisLineCourse = null,
    string? IbisRouteCode = null,
    string? IbisTerminusName = null,
    string? IbisDelayMinutes = null,
    string? IbisDelaySeconds = null,
    string? IbisDelayState = null,
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
    TrafficVehicleState[]? TrafficVehicles = null,
    int? MapTileIndex = null,
    int? PluginPressureLevel = null,
    double? PluginWorkMilliseconds = null,
    double? PluginAverageWorkMilliseconds = null,
    double? PluginAverageFrameIntervalMilliseconds = null,
    long? PluginMinimumWorkIntervalMilliseconds = null,
    int? PluginMaxCommandsPerSlice = null,
    double? PluginLastFrameIntervalMilliseconds = null,
    double? PluginPeakFrameIntervalMilliseconds = null,
    long? PluginFrameStallCount = null,
    string? PerformanceProfile = null,
    int? PhysicalGridX = null,
    int? PhysicalGridY = null,
    double? VelocityX = null,
    double? VelocityY = null,
    double? VelocityZ = null,
    double? AccelerationLocalX = null,
    double? AccelerationLocalY = null,
    double? AccelerationLocalZ = null,
    VehicleSectionPose[]? RearSections = null,
    string? OpenOmsiView = null,
    bool? OpenOmsiOnFoot = null,
    bool? OpenOmsiMultiplayer = null,
    int? OpenOmsiTrafficCount = null,
    int? OpenOmsiNearbyAiCount = null,
    int? OpenOmsiNearbyPlayerCount = null,
    double? OpenOmsiNextStopArrival = null,
    double? OpenOmsiNextStopDeparture = null,
    int? OpenOmsiTripIndex = null,
    int? OpenOmsiTripsCount = null,
    int? OpenOmsiNextStopNumber = null,
    OpenOmsiNearbyVehicleState[]? OpenOmsiNearbyVehicles = null,
    double? OpenOmsiSuggestedMapRadiusMeters = null,
    string? OpenOmsiCongestionLevel = null,
    double? OpenOmsiAverageNearbyTrafficSpeedKph = null,
    int? OpenOmsiNearbyMovingAiCount = null,
    int? OpenOmsiNearbySlowAiCount = null,
    int? OpenOmsiNearbyStoppedAiCount = null,
    bool? OpenOmsiMiniMapEnabled = null,
    bool? OpenOmsiFullMapEnabled = null,
    bool? OpenOmsiAutoZoomEnabled = null,
    bool? OpenOmsiFollowVehicleEnabled = null,
    bool? OpenOmsiTimetableHudEnabled = null,
    bool? OpenOmsiTeleMatrixEnabled = null,
    bool? OpenOmsiTrafficLayerEnabled = null,
    bool? OpenOmsiMultiplayerLayerEnabled = null,
    bool? OpenOmsiCongestionLayerEnabled = null,
    bool? OpenOmsiRouteGuidanceEnabled = null,
    OpenOmsiRoutePoint[]? OpenOmsiRoutePoints = null,
    bool? OpenOmsiRouteLoaded = null,
    string? OpenOmsiRouteKey = null,
    int? OpenOmsiRoutePointCount = null,
    double? OpenOmsiDistanceFromRouteMeters = null,
    bool? OpenOmsiOffRoute = null,
    int? OpenOmsiNearestRoutePointIndex = null,
    int? OpenOmsiRejoinRoutePointIndex = null,
    double? OpenOmsiRejoinTargetX = null,
    double? OpenOmsiRejoinTargetY = null,
    bool? OpenOmsiContentRootAvailable = null,
    bool? OpenOmsiMapContentAvailable = null,
    bool? OpenOmsiTimetableDataAvailable = null,
    string? OpenOmsiResolvedTripName = null,
    string? OpenOmsiResolvedTripTerminus = null,
    int? OpenOmsiResolvedProfileIndex = null,
    double? OpenOmsiResolvedDepartureMinutes = null,
    string[]? OpenOmsiResolvedStops = null,
    OpenOmsiRouteStepState[]? OpenOmsiRouteSteps = null,
    bool? OpenOmsiAutomaticRouteGeometryAvailable = null,
    int? OpenOmsiAutomaticRoutePointCount = null,
    bool? OpenOmsiGuidanceAvailable = null,
    string? OpenOmsiNextManeuver = null,
    double? OpenOmsiNextTurnAngleDegrees = null,
    double? OpenOmsiDistanceToManeuverMeters = null,
    double? OpenOmsiManeuverTargetX = null,
    double? OpenOmsiManeuverTargetY = null,
    bool? OpenOmsiMiniMapRuntimeAvailable = null,
    double? OpenOmsiRouteLengthMeters = null,
    double? OpenOmsiRouteProgressMeters = null,
    double? OpenOmsiRouteRemainingMeters = null,
    double? OpenOmsiRouteProgressPercent = null,
    OpenOmsiGuidanceWaypointState[]? OpenOmsiGuidanceWaypoints = null,
    bool? OpenOmsiMapVisualAvailable = null,
    OpenOmsiRoutePoint[]? OpenOmsiTraveledRoute = null,
    OpenOmsiRoutePoint[]? OpenOmsiForwardRoute = null,
    OpenOmsiRoutePoint[]? OpenOmsiRejoinRoute = null,
    int? OpenOmsiCurrentRoutePointIndex = null,
    bool? OpenOmsiMapPresentationAvailable = null,
    double? OpenOmsiMapCenterX = null,
    double? OpenOmsiMapCenterY = null,
    double? OpenOmsiMapRotationDegrees = null,
    double? OpenOmsiMapRadiusMeters = null,
    string? OpenOmsiMapOrientationMode = null,
    OpenOmsiMapMarkerState[]? OpenOmsiMapMarkers = null,
    bool? OpenOmsiTeleMatrixRuntimeAvailable = null,
    string? OpenOmsiTeleMatrixLine = null,
    string? OpenOmsiTeleMatrixDestination = null,
    string? OpenOmsiTeleMatrixNextStop = null,
    int? OpenOmsiTeleMatrixStopNumber = null,
    int? OpenOmsiTeleMatrixStopCount = null,
    int? OpenOmsiTeleMatrixDelaySeconds = null,
    double? OpenOmsiTeleMatrixNextArrivalSeconds = null,
    double? OpenOmsiTeleMatrixNextDepartureSeconds = null,
    string? OpenOmsiTeleMatrixPunctualityState = null,
    bool? OpenOmsiCompactHudAvailable = null,
    string? OpenOmsiCompactHudPrimaryText = null,
    string? OpenOmsiCompactHudSecondaryText = null,
    string? OpenOmsiCompactHudManeuver = null,
    string? OpenOmsiCompactHudManeuverIcon = null,
    double? OpenOmsiCompactHudDistanceMeters = null,
    double? OpenOmsiCompactHudRouteRemainingMeters = null,
    bool? OpenOmsiCompactHudOffRoute = null,
    OpenOmsiGroundArrowState[]? OpenOmsiGroundArrows = null);

public sealed record OpenOmsiNearbyVehicleState(
    string Id,
    string Kind,
    string? Name,
    double X,
    double Y,
    double Z,
    double HeadingDegrees,
    double? SpeedKph = null);

public sealed record OpenOmsiRoutePoint(
    double X,
    double Y,
    double? Z = null,
    string? StopName = null);

public sealed record OpenOmsiRouteStepState(
    int Leg,
    int TileIndex,
    long ObjectId,
    int PathIndex,
    double LengthMeters,
    bool IsTrack);

public sealed record OpenOmsiGuidanceWaypointState(
    double X,
    double Y,
    double? Z,
    double DistanceAheadMeters,
    double HeadingDegrees);

public sealed record OpenOmsiMapMarkerState(
    string Id,
    string Kind,
    string? Label,
    double X,
    double Y,
    double Z,
    double HeadingDegrees,
    double? SpeedKph = null);

public sealed record OpenOmsiGroundArrowState(
    double X,
    double Y,
    double? Z,
    double HeadingDegrees,
    double DistanceAheadMeters,
    string Kind);
