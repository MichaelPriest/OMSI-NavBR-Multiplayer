using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

var identityReaderType = typeof(OmsiPluginBridgeServer).Assembly.GetType(
    "NavBR.Client.Telemetry.OmsiVehicleIdentityReader",
    throwOnError: true)!;
var normalizeVehiclePath = identityReaderType.GetMethod(
    "NormalizeVehiclePath",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException("vehicle identity normalizer not found");

var splitOmsiIdentity = (string?)normalizeVehiclePath.Invoke(
    null,
    [@"C:\\Program Files (x86)\\OMSI 2", "MAN_EN92_main.bus", @"Vehicles\\MAN_NL_NG"]);
Require(
    string.Equals(
        splitOmsiIdentity,
        @"Vehicles\\MAN_NL_NG\\MAN_EN92_main.bus",
        StringComparison.OrdinalIgnoreCase),
    "split OMSI MyPath + Obj vehicle identity was not normalized");

var completeOmsiIdentity = (string?)normalizeVehiclePath.Invoke(
    null,
    [@"C:\\Program Files (x86)\\OMSI 2", @"Vehicles\\MAN_NL_NG\\MAN_EN92_main.bus", @"Vehicles\\MAN_NL_NG"]);
Require(
    string.Equals(
        completeOmsiIdentity,
        @"Vehicles\\MAN_NL_NG\\MAN_EN92_main.bus",
        StringComparison.OrdinalIgnoreCase),
    "complete OMSI vehicle identity path regressed");

await using var server = new OmsiPluginBridgeServer();
server.Start();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await using var pipe = new NamedPipeClientStream(
    ".",
    PluginBridgeProtocol.PipeName,
    PipeDirection.InOut,
    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

await pipe.ConnectAsync(5_000, cts.Token);

using var reader = new StreamReader(
    pipe,
    new UTF8Encoding(false),
    detectEncodingFromByteOrderMarks: false,
    bufferSize: 4096,
    leaveOpen: true);
using var writer = new StreamWriter(
    pipe,
    new UTF8Encoding(false),
    bufferSize: 4096,
    leaveOpen: true)
{
    AutoFlush = true
};

var pluginHello = new PluginBridgeMessage(
    PluginBridgeProtocol.PluginHello,
    PluginBridgeProtocol.Version,
    ProcessId: 4242,
    ComponentVersion: "smoke-test");

await writer.WriteLineAsync(JsonSerializer.Serialize(pluginHello));

var helloLine = await reader.ReadLineAsync(cts.Token);
var clientHello = JsonSerializer.Deserialize<PluginBridgeMessage>(
    helloLine ?? throw new InvalidOperationException("client-hello not received"));

Require(clientHello is not null, "client-hello could not be parsed");
Require(clientHello!.Type == PluginBridgeProtocol.ClientHello, "unexpected client hello type");
Require(clientHello.ProtocolVersion == PluginBridgeProtocol.Version, "protocol version mismatch");

var pluginStatus = new PluginBridgeMessage(
    PluginBridgeProtocol.PluginStatus,
    PluginBridgeProtocol.Version,
    ProcessId: 4242,
    ComponentVersion: "smoke-test",
    TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    SpeedKph: 37.5,
    SystemVariableCallbacks: 123,
    RemoteVehicleCount: 2,
    CompatibleRemoteVehicleCount: 1,
    StaleRemovedCount: 3,
    LastSystemVariableIndex: 7,
    StopRequested: true,
    Capabilities:
    [
        PluginBridgeProtocol.CapabilityAdvancedTelemetry,
        PluginBridgeProtocol.CapabilityCharacterPossession,
        PluginBridgeProtocol.CapabilityCharacterTransform,
        PluginBridgeProtocol.CapabilityPhysicalMultiplayerV25
    ]);

await writer.WriteLineAsync(JsonSerializer.Serialize(pluginStatus));

OmsiPluginBridgeConnectionInfo? info = null;
for (var attempt = 0; attempt < 30; attempt++)
{
    info = server.GetConnectionInfo();
    if (info.LastStatus is not null)
    {
        break;
    }

    await Task.Delay(100, cts.Token);
}

Require(info is not null, "bridge info was not available");
Require(info!.IsConnected, "bridge did not report connected state");
Require(info.PluginProcessId == 4242, "plugin PID was not retained from handshake");
Require(info.PluginComponentVersion == "smoke-test", "plugin version was not retained");
Require(info.LastStatus is not null, "plugin-status was not stored");
Require(info.LastStatus!.SystemVariableCallbacks == 123, "callback count mismatch");
Require(info.LastStatus.RemoteVehicleCount == 2, "remote count mismatch");
Require(info.LastStatus.CompatibleRemoteVehicleCount == 1, "compatible remote count mismatch");
Require(info.LastStatus.StaleRemovedCount == 3, "stale count mismatch");
Require(info.LastStatus.LastSystemVariableIndex == 7, "system variable index mismatch");
Require(info.LastStatus.SpeedKph == 37.5, "plugin Velocity status mismatch");
Require(info.LastStatus.StopRequested == true, "stop request status mismatch");
Require(server.SupportsCapability(PluginBridgeProtocol.CapabilityCharacterPossession),
    "runtime character-possession capability was not refreshed from plugin status");
Require(server.SupportsCapability(PluginBridgeProtocol.CapabilityCharacterTransform),
    "runtime character-transform capability was not refreshed from plugin status");
Require(server.SupportsCapability(PluginBridgeProtocol.CapabilityPhysicalMultiplayerV25),
    "runtime physical-multiplayer-v25 capability was not refreshed from plugin status");

Require(
    PluginBridgeProtocol.IsRecoverablePhysicalMotionError(
        PluginBridgeProtocol.ErrorMotionReadbackUnavailable),
    "motion readback unavailable must remain recoverable");
Require(
    PluginBridgeProtocol.IsRecoverablePhysicalMotionError(
        PluginBridgeProtocol.ErrorMotionTransformMismatch),
    "motion transform mismatch must remain recoverable");
Require(
    PluginBridgeProtocol.IsRecoverablePhysicalMotionError(
        PluginBridgeProtocol.ErrorMotionTileMismatch),
    "motion tile mismatch must remain recoverable");
Require(
    PluginBridgeProtocol.IsRecoverablePhysicalMotionError(
        PluginBridgeProtocol.ErrorMotionWorldOriginUnavailable),
    "Kachel world-origin wait must remain recoverable");
Require(
    !PluginBridgeProtocol.IsRecoverablePhysicalMotionError("vehicle-not-owned"),
    "ownership failures must not be classified as recoverable motion drift");

var motionVectorMessage = new PluginBridgeMessage(
    PluginBridgeProtocol.UpdateRemoteVehicle,
    PluginBridgeProtocol.Version,
    PhysicalGridX: 17,
    PhysicalGridY: -23,
    VelocityX: 1.25,
    VelocityY: -0.5,
    VelocityZ: 3.75,
    AccelerationLocalX: 0.1,
    AccelerationLocalY: 0.2,
    AccelerationLocalZ: -0.3);
var motionVectorJson = JsonSerializer.Serialize(motionVectorMessage);
var motionVectorRoundTrip =
    JsonSerializer.Deserialize<PluginBridgeMessage>(motionVectorJson);
Require(motionVectorRoundTrip?.PhysicalGridX == 17, "physical grid X did not round-trip");
Require(motionVectorRoundTrip?.PhysicalGridY == -23, "physical grid Y did not round-trip");
Require(motionVectorRoundTrip?.VelocityX == 1.25, "velocity X did not round-trip");
Require(motionVectorRoundTrip?.VelocityY == -0.5, "velocity Y did not round-trip");
Require(motionVectorRoundTrip?.VelocityZ == 3.75, "velocity Z did not round-trip");
Require(motionVectorRoundTrip?.AccelerationLocalX == 0.1, "Acc_Local X did not round-trip");
Require(motionVectorRoundTrip?.AccelerationLocalY == 0.2, "Acc_Local Y did not round-trip");
Require(motionVectorRoundTrip?.AccelerationLocalZ == -0.3, "Acc_Local Z did not round-trip");

var spoofedStatus = pluginStatus with
{
    ProcessId = 9999,
    TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    SystemVariableCallbacks = 999
};
await writer.WriteLineAsync(JsonSerializer.Serialize(spoofedStatus));
await Task.Delay(250, cts.Token);

var afterSpoof = server.GetConnectionInfo();
Require(afterSpoof.LastStatus?.SystemVariableCallbacks == 123,
    "runtime status from a PID different from the handshake was accepted");

var invalidCountersStatus = pluginStatus with
{
    TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    SystemVariableCallbacks = 777,
    RemoteVehicleCount = -1
};
await writer.WriteLineAsync(JsonSerializer.Serialize(invalidCountersStatus));
await Task.Delay(250, cts.Token);

var afterInvalidCounters = server.GetConnectionInfo();
Require(afterInvalidCounters.LastStatus?.SystemVariableCallbacks == 123,
    "runtime status with invalid counters was accepted");

var trafficVehicle = new TrafficVehicleState(
    "traffic-17",
    "Vehicles\\MAN_NL_NG\\MAN_NL263.bus",
    "sha256:test-vehicle",
    1234.5,
    2345.6,
    12.3,
    34.5,
    45.6,
    12.3,
    0,
    0,
    0,
    1,
    37.5,
    LightFlags: 3,
    TurnSignal: 1);

var trafficMessage = new PluginBridgeMessage(
    PluginBridgeProtocol.TrafficSnapshotState,
    PluginBridgeProtocol.Version,
    MapName: "Berlin-Spandau",
    MapCompatibilityId: "sha256:test-map",
    TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
    AuthorityPlayerId: "host-player",
    Sequence: 42,
    TrafficVehicles: [trafficVehicle]);

var trafficRead = reader.ReadLineAsync(cts.Token).AsTask();
await server.SendMessageAsync(trafficMessage, cts.Token);
var trafficLine = await trafficRead;
var receivedTraffic = JsonSerializer.Deserialize<PluginBridgeMessage>(
    trafficLine ?? throw new InvalidOperationException("traffic snapshot not received"));

Require(receivedTraffic?.Type == PluginBridgeProtocol.TrafficSnapshotState,
    "unexpected traffic message type");
Require(receivedTraffic?.AuthorityPlayerId == "host-player", "traffic authority mismatch");
Require(receivedTraffic?.Sequence == 42, "traffic sequence mismatch");
Require(receivedTraffic?.TrafficVehicles?.Length == 1, "traffic vehicle count mismatch");
Require(receivedTraffic?.TrafficVehicles?[0].TrafficId == "traffic-17", "traffic vehicle id mismatch");

var clearTraffic = new PluginBridgeMessage(
    PluginBridgeProtocol.ClearTrafficVehicles,
    PluginBridgeProtocol.Version);
var clearRead = reader.ReadLineAsync(cts.Token).AsTask();
await server.SendMessageAsync(clearTraffic, cts.Token);
var clearLine = await clearRead;
var receivedClear = JsonSerializer.Deserialize<PluginBridgeMessage>(
    clearLine ?? throw new InvalidOperationException("traffic clear not received"));
Require(receivedClear?.Type == PluginBridgeProtocol.ClearTrafficVehicles,
    "traffic clear message type mismatch");

Console.WriteLine("Plugin bridge v3 smoke test passed: handshake + runtime capability refresh + status + traffic delivery + rejection checks.");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
