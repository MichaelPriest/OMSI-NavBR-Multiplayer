using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NavBR.Client.Multiplayer;
using NavBR.Server;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.OpenOmsi;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

const string mapName = "Grundorf";
const string mapCompatibilityId =
    "sha256:1111111111111111111111111111111111111111111111111111111111111111";
const string roomId = "ci-openomsi-signalr-physical";
const string senderPlayerId = "ci-omsi2-sender";
const string receiverPlayerId = "ci-openomsi-receiver";
const string reportedVehiclePath =
    @"Vehicles\RemotePack\Remote.bus";
const string localVehiclePath =
    @"Vehicles\LocalPack\ResolvedRemote.bus";

var previousWrites =
    Environment.GetEnvironmentVariable(
        "NAVBR_OMSI_EXPERIMENTAL_WRITES");
var previousBackend =
    Environment.GetEnvironmentVariable(
        "NAVBR_OMSI_PHYSICAL_BACKEND");

var tempRoot = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-SignalR-" +
    Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(
    Path.Combine(tempRoot, "Vehicles", "LocalPack"));

var localVehicleFullPath =
    Path.Combine(tempRoot, localVehiclePath);
var vehicleBytes = Encoding.UTF8.GetBytes(
    "[friendlyname]\r\nNavBR SignalR openOMSI Smoke\r\n");
File.WriteAllBytes(
    localVehicleFullPath,
    vehicleBytes);
var vehicleCompatibilityId =
    "sha256:" +
    Convert.ToHexString(
        SHA256.HashData(vehicleBytes))
    .ToLowerInvariant();

var tcpPort = GetFreeTcpPort();
var serverUrl = $"http://127.0.0.1:{tcpPort}";
var app = NavBRServerApplication.Build(
    listenUrl: serverUrl);

MultiplayerClientService? receiver = null;
MultiplayerClientService? sender = null;
UdpClient? openOmsiClient = null;
var gateway = OpenOmsiLanGateway.Shared;
var gatewayDisposed = false;

