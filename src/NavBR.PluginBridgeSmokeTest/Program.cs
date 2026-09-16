using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

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
    SystemVariableCallbacks: 123,
    RemoteVehicleCount: 2,
    CompatibleRemoteVehicleCount: 1,
    StaleRemovedCount: 3,
    LastSystemVariableIndex: 7);

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

Console.WriteLine("Plugin bridge smoke test passed: handshake + runtime status + traffic delivery + rejection checks.");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
