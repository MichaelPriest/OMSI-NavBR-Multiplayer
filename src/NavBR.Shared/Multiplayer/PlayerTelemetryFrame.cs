using NavBR.Shared.Telemetry;

namespace NavBR.Shared.Multiplayer;

public sealed record PlayerTelemetryFrame(
    PlayerPresence Player,
    VehicleTelemetry Telemetry);