try
{
    // Process scope only. ConnectAsync sees the feature as already enabled and
    // therefore never calls the persistent user-setting writer.
    Environment.SetEnvironmentVariable(
        "NAVBR_OMSI_EXPERIMENTAL_WRITES",
        "1");
    Environment.SetEnvironmentVariable(
        "NAVBR_OMSI_PHYSICAL_BACKEND",
        "1");

    await app.StartAsync();

    gateway.Start();
    var gatewayPort = gateway.Port ??
        throw new InvalidOperationException(
            "openOMSI gateway did not bind a loopback UDP port.");
    var gatewayEndpoint =
        new IPEndPoint(
            IPAddress.Loopback,
            gatewayPort);

    openOmsiClient = new UdpClient(
        new IPEndPoint(
            IPAddress.Loopback,
            0));

    var hello =
        "HELLO|6|-|CI openOMSI|" +
        "Vehicles/LocalPack/Receiver.bus|" +
        "maps/Grundorf/global.cfg|" +
        "2026-10-01|36000||autumn|" +
        "0011223344556677";
    await SendTextAsync(
        openOmsiClient,
        gatewayEndpoint,
        hello);

    var welcome = await ReceiveUntilTextAsync(
        openOmsiClient,
        text => text.StartsWith(
            "WELCOME|6|2|",
            StringComparison.Ordinal),
        TimeSpan.FromSeconds(4));
    Require(
        welcome.Contains(
            "|maps/Grundorf/global.cfg|",
            StringComparison.Ordinal),
        "Gateway WELCOME lost the local openOMSI map.");

    receiver = new MultiplayerClientService(
        omsiInstallDirectorySource: null,
        openOmsiContentRootsSource:
            () => new[] { tempRoot });
    sender = new MultiplayerClientService();

    var receiverManifest =
        new OmsiCompatibilityManifest(
            OmsiVersion: "openOMSI",
            NavBRVersion: "ci",
            MapName: mapName,
            MapCompatibilityId:
                mapCompatibilityId,
            VehiclePath: null,
            VehicleCompatibilityId: null,
            HofName: null,
            HofCompatibilityId: null,
            PluginProtocolVersion:
                PluginBridgeProtocol.Version,
            PluginDeployment:
                "OPENOMSI-X64",
            Capabilities:
            [
                PluginBridgeProtocol
                    .CapabilityOpenOmsiStandardPlugin
            ]);

    var senderManifest =
        new OmsiCompatibilityManifest(
            OmsiVersion: "2.3.004",
            NavBRVersion: "ci",
            MapName: mapName,
            MapCompatibilityId:
                mapCompatibilityId,
            VehiclePath:
                reportedVehiclePath,
            VehicleCompatibilityId:
                vehicleCompatibilityId,
            HofName: null,
            HofCompatibilityId: null,
            PluginProtocolVersion:
                PluginBridgeProtocol.Version,
            PluginDeployment:
                "NATIVE-AOT-X86",
            Capabilities:
                Array.Empty<string>());

    var receiverSettings =
        MultiplayerSettings.CreateDefault() with
        {
            PlayerId = receiverPlayerId,
            DisplayName = "CI openOMSI Receiver",
            ServerUrl = serverUrl,
            RoomId = roomId,
            ExperimentalPhysicalVehiclesEnabled =
                true
        };
    var senderSettings =
        MultiplayerSettings.CreateDefault() with
        {
            PlayerId = senderPlayerId,
            DisplayName = "CI OMSI2 Sender",
            ServerUrl = serverUrl,
            RoomId = roomId,
            ExperimentalPhysicalVehiclesEnabled =
                true
        };

    await receiver.ConnectAsync(
        receiverSettings,
        mapName,
        mapCompatibilityId,
        receiverManifest);
    await sender.ConnectAsync(
        senderSettings,
        mapName,
        mapCompatibilityId,
        senderManifest);

    var firstTimestamp =
        DateTimeOffset.UtcNow;
    var firstTelemetry =
        new VehicleTelemetry(
            PlayerId: senderPlayerId,
            Timestamp: firstTimestamp,
            MapName: mapName,
            VehicleName: "Remote Smoke Bus",
            Line: "76",
            Route: "76/1",
            X: 325.0,
            Y: 1.5,
            Z: 442.0,
            HeadingDegrees: 45.0,
            SpeedKph: 18.0,
            IsInGame: true,
            MapCompatibilityId:
                mapCompatibilityId,
            DestinationName:
                "Rathaus Spandau",
            VehiclePath:
                reportedVehiclePath,
            ThrottlePercent: 38d,
            BrakePercent: 0d,
            SteeringDegrees: 7.5d,
            Doors:
                VehicleDoorFlags.Middle,
            Lights:
                VehicleLightFlags.Position |
                VehicleLightFlags.LowBeam,
            TurnSignal:
                TurnSignalState.Right,
            WipersActive: true,
            VehicleCompatibilityId:
                vehicleCompatibilityId,
            LocalX: 25.0,
            LocalY: 1.5,
            LocalZ: 142.0,
            RotationX: 0d,
            RotationY: 0d,
            RotationZ: 0d,
            RotationW: 1d,
            PhysicalGridX: 1,
            PhysicalGridY: 1,
            SourceTimestampUnixMilliseconds:
                firstTimestamp
                    .ToUnixTimeMilliseconds());

    await sender.PublishTelemetryAsync(
        firstTelemetry);

    var infoText =
        await ReceiveUntilTextAsync(
            openOmsiClient,
            text =>
                text.StartsWith(
                    "INFO|",
                    StringComparison.Ordinal) &&
                text.Contains(
                    "|CI OMSI2 Sender|",
                    StringComparison.Ordinal),
            TimeSpan.FromSeconds(8));
    Require(
        OpenOmsiLanProtocol.TryDecodeInfo(
            infoText,
            out var remoteInfo),
        "SignalR-routed openOMSI INFO did not decode.");
    Require(
        remoteInfo.VehiclePath ==
            "Vehicles/LocalPack/ResolvedRemote.bus",
        $"Remote SHA was not relocated to the receiver-local bus path. Got '{remoteInfo.VehiclePath}'.");
    Require(
        remoteInfo.Line == "76",
        "SignalR route lost remote line.");
    Require(
        remoteInfo.Destination ==
            "Rathaus Spandau",
        "SignalR route lost remote destination.");

    var firstStateBytes =
        await ReceiveUntilBytesAsync(
            openOmsiClient,
            bytes =>
                IsStateFor(
                    bytes,
                    remoteInfo.PlayerId),
            TimeSpan.FromSeconds(8));
    Require(
        OpenOmsiLanStateCodec.TryDecode(
            firstStateBytes,
            out var firstState),
        "SignalR-routed openOMSI STATE did not decode.");

    // Sender is OMSI 2/D3D: X stays X, OMSI Z becomes openOMSI ground Y,
    // and OMSI Y becomes openOMSI vertical Z.
    Near(
        firstState.X,
        325.0,
        0.01,
        "first state x");
    Near(
        firstState.Y,
        442.0,
        0.01,
        "first state ground y");
    Near(
        firstState.Z,
        1.5,
        0.01,
        "first state height z");
    Near(
        firstState.HeadingDegrees,
        45.0,
        0.02,
        "first state heading");
    Require(
        firstState.TurnSignal == 2,
        "SignalR route lost right indicator.");
    Require(
        firstState.Doors.Count >= 2 &&
        firstState.Doors[1] > 0.99f,
        "SignalR route lost middle door state.");

    var secondTimestamp =
        firstTimestamp +
        TimeSpan.FromMilliseconds(100);
    var secondTelemetry =
        firstTelemetry with
        {
            Timestamp =
                secondTimestamp,
            X = 331.25,
            Y = 1.65,
            Z = 450.5,
            HeadingDegrees = 63.5,
            SpeedKph = 27.0,
            ThrottlePercent = 61d,
            SteeringDegrees = -4.0d,
            SourceTimestampUnixMilliseconds =
                secondTimestamp
                    .ToUnixTimeMilliseconds()
        };

    await sender.PublishTelemetryAsync(
        secondTelemetry);

    var secondStateBytes =
        await ReceiveUntilBytesAsync(
            openOmsiClient,
            bytes =>
            {
                if (!IsStateFor(
                        bytes,
                        remoteInfo.PlayerId) ||
                    !OpenOmsiLanStateCodec
                        .TryDecode(
                            bytes,
                            out var candidate))
                {
                    return false;
                }

                return OpenOmsiLanStateCodec
                    .IsSequenceNewer(
                        candidate.Sequence,
                        firstState.Sequence);
            },
            TimeSpan.FromSeconds(8));
    Require(
        OpenOmsiLanStateCodec.TryDecode(
            secondStateBytes,
            out var secondState),
        "Second SignalR-routed STATE did not decode.");
    Near(
        secondState.X,
        331.25,
        0.01,
        "second state x");
    Near(
        secondState.Y,
        450.5,
        0.01,
        "second state ground y");
    Near(
        secondState.Z,
        1.65,
        0.01,
        "second state height z");
    Near(
        secondState.HeadingDegrees,
        63.5,
        0.02,
        "second state heading");
    Require(
        secondState.Sequence !=
            firstState.Sequence,
        "Second remote STATE reused the first sequence.");

    var gatewayStatus =
        gateway.GetStatus();
    var routedRemote =
        gatewayStatus.Remotes.SingleOrDefault(
            remote =>
                remote.PlayerId ==
                    senderPlayerId);
    Require(
        routedRemote is not null,
        "SignalR sender was not registered in the openOMSI gateway.");
    Require(
        routedRemote!.HasInfo &&
        routedRemote.HasState,
        "SignalR sender never became materializable in the gateway.");
    Require(
        string.Equals(
            routedRemote.VehiclePath,
            localVehiclePath.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase),
        "Gateway did not retain the receiver-local resolved bus path.");
    Require(
        string.Equals(
            routedRemote
                .ExpectedVehicleCompatibilityId,
            vehicleCompatibilityId,
            StringComparison.OrdinalIgnoreCase),
        "Gateway lost the authoritative remote vehicle SHA.");

    await sender.DisconnectAsync();

    var bye = await ReceiveUntilTextAsync(
        openOmsiClient,
        text =>
            text ==
            $"BYE|{remoteInfo.PlayerId}",
        TimeSpan.FromSeconds(8));
    Require(
        bye ==
            $"BYE|{remoteInfo.PlayerId}",
        "SignalR playerLeft did not become openOMSI BYE.");

    var removalDeadline =
        DateTimeOffset.UtcNow +
        TimeSpan.FromSeconds(3);
    while (gateway.GetStatus().Remotes.Any(
               remote =>
                   remote.PlayerId ==
                       senderPlayerId) &&
           DateTimeOffset.UtcNow <
               removalDeadline)
    {
        await Task.Delay(20);
    }

    Require(
        gateway.GetStatus().Remotes.All(
            remote =>
                remote.PlayerId !=
                    senderPlayerId),
        "Disconnected SignalR player remained in openOMSI gateway diagnostics.");

    Console.WriteLine(
        "SignalR -> NavBR client -> SHA resolver -> openOMSI LAN v6 smoke passed: " +
        "remote path relocation + D3D axis conversion + two STATE updates + BYE.");
}
finally
{
    if (sender is not null)
    {
        await sender.DisposeAsync();
    }

    if (receiver is not null)
    {
        await receiver.DisposeAsync();
    }

    if (!gatewayDisposed)
    {
        try
        {
            await gateway.DisposeAsync();
        }
        catch
        {
        }

        gatewayDisposed = true;
    }

    openOmsiClient?.Dispose();

    try
    {
        await app.StopAsync();
    }
    catch
    {
    }

    await app.DisposeAsync();

    Environment.SetEnvironmentVariable(
        "NAVBR_OMSI_EXPERIMENTAL_WRITES",
        previousWrites);
    Environment.SetEnvironmentVariable(
        "NAVBR_OMSI_PHYSICAL_BACKEND",
        previousBackend);

    try
    {
        Directory.Delete(
            tempRoot,
            recursive: true);
    }
    catch
    {
    }
}

