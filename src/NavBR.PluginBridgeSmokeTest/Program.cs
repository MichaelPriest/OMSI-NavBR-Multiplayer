using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
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
    [@"C:\Program Files (x86)\OMSI 2", "MAN_EN92_main.bus", null, @"Vehicles\MAN_NL_NG"]);
Require(
    string.Equals(
        splitOmsiIdentity,
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        StringComparison.OrdinalIgnoreCase),
    "split OMSI MyPath + Obj vehicle identity was not normalized");

var roadVehicleIdentity = (string?)normalizeVehiclePath.Invoke(
    null,
    [@"C:\Program Files (x86)\OMSI 2", null, "MAN_EN92_main.bus", @"Vehicles\MAN_NL_NG"]);
Require(
    string.Equals(
        roadVehicleIdentity,
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        StringComparison.OrdinalIgnoreCase),
    "RoadVehicle.FileName + MyPath vehicle identity was not normalized");

var completeOmsiIdentity = (string?)normalizeVehiclePath.Invoke(
    null,
    [@"C:\Program Files (x86)\OMSI 2", null, @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus", @"Vehicles\MAN_NL_NG"]);
Require(
    string.Equals(
        completeOmsiIdentity,
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        StringComparison.OrdinalIgnoreCase),
    "complete OMSI vehicle identity path regressed");

var fingerprintVehicle = identityReaderType.GetMethod(
    "TryFingerprintInstalledVehicle",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException("installed vehicle fingerprint helper not found");
var fingerprintRoot = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-fingerprint-" + Guid.NewGuid().ToString("N"));
var fingerprintRelative = Path.Combine(
    "Vehicles",
    "MAN_NL_NG",
    "MAN_EN92_main.bus");
var fingerprintPath = Path.Combine(
    fingerprintRoot,
    fingerprintRelative);
Directory.CreateDirectory(Path.GetDirectoryName(fingerprintPath)!);
var fingerprintBytes = Encoding.UTF8.GetBytes(
    "[friendlyname]\r\nNavBR openOMSI fingerprint smoke\r\n");
File.WriteAllBytes(fingerprintPath, fingerprintBytes);
try
{
    var expectedFingerprint =
        "sha256:" +
        Convert.ToHexString(
            SHA256.HashData(fingerprintBytes))
        .ToLowerInvariant();
    var actualFingerprint = (string?)fingerprintVehicle.Invoke(
        null,
        [fingerprintRoot, fingerprintRelative.Replace('\\', '/')]);
    Require(
        string.Equals(
            actualFingerprint,
            expectedFingerprint,
            StringComparison.Ordinal),
        "openOMSI installed vehicle SHA-256 fingerprint regressed");

    var escapedFingerprint = (string?)fingerprintVehicle.Invoke(
        null,
        [fingerprintRoot, @"..\outside.bus"]);
    Require(
        escapedFingerprint is null,
        "vehicle fingerprint helper accepted a path outside the content root");
}
finally
{
    Directory.Delete(fingerprintRoot, recursive: true);
}

var openOmsiInstallerType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.PluginInstaller.OpenOmsiPluginInstallationService",
        throwOnError: true)!;
var resolveInstalledVehicle =
    openOmsiInstallerType.GetMethod(
        "ResolveInstalledVehicleFile",
        BindingFlags.Public |
        BindingFlags.Static)
    ?? throw new InvalidOperationException(
        "openOMSI installed vehicle resolver not found");
var assetRoot = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-asset-" +
    Guid.NewGuid().ToString("N"));
var assetRelative = Path.Combine(
    "Vehicles",
    "NavBR_Smoke",
    "Smoke.bus");
var assetPath = Path.Combine(
    assetRoot,
    assetRelative);
Directory.CreateDirectory(
    Path.GetDirectoryName(assetPath)!);
File.WriteAllText(
    assetPath,
    "[friendlyname]\r\nNavBR Asset Smoke\r\n");
var previousOmsiRoot =
    Environment.GetEnvironmentVariable("OMSI_ROOT");
try
{
    Environment.SetEnvironmentVariable(
        "OMSI_ROOT",
        assetRoot);

    var resolvedAsset = (string?)resolveInstalledVehicle.Invoke(
        null,
        [
            assetRelative.Replace('\\', '/'),
            null
        ]);
    Require(
        string.Equals(
            Path.GetFullPath(resolvedAsset ?? string.Empty),
            Path.GetFullPath(assetPath),
            StringComparison.OrdinalIgnoreCase),
        "openOMSI content-root vehicle resolver did not find an installed .bus.");

    var escapedAsset = (string?)resolveInstalledVehicle.Invoke(
        null,
        [@"..\outside.bus", null]);
    Require(
        escapedAsset is null,
        "openOMSI content-root vehicle resolver accepted path traversal.");

    var invalidAsset = (string?)resolveInstalledVehicle.Invoke(
        null,
        [@"Vehicles\NavBR_Smoke\Smoke.cfg", null]);
    Require(
        invalidAsset is null,
        "openOMSI content-root vehicle resolver accepted a non-vehicle extension.");
}
finally
{
    Environment.SetEnvironmentVariable(
        "OMSI_ROOT",
        previousOmsiRoot);
    Directory.Delete(
        assetRoot,
        recursive: true);
}

var multiRootResolverType =
    typeof(OmsiPluginBridgeServer).Assembly.GetType(
        "NavBR.Client.Multiplayer.OpenOmsiRemoteVehicleAssetResolver",
        throwOnError: true)!;
var multiRootResolve = multiRootResolverType.GetMethod(
    "ResolveAsync",
    BindingFlags.Public | BindingFlags.Instance)
    ?? throw new InvalidOperationException(
        "openOMSI multi-root SHA vehicle resolver not found");
var multiRootA = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-multi-a-" +
    Guid.NewGuid().ToString("N"));
