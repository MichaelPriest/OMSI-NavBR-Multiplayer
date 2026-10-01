using NavBR.Server.Multiplayer;
using NavBR.Shared.Multiplayer;

var registry = new MultiplayerRoomRegistry();

var initialManifest = new OmsiCompatibilityManifest(
    OmsiVersion: "2.3.004",
    NavBRVersion: "smoke",
    MapName: "Grundorf",
    MapCompatibilityId: "map-sha",
    VehiclePath: null,
    VehicleCompatibilityId: null,
    HofName: null,
    HofCompatibilityId: null,
    PluginProtocolVersion: 3,
    PluginDeployment: "OPENOMSI-X64",
    Capabilities: ["telemetry"]);

var initial = registry.Upsert(
    connectionId: "conn-a",
    roomId: "room-a",
    playerId: "player-a",
    displayName: "Driver A",
    mapName: "Grundorf",
    mapCompatibilityId: "map-sha",
    compatibility: initialManifest);

Require(initial.Compatibility?.VehiclePath is null, "Initial vehicle path should be empty.");

var resolved = registry.UpdateTelemetryIdentity(
    "conn-a",
    "Grundorf",
    "map-sha",
    @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
    "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "Grundorf.hof",
    "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

Require(resolved is not null, "Resolved live identity did not update presence.");
Require(
    resolved!.Compatibility?.VehiclePath ==
    @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
    "Resolved vehicle path was not stored.");
Require(
    resolved.Compatibility?.VehicleCompatibilityId?.StartsWith("sha256:", StringComparison.Ordinal) == true,
    "Resolved vehicle SHA was not stored.");
Require(
    resolved.Compatibility?.HofName == "Grundorf.hof",
    "Resolved HOF name was not stored.");

var duplicate = registry.UpdateTelemetryIdentity(
    "conn-a",
    "Grundorf",
    "map-sha",
    @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
    "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "Grundorf.hof",
    "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

Require(
    duplicate is null,
    "Identical 20 Hz telemetry would cause playerPresenceChanged churn.");

var partial = registry.UpdateTelemetryIdentity(
    "conn-a",
    "Grundorf",
    "map-sha",
    vehiclePath: null,
    vehicleCompatibilityId: null,
    hofName: null,
    hofCompatibilityId: null);

Require(
    partial is null,
    "Partial telemetry should preserve the current live identity without presence churn.");
Require(
    registry.TryGet("conn-a", out var afterPartial) &&
    afterPartial?.Compatibility?.VehicleCompatibilityId ==
    "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "Partial telemetry erased the valid vehicle SHA.");

var switched = registry.UpdateTelemetryIdentity(
    "conn-a",
    "Grundorf",
    "map-sha",
    @"Vehicles\MAN_NL_NG\MAN_NL202.bus",
    vehicleCompatibilityId: null,
    hofName: "Grundorf.hof",
    hofCompatibilityId: null);

Require(switched is not null, "Vehicle switch did not update presence.");
Require(
    switched!.Compatibility?.VehiclePath ==
    @"Vehicles\MAN_NL_NG\MAN_NL202.bus",
    "New vehicle path was not stored.");
Require(
    switched.Compatibility?.VehicleCompatibilityId is null,
    "Old vehicle SHA survived a real vehicle-path change.");
Require(
    switched.Compatibility?.HofCompatibilityId ==
    "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
    "Stable HOF identity was erased during vehicle change.");

var switchedResolved = registry.UpdateTelemetryIdentity(
    "conn-a",
    "Grundorf",
    "map-sha",
    @"Vehicles\MAN_NL_NG\MAN_NL202.bus",
    "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
    hofName: null,
    hofCompatibilityId: null);

Require(switchedResolved is not null, "New vehicle SHA did not refresh presence.");
Require(
    switchedResolved!.Compatibility?.VehicleCompatibilityId ==
    "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
    "New vehicle SHA was not stored.");

Console.WriteLine(
    "Server live-presence smoke passed: resolve -> dedupe -> partial preserve -> vehicle switch invalidates old SHA -> new SHA.");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
