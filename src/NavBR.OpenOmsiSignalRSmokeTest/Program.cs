using System.Net;
using System.Net.Sockets;
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
const string vehicleCompatibilityId =
    "sha256:2222222222222222222222222222222222222222222222222222222222222222";
const string roomId = "ci-openomsi-v6-sidecar";
const string senderPlayerId = "ci-omsi2-sender";
const string receiverPlayerId = "ci-omsi2-receiver";
const string reportedVehiclePath =
    @"Vehicles\RemotePack\Remote.bus";

var previousWrites =
    Environment.GetEnvironmentVariable(
        "NAVBR_OMSI_EXPERIMENTAL_WRITES");
var previousBackend =
    Environment.GetEnvironmentVariable(
        "NAVBR_OMSI_PHYSICAL_BACKEND");

var tcpPort = GetFreeTcpPort();
var serverUrl = $"http://127.0.0.1:{tcpPort}";
var app = NavBRServerApplication.Build(
    listenUrl: serverUrl);

MultiplayerClientService? receiver = null;
MultiplayerClientService? sender = null;
UdpClient? observer = null;

try
{
    // Keep the old feature switch enabled deliberately: this smoke must prove
    // that SignalR telemetry still does NOT revive the retired physical route.
    Environment.SetEnvironmentVariable(
        "NAVBR_OMSI_EXPERIMENTAL_WRITES",
        "1");
    Environment.SetEnvironmentVariable(
        "NAVBR_OMSI_PHYSICAL_BACKEND",
        "1");

    await app.StartAsync();

    receiver = new MultiplayerClientService();
    sender = new MultiplayerClientService();

    var receiverManifest =
        new OmsiCompatibilityManifest(
            OmsiVersion: "2.3.004",
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
                "NATIVE-AOT-X86",
            Capabilities:
                Array.Empty<string>());

    var senderManifest =
        receiverManifest with
        {
            VehiclePath =
                reportedVehiclePath,
            VehicleCompatibilityId =
                vehicleCompatibilityId
        };

    var receiverSettings =
        MultiplayerSettings.CreateDefault() with
        {
            PlayerId = receiverPlayerId,
            DisplayName = "CI OMSI2 Receiver",
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

    Require(
        receiver.IsTrafficAuthority,
        "First room member did not become the openOMSI v6 authority.");
    Require(
        receiver.UsesOpenOmsiV6Transport &&
        receiver.IsOpenOmsiV6Host,
        "Receiver did not start the authoritative openOMSI LAN v6 host.");

    var hostPort =
        receiver.OpenOmsiV6Port ??
        throw new InvalidOperationException(
            "openOMSI v6 host did not expose its UDP port.");
    var hostEndpoint =
        new IPEndPoint(
            IPAddress.Loopback,
            hostPort);

    // A raw observer represents another openOMSI-compatible v6 peer. It
    // watches the physical wire directly, independently of the SignalR plane.
    observer = new UdpClient(
        new IPEndPoint(
            IPAddress.Loopback,
            0));
    var hello =
        "HELLO|6|-|CI v6 Observer||" +
        "maps/Grundorf/global.cfg|" +
        "2026-10-07|0|||0011223344556677";
    await SendTextAsync(
        observer,
        hostEndpoint,
        hello);

    var welcome =
        await ReceiveUntilTextAsync(
            observer,
            text =>
                text.StartsWith(
                    "WELCOME|6|",
                    StringComparison.Ordinal),
            TimeSpan.FromSeconds(8));
    Require(
        welcome.Contains(
            "|maps/Grundorf/global.cfg|",
            StringComparison.Ordinal),
        "openOMSI v6 host WELCOME lost the room map.");

    var sidecarTelemetry =
        new TaskCompletionSource<PlayerTelemetryFrame>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    receiver.TelemetryReceived += frame =>
    {
        if (string.Equals(
                frame.Player.PlayerId,
                senderPlayerId,
                StringComparison.OrdinalIgnoreCase) &&
            frame.Telemetry.SourceTimestampUnixMilliseconds is long source &&
            source == _expectedSidecarTimestamp)
        {
            sidecarTelemetry.TrySetResult(frame);
        }
    };

    await sender.ConnectAsync(
        senderSettings,
        mapName,
        mapCompatibilityId,
        senderManifest);

    Require(
        sender.UsesOpenOmsiV6Transport &&
        !sender.IsOpenOmsiV6Host,
        "Second OMSI 2 app client did not join the authority's openOMSI v6 transport.");
    Require(
        !string.IsNullOrWhiteSpace(
            receiver.OpenOmsiV6SessionCode) &&
        string.Equals(
            receiver.OpenOmsiV6SessionCode,
            sender.OpenOmsiV6SessionCode,
            StringComparison.Ordinal),
        "Host and client did not converge on the same openOMSI v6 session code.");

    var firstTimestamp =
        DateTimeOffset.UtcNow;
    _expectedSidecarTimestamp =
        firstTimestamp.ToUnixTimeMilliseconds();

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
                _expectedSidecarTimestamp);

    await sender.PublishTelemetryAsync(
        firstTelemetry);

    var sidecarFrame =
        await sidecarTelemetry.Task.WaitAsync(
            TimeSpan.FromSeconds(8));
    Require(
        sidecarFrame.Telemetry.X == firstTelemetry.X &&
        sidecarFrame.Telemetry.Y == firstTelemetry.Y &&
        sidecarFrame.Telemetry.Z == firstTelemetry.Z,
        "SignalR sidecar changed the service telemetry payload.");

    var infoText =
        await ReceiveUntilTextAsync(
            observer,
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
        "openOMSI v6 INFO from the OMSI 2 sender did not decode.");
    Require(
        remoteInfo.VehiclePath ==
            "Vehicles/RemotePack/Remote.bus",
        $"openOMSI v6 INFO changed the sender bus path. Got '{remoteInfo.VehiclePath}'.");
    Require(
        remoteInfo.Line == "76",
        "openOMSI v6 INFO lost remote line.");
    Require(
        remoteInfo.Destination ==
            "Rathaus Spandau",
        "openOMSI v6 INFO lost remote destination.");

    var firstStateBytes =
        await ReceiveUntilBytesAsync(
            observer,
            bytes =>
                IsStateFor(
                    bytes,
                    remoteInfo.PlayerId),
            TimeSpan.FromSeconds(8));
    Require(
        OpenOmsiLanStateCodec.TryDecode(
            firstStateBytes,
            out var firstState),
        "openOMSI v6 STATE from the OMSI 2 sender did not decode.");

    // OMSI 2/D3D -> openOMSI: X stays X, OMSI Z is ground Y,
    // and OMSI Y is vertical Z.
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
        "openOMSI v6 STATE lost right indicator.");
    Require(
        firstState.Doors.Count >= 2 &&
        firstState.Doors[1] > 0.99f,
        "openOMSI v6 STATE lost middle door state.");

    var legacyGateway =
        OpenOmsiLanGateway.Shared.GetStatus();
    Require(
        !legacyGateway.Running &&
        legacyGateway.Remotes.Count == 0,
        "SignalR telemetry revived the retired NavBR physical LAN gateway.");

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
            observer,
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
        "Second openOMSI v6 STATE did not decode.");
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
        "Second openOMSI v6 STATE reused the first sequence.");

    await sender.DisconnectAsync();

    var bye =
        await ReceiveUntilTextAsync(
            observer,
            text =>
                text ==
                $"BYE|{remoteInfo.PlayerId}",
            TimeSpan.FromSeconds(8));
    Require(
        bye ==
            $"BYE|{remoteInfo.PlayerId}",
        "openOMSI v6 sender disconnect did not relay BYE.");

    Require(
        OpenOmsiLanGateway.Shared
            .GetStatus()
            .Remotes.Count == 0,
        "Retired physical gateway gained remotes after disconnect.");

    Console.WriteLine(
        "SignalR sidecar + openOMSI LAN v6 smoke passed: " +
        "service telemetry stayed on SignalR, physical INFO/STATE/BYE stayed on v6, " +
        "and the retired NavBR physical gateway remained inactive.");
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

    observer?.Dispose();

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
}

static long _expectedSidecarTimestamp;

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