var multiRootB = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-multi-b-" +
    Guid.NewGuid().ToString("N"));
var reportedRelative =
    Path.Combine("Vehicles", "RemotePack", "Remote.bus");
var relocatedRelative =
    Path.Combine("Vehicles", "LocalPack", "RenamedRemote.bus");
var wrongPath = Path.Combine(
    multiRootA,
    reportedRelative);
var correctPath = Path.Combine(
    multiRootB,
    relocatedRelative);
Directory.CreateDirectory(
    Path.GetDirectoryName(wrongPath)!);
Directory.CreateDirectory(
    Path.GetDirectoryName(correctPath)!);
var wrongBytes = Encoding.UTF8.GetBytes(
    "[friendlyname]\r\nWrong local content\r\n");
var correctBytes = Encoding.UTF8.GetBytes(
    "[friendlyname]\r\nSame remote content under another path\r\n");
File.WriteAllBytes(wrongPath, wrongBytes);
File.WriteAllBytes(correctPath, correctBytes);
try
{
    var expectedRemoteFingerprint =
        "sha256:" +
        Convert.ToHexString(
            SHA256.HashData(correctBytes))
        .ToLowerInvariant();
    Func<IReadOnlyList<string>> rootsSource =
        () => new[] { multiRootA, multiRootB };
    var resolver = Activator.CreateInstance(
        multiRootResolverType,
        rootsSource)
        ?? throw new InvalidOperationException(
            "openOMSI multi-root SHA resolver could not be created");

    var resolveTask = multiRootResolve.Invoke(
        resolver,
        [
            reportedRelative,
            expectedRemoteFingerprint,
            CancellationToken.None
        ]) as Task<string?>
        ?? throw new InvalidOperationException(
            "openOMSI multi-root SHA resolver did not return Task<string?>");
    var resolvedRemotePath = await resolveTask;
    Require(
        string.Equals(
            resolvedRemotePath,
            relocatedRelative.Replace('/', '\\'),
            StringComparison.OrdinalIgnoreCase),
        $"openOMSI SHA resolver did not relocate the remote vehicle. Got '{resolvedRemotePath}'.");

    var mismatchedFingerprint =
        "sha256:" + new string('0', 64);
    var mismatchTask = multiRootResolve.Invoke(
        resolver,
        [
            reportedRelative,
            mismatchedFingerprint,
            CancellationToken.None
        ]) as Task<string?>
        ?? throw new InvalidOperationException(
            "openOMSI multi-root mismatch resolver did not return Task<string?>");
    Require(
        await mismatchTask is null,
        "openOMSI SHA resolver accepted a locally different vehicle definition");
}
finally
{
    Directory.Delete(multiRootA, recursive: true);
    Directory.Delete(multiRootB, recursive: true);
}

var mapCatalogType =
    typeof(NavBR.Client.Maps.OmsiMapCatalog);
var fingerprintMap = mapCatalogType
    .GetMethods(
        BindingFlags.NonPublic |
        BindingFlags.Static)
    .SingleOrDefault(method =>
        method.Name ==
            "TryFingerprintInstalledMap" &&
        method.GetParameters() is var parameters &&
        parameters.Length == 2 &&
        parameters[0].ParameterType ==
            typeof(string))
    ?? throw new InvalidOperationException(
        "single-root installed map fingerprint helper not found");