static int GetFreeTcpPort()
{
    var listener = new TcpListener(
        IPAddress.Loopback,
        0);
    listener.Start();
    try
    {
        return ((IPEndPoint)
            listener.LocalEndpoint).Port;
    }
    finally
    {
        listener.Stop();
    }
}

static async Task SendTextAsync(
    UdpClient client,
    IPEndPoint endpoint,
    string text)
{
    var bytes =
        Encoding.UTF8.GetBytes(text);
    await client.SendAsync(
        bytes,
        endpoint);
}

static bool IsStateFor(
    byte[] bytes,
    ushort playerId) =>
    bytes.Length >=
        OpenOmsiLanProtocol.StateHeaderBytes &&
    bytes[0] ==
        OpenOmsiLanProtocol.StateMagic &&
    bytes[1] ==
        OpenOmsiLanProtocol.ProtocolVersion &&
    BitConverter.ToUInt16(
        bytes,
        2) == playerId;

static async Task<string>
    ReceiveUntilTextAsync(
        UdpClient client,
        Func<string, bool> predicate,
        TimeSpan timeout)
{
    var deadline =
        DateTimeOffset.UtcNow + timeout;
    while (DateTimeOffset.UtcNow <
           deadline)
    {
        var remaining =
            deadline -
            DateTimeOffset.UtcNow;
        using var cts =
            new CancellationTokenSource(
                remaining);

        UdpReceiveResult result;
        try
        {
            result =
                await client.ReceiveAsync(
                    cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }

        if (result.Buffer.Length == 0 ||
            result.Buffer[0] ==
                OpenOmsiLanProtocol.StateMagic)
        {
            continue;
        }

        string text;
        try
        {
            text =
                new UTF8Encoding(
                    false,
                    true)
                .GetString(
                    result.Buffer);
        }
        catch (
            DecoderFallbackException)
        {
            continue;
        }

        if (predicate(text))
        {
            return text;
        }
    }

    throw new TimeoutException(
        "Expected openOMSI text datagram was not received.");
}

static async Task<byte[]>
    ReceiveUntilBytesAsync(
        UdpClient client,
        Func<byte[], bool> predicate,
        TimeSpan timeout)
{
    var deadline =
        DateTimeOffset.UtcNow + timeout;
    while (DateTimeOffset.UtcNow <
           deadline)
    {
        var remaining =
            deadline -
            DateTimeOffset.UtcNow;
        using var cts =
            new CancellationTokenSource(
                remaining);

        UdpReceiveResult result;
        try
        {
            result =
                await client.ReceiveAsync(
                    cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }

        if (predicate(result.Buffer))
        {
            return result.Buffer;
        }
    }

    throw new TimeoutException(
        "Expected openOMSI binary datagram was not received.");
}

static void Require(
    bool condition,
    string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(
            message);
    }
}

static void Near(
    double actual,
    double expected,
    double tolerance,
    string label)
{
    if (!double.IsFinite(actual) ||
        Math.Abs(actual - expected) >
            tolerance)
    {
        throw new InvalidOperationException(
            $"{label}: expected {expected}, got {actual} (tol {tolerance}).");
    }
}