var fingerprintMergedMap = mapCatalogType
    .GetMethods(
        BindingFlags.NonPublic |
        BindingFlags.Static)
    .SingleOrDefault(method =>
        method.Name ==
            "TryFingerprintInstalledMap" &&
        method.GetParameters() is var parameters &&
        parameters.Length == 2 &&
        parameters[0].ParameterType ==
            typeof(IReadOnlyList<string>))
    ?? throw new InvalidOperationException(
        "multi-root installed map fingerprint helper not found");
var mapRoot = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-map-fingerprint-" +
    Guid.NewGuid().ToString("N"));
var mapDirectory = Path.Combine(
    mapRoot,
    "maps",
    "Grundorf");
Directory.CreateDirectory(mapDirectory);
var globalCfgPath =
    Path.Combine(mapDirectory, "global.cfg");
var tileAPath =
    Path.Combine(mapDirectory, "tile_-1_0.map");
var tileBPath =
    Path.Combine(mapDirectory, "tile_0_0.map");
var globalCfgBytes = Encoding.UTF8.GetBytes(
    "[name]\r\nGrundorf\r\n[friendlyname]\r\nGrundorf Smoke\r\n");
File.WriteAllBytes(globalCfgPath, globalCfgBytes);
File.WriteAllBytes(tileAPath, new byte[17]);
File.WriteAllBytes(tileBPath, new byte[29]);
try
{
    using var mapHash =
        IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
    mapHash.AppendData(globalCfgBytes);
    foreach (var tilePath in new[]
             {
                 tileAPath,
                 tileBPath
             }
             .OrderBy(
                 path => Path.GetFileName(path),
                 StringComparer.OrdinalIgnoreCase))
    {
        var name =
            Path.GetFileName(tilePath)
                .ToUpperInvariant();
        var length =
            new FileInfo(tilePath).Length;
        mapHash.AppendData(
            Encoding.UTF8.GetBytes(
                $"\n{name}:{length}"));
    }

    var expectedMapFingerprint =
        Convert.ToHexString(
            mapHash.GetHashAndReset())
        .ToLowerInvariant();

    var actualMapFingerprint = (string?)fingerprintMap.Invoke(
        null,
        [mapRoot, "maps/Grundorf/global.cfg"]);
    Require(
        string.Equals(
            actualMapFingerprint,
            expectedMapFingerprint,
            StringComparison.Ordinal),
        "openOMSI installed map fingerprint regressed");

    var mapByFolderFingerprint = (string?)fingerprintMap.Invoke(
        null,
        [mapRoot, "Grundorf"]);
    Require(
        string.Equals(
            mapByFolderFingerprint,
            expectedMapFingerprint,
            StringComparison.Ordinal),
        "openOMSI map folder fingerprint did not match raw LAN map path");

    var escapedMapFingerprint = (string?)fingerprintMap.Invoke(
        null,
        [mapRoot, @"..\outside"]);
    Require(
        escapedMapFingerprint is null,
        "map fingerprint helper accepted a path outside the content root");

    var overlayRoot = Path.Combine(
        Path.GetTempPath(),
        "NavBR-openOMSI-map-overlay-" +
        Guid.NewGuid().ToString("N"));
    var overlayDirectory = Path.Combine(
        overlayRoot,
        "maps",
        "Grundorf");
    Directory.CreateDirectory(overlayDirectory);
    var overlayTileB =
        Path.Combine(
            overlayDirectory,
            "tile_0_0.map");
    File.WriteAllBytes(
        overlayTileB,
        new byte[41]);
    try
    {
        using var mergedHash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);
        mergedHash.AppendData(globalCfgBytes);
        foreach (var item in new[]
                 {
                     (
                         Name: "tile_-1_0.map",
                         Length: 17L),
                     (
                         Name: "tile_0_0.map",
                         Length: 41L)
                 }
                 .OrderBy(
                     item => item.Name,
                     StringComparer.OrdinalIgnoreCase))
        {
            mergedHash.AppendData(
                Encoding.UTF8.GetBytes(
                    $"\n{item.Name.ToUpperInvariant()}:{item.Length}"));
        }

        var expectedMergedMapFingerprint =
            Convert.ToHexString(
                mergedHash.GetHashAndReset())
            .ToLowerInvariant();

        var actualMergedMapFingerprint =
            (string?)fingerprintMergedMap.Invoke(
                null,
                [
                    new[]
                    {
                        overlayRoot,
                        mapRoot
                    },
                    "maps/Grundorf/global.cfg"
                ]);
        Require(
            string.Equals(
                actualMergedMapFingerprint,
                expectedMergedMapFingerprint,
                StringComparison.Ordinal),
            "openOMSI merged content-root map fingerprint did not honor overlay priority");
    }
    finally
    {
        Directory.Delete(
            overlayRoot,
            recursive: true);
    }
}
finally
{
    Directory.Delete(mapRoot, recursive: true);
}

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
