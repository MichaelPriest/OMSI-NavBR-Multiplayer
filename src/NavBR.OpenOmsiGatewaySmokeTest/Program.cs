using System.Net;
using System.Net.Sockets;
using System.Text;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.OpenOmsi;
using NavBR.Shared.Telemetry;

Environment.SetEnvironmentVariable(
    "NAVBR_OMSI_EXPERIMENTAL_WRITES",
    "1");
Environment.SetEnvironmentVariable(
    "NAVBR_OMSI_PHYSICAL_BACKEND",
    "1");

await using var gateway = new OpenOmsiLanGateway();
var localTelemetryTcs =
    new TaskCompletionSource<VehicleTelemetry>(
        TaskCreationOptions.RunContinuationsAsynchronously);
gateway.LocalTelemetryReceived += telemetry =>
    localTelemetryTcs.TrySetResult(telemetry);
gateway.Start();

var port = gateway.Port
    ?? throw new InvalidOperationException(
        "Gateway did not bind a loopback UDP port.");

using var client = new UdpClient(
    new IPEndPoint(IPAddress.Loopback, 0));
var gatewayEndpoint =
    new IPEndPoint(IPAddress.Loopback, port);

await SendTextAsync(
    client,
    gatewayEndpoint,
    "DISCOVER|6");

var here = await ReceiveUntilTextAsync(
    client,
    text => text.StartsWith("HERE|6|NavBR Gateway|", StringComparison.Ordinal),
    TimeSpan.FromSeconds(4));
Require(here.Contains("|1", StringComparison.Ordinal), "HERE did not report a player count.");

const string hello =
    "HELLO|6|-|Local Driver|Vehicles/MAN_NL_NG/MAN_EN92_main.bus|" +
    "maps/Grundorf/global.cfg|2026-10-01|36000||autumn|0011223344556677";
await SendTextAsync(client, gatewayEndpoint, hello);

var welcome = await ReceiveUntilTextAsync(
    client,
    text => text.StartsWith("WELCOME|6|2|", StringComparison.Ordinal),
    TimeSpan.FromSeconds(4));
Require(
    welcome.Contains("|maps/Grundorf/global.cfg|2026-10-01|", StringComparison.Ordinal),
    "WELCOME did not preserve the local openOMSI world.");

const string localInfo =
    "INFO|2|Local Driver|Vehicles/MAN_NL_NG/MAN_EN92_main.bus||76|" +
    "Rathaus Spandau|12.20|2.55|0.00|00000000|76/1||";
await SendTextAsync(client, gatewayEndpoint, localInfo);

var localState = new OpenOmsiLanVehicleState(
    PlayerId: 2,
    Sequence: 11,
    Flags:
        OpenOmsiLanProtocol.FlagVehicle |
        OpenOmsiLanProtocol.FlagEngine |
        OpenOmsiLanProtocol.FlagElectrics |
        OpenOmsiLanProtocol.FlagHorn |
        OpenOmsiLanProtocol.FlagWipers,
    X: 202.3,
    Y: 135.7,
    Z: 1.2,
    HeadingDegrees: 91.5f,
    PitchDegrees: 0f,
    BankDegrees: 0f,
    SpeedKph: 31.4f,
    SteeringDegrees: -4.5f,
    HeadLightLevel: 2,
    InteriorLightLevel: 3,
    TurnSignal: 1,
    EngineRpm: 1450f,
    Throttle: 0.45f,
    Brake: 0.1f,
    Passengers: 12,
    Doors: [1f, 0f, 0f],
    Suspension: [],
    RearSections: [],
    Lamps: [],
    Switches: [],
    Values: [],
    Walker: null,
    SentMilliseconds: 12345);
await SendBytesAsync(
    client,
    gatewayEndpoint,
    OpenOmsiLanStateCodec.Encode(localState));

using (var localTimeout = new CancellationTokenSource(
           TimeSpan.FromSeconds(4)))
{
    var localTelemetry =
        await localTelemetryTcs.Task.WaitAsync(localTimeout.Token);
    Require(localTelemetry.MapName == "Grundorf", "Local map was not derived from HELLO.");
    Require(
        localTelemetry.VehiclePath ==
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        "Local vehicle path was not taken from INFO.");
    Require(localTelemetry.Line == "76", "Local line was not taken from INFO.");
    Require(localTelemetry.Route == "76/1", "Local tour was not taken from INFO.");
    Near(localTelemetry.X, 202.3, 0.006, "local x");
    Near(localTelemetry.Y, 135.7, 0.006, "local y");
    Near(localTelemetry.SpeedKph, 31.4, 0.03, "local speed");
    Require(
        localTelemetry.Doors.HasFlag(VehicleDoorFlags.Front),
        "Local front door state was not mapped.");
    Require(
        localTelemetry.TurnSignal == TurnSignalState.Left,
        "Local indicator state was not mapped.");
    Require(localTelemetry.HornActive, "Local horn state was not mapped.");
    Require(localTelemetry.WipersActive, "Local wiper state was not mapped.");
}

await Task.Delay(55);
var secondLocalState = localState with
{
    Sequence = 12,
    X = 202.42,
    SentMilliseconds = 12400
};
await SendBytesAsync(
    client,
    gatewayEndpoint,
    OpenOmsiLanStateCodec.Encode(secondLocalState));

var stateDeadline =
    DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
while (gateway.GetStatus().LocalStateFrames < 2 &&
       DateTimeOffset.UtcNow < stateDeadline)
{
    await Task.Delay(20);
}

var streamingStatus = gateway.GetStatus();
Require(
    streamingStatus.LocalStateFrames >= 2,
    "Gateway STATE diagnostics did not count local frames.");
Require(
    streamingStatus.LastLocalStateSequence == 12,
    "Gateway STATE diagnostics did not retain the latest sequence.");
Require(
    streamingStatus.LocalStateRateHz is > 0d and <= 240d,
    "Gateway STATE diagnostics did not expose a valid receive rate.");
Require(
    streamingStatus.LastLocalStateUtc is not null,
    "Gateway STATE diagnostics did not expose the last STATE timestamp.");

await SendTextAsync(
    client,
    gatewayEndpoint,
    "PLACE|2|202.30|135.70|1.20|91.5|12.2|2.55");
var near = await ReceiveUntilTextAsync(
    client,
    text => text.StartsWith("NEAR|2|", StringComparison.Ordinal),
    TimeSpan.FromSeconds(4));
Require(near == "NEAR|2|", "Gateway should return an empty safe NEAR list in the first bridge.");

var pendingPresence = new PlayerPresence(
    "pending-identity",
    "Pending Identity",
    "navbr-smoke",
    "Grundorf",
    DateTimeOffset.UtcNow,
    Compatibility: new OmsiCompatibilityManifest(
        OmsiVersion: null,
        NavBRVersion: "smoke",
        MapName: "Grundorf",
        MapCompatibilityId: null,
        VehiclePath: null,
        VehicleCompatibilityId: null,
        HofName: null,
        HofCompatibilityId: null,
        PluginProtocolVersion: 3,
        PluginDeployment: "OPENOMSI-X64",
        Capabilities: []));
var pendingTelemetry = new VehicleTelemetry(
    "pending-identity",
    DateTimeOffset.UtcNow,
    "Grundorf",
    "Bus identity pending",
    null,
    null,
    210.0,
    140.0,
    1.2,
    90.0,
    0.0,
    true);

await gateway.UpsertRemoteAsync(
    new PlayerTelemetryFrame(
        pendingPresence,
        pendingTelemetry));

var pendingStatus = gateway.GetStatus();
var pendingRemote = pendingStatus.Remotes.SingleOrDefault(
    remote => remote.PlayerId == "pending-identity");
Require(
    pendingRemote is not null,
    "Pending-identity remote was not retained for diagnostics.");
Require(
    !pendingRemote!.HasInfo && !pendingRemote.HasState,
    "Pending-identity remote incorrectly advertised INFO/STATE readiness.");
Require(
    pendingStatus.RemotePlayers == 0,
    "Pending-identity remote incorrectly counted as a materializable player.");

await RequireNoRemoteAnnouncementAsync(
    client,
    pendingRemote.LanId,
    "Pending Identity",
    TimeSpan.FromMilliseconds(700));

var resolvedPendingPresence = pendingPresence with
{
    Compatibility = pendingPresence.Compatibility! with
    {
        VehiclePath =
            @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        VehicleCompatibilityId =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
    }
};
var resolvedPendingTelemetry = pendingTelemetry with
{
    VehicleName = "NL202 - EN92",
    VehiclePath =
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
    VehicleCompatibilityId =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    SpeedKph = 12.0
};

await gateway.UpsertRemoteAsync(
    new PlayerTelemetryFrame(
        resolvedPendingPresence,
        resolvedPendingTelemetry));

var pendingInfoText = await ReceiveUntilTextAsync(
    client,
    text =>
        text.StartsWith(
            $"INFO|{pendingRemote.LanId}|",
            StringComparison.Ordinal),
    TimeSpan.FromSeconds(4));
Require(
    OpenOmsiLanProtocol.TryDecodeInfo(
        pendingInfoText,
        out var resolvedPendingInfo),
    "Resolved pending remote INFO did not decode.");
Require(
    resolvedPendingInfo.VehiclePath ==
        "Vehicles/MAN_NL_NG/MAN_EN92_main.bus",
    "Resolved pending remote INFO lost the vehicle path.");

var pendingStateBytes = await ReceiveUntilBytesAsync(
    client,
    bytes =>
        bytes.Length >=
            OpenOmsiLanProtocol.StateHeaderBytes &&
        bytes[0] ==
            OpenOmsiLanProtocol.StateMagic &&
        BitConverter.ToUInt16(bytes, 2) ==
            pendingRemote.LanId,
    TimeSpan.FromSeconds(4));
Require(
    OpenOmsiLanStateCodec.TryDecode(
        pendingStateBytes,
        out _),
    "Resolved pending remote STATE did not decode.");

var resolvedPendingStatus = gateway.GetStatus();
var resolvedPendingRemote =
    resolvedPendingStatus.Remotes.Single(
        remote =>
            remote.PlayerId == "pending-identity");
Require(
    resolvedPendingRemote.HasInfo &&
    resolvedPendingRemote.HasState,
    "Resolved pending remote did not become INFO/STATE ready.");
Require(
    resolvedPendingStatus.RemotePlayers == 1,
    "Resolved pending remote was not counted as materializable.");

await gateway.SetRemotePendingAsync(
    new PlayerTelemetryFrame(
        resolvedPendingPresence,
        resolvedPendingTelemetry));
var pendingBye = await ReceiveUntilTextAsync(
    client,
    text =>
        text ==
        $"BYE|{pendingRemote.LanId}",
    TimeSpan.FromSeconds(4));
Require(
    pendingBye ==
        $"BYE|{pendingRemote.LanId}",
    "Active remote was not removed from openOMSI when it became pending.");

var rependingStatus = gateway.GetStatus();
var rependingRemote =
    rependingStatus.Remotes.Single(
        remote =>
            remote.PlayerId == "pending-identity");
Require(
    !rependingRemote.HasInfo &&
    !rependingRemote.HasState,
    "Pending transition retained materialization readiness.");
Require(
    rependingStatus.RemotePlayers == 0,
    "Pending transition remained counted as a materializable player.");
Require(
    rependingRemote.VehiclePath ==
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
    "Pending transition lost the reported remote vehicle path.");
Require(
    rependingRemote.ExpectedVehicleCompatibilityId ==
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "Pending transition lost the expected remote vehicle fingerprint.");

await gateway.RemoveRemoteAsync("pending-identity");
Require(
    gateway.GetStatus().Remotes.All(
        remote =>
            remote.PlayerId != "pending-identity"),
    "Pending remote remained registered after explicit removal.");

var remotePresence = new PlayerPresence(
    "remote-1",
    "Remote Driver",
    "navbr-smoke",
    "Grundorf",
    DateTimeOffset.UtcNow,
    Compatibility: new OmsiCompatibilityManifest(
        OmsiVersion: null,
        NavBRVersion: "smoke",
        MapName: "Grundorf",
        MapCompatibilityId: null,
        VehiclePath: @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        VehicleCompatibilityId: null,
        HofName: null,
        HofCompatibilityId: null,
        PluginProtocolVersion: 3,
        PluginDeployment: "SMOKE",
        Capabilities: []));
var remoteTelemetry = new VehicleTelemetry(
    "remote-1",
    DateTimeOffset.UtcNow,
    "Grundorf",
    "NL202 - EN92",
    "76",
    "76/1",
    225.25,
    142.5,
    1.3,
    180.25,
    42.0,
    true,
    DestinationName: "Rathaus Spandau",
    VehiclePath: @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
    ThrottlePercent: 52,
    BrakePercent: 0,
    SteeringDegrees: 8.2,
    Doors: VehicleDoorFlags.Middle,
    Lights: VehicleLightFlags.Position |
            VehicleLightFlags.LowBeam |
            VehicleLightFlags.Interior,
    TurnSignal: TurnSignalState.Right,
    HornActive: false,
    WipersActive: true,
    ParkingBrakeActive: false,
    ReverseGear: false);
var remoteFrame =
    new PlayerTelemetryFrame(remotePresence, remoteTelemetry);

await gateway.UpsertRemoteAsync(remoteFrame);

var remoteInfoText = await ReceiveUntilTextAsync(
    client,
    text =>
        text.StartsWith("INFO|", StringComparison.Ordinal) &&
        text.Contains("|Remote Driver|", StringComparison.Ordinal),
    TimeSpan.FromSeconds(4));
Require(
    OpenOmsiLanProtocol.TryDecodeInfo(
        remoteInfoText,
        out var remoteInfo),
    "Remote INFO did not decode.");
Require(remoteInfo.PlayerId >= 3, "Remote player id must not collide with local id 2.");
Require(
    remoteInfo.VehiclePath ==
    "Vehicles/MAN_NL_NG/MAN_EN92_main.bus",
    "Remote INFO lost the vehicle path.");
Require(remoteInfo.Line == "76", "Remote INFO lost line.");
Require(
    remoteInfo.Destination == "Rathaus Spandau",
    "Remote INFO lost destination.");

var remoteStateBytes = await ReceiveUntilBytesAsync(
    client,
    bytes =>
        bytes.Length >= OpenOmsiLanProtocol.StateHeaderBytes &&
        bytes[0] == OpenOmsiLanProtocol.StateMagic &&
        BitConverter.ToUInt16(bytes, 2) == remoteInfo.PlayerId,
    TimeSpan.FromSeconds(4));
Require(
    OpenOmsiLanStateCodec.TryDecode(
        remoteStateBytes,
        out var remoteState),
    "Remote STATE did not decode.");
Near(remoteState.X, 225.25, 0.006, "remote x");
Near(remoteState.Y, 142.5, 0.006, "remote y");
Near(remoteState.HeadingDegrees, 180.25, 0.01, "remote heading");
Near(remoteState.SpeedKph, 42.0, 0.03, "remote speed");
Require(remoteState.TurnSignal == 2, "Remote indicator was not mapped.");
Require(remoteState.HeadLightLevel == 2, "Remote low beam was not mapped.");
Require(remoteState.InteriorLightLevel == 3, "Remote interior light was not mapped.");
Require(remoteState.Doors.Count >= 2 && remoteState.Doors[1] > 0.99f, "Remote middle door was not mapped.");
Require(
    (remoteState.Flags & OpenOmsiLanProtocol.FlagWipers) != 0,
    "Remote wiper flag was not mapped.");
Require(gateway.HasRemote("remote-1"), "Gateway did not report the remote bus.");

var nativePresence = new PlayerPresence(
    "native-omsi2",
    "Native OMSI Driver",
    "navbr-smoke",
    "Grundorf",
    DateTimeOffset.UtcNow,
    Compatibility: new OmsiCompatibilityManifest(
        OmsiVersion: "2.3.004",
        NavBRVersion: "smoke",
        MapName: "Grundorf",
        MapCompatibilityId: null,
        VehiclePath: @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
        VehicleCompatibilityId: null,
        HofName: null,
        HofCompatibilityId: null,
        PluginProtocolVersion: 3,
        PluginDeployment: "NATIVE-AOT-X86",
        Capabilities: []));
var nativeTelemetry = new VehicleTelemetry(
    "native-omsi2",
    DateTimeOffset.UtcNow,
    "Grundorf",
    "NL202 - EN92",
    "76",
    "76/1",
    X: 325.0,
    Y: 1.5,
    Z: 442.0,
    HeadingDegrees: 45.0,
    SpeedKph: 12.0,
    IsInGame: true,
    VehiclePath: @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
    LocalX: 25.0,
    LocalY: 1.5,
    LocalZ: 142.0,
    PhysicalGridX: 1,
    PhysicalGridY: 1,
    RearSections:
    [
        new VehicleSectionPose(
            LocalX: 298.0,
            LocalY: 1.4,
            LocalZ: 140.0,
            RotationX: 0.0,
            RotationY: 0.0,
            RotationZ: 0.0,
            RotationW: 1.0,
            GridX: 0,
            GridY: 1,
            MapTileIndex: 0)
    ]);
await gateway.UpsertRemoteAsync(
    new PlayerTelemetryFrame(nativePresence, nativeTelemetry));

var nativeInfoText = await ReceiveUntilTextAsync(
    client,
    text =>
        text.StartsWith("INFO|", StringComparison.Ordinal) &&
        text.Contains("|Native OMSI Driver|", StringComparison.Ordinal),
    TimeSpan.FromSeconds(4));
Require(
    OpenOmsiLanProtocol.TryDecodeInfo(nativeInfoText, out var nativeInfo),
    "Native OMSI remote INFO did not decode.");

var nativeStateBytes = await ReceiveUntilBytesAsync(
    client,
    bytes =>
        bytes.Length >= OpenOmsiLanProtocol.StateHeaderBytes &&
        bytes[0] == OpenOmsiLanProtocol.StateMagic &&
        BitConverter.ToUInt16(bytes, 2) == nativeInfo.PlayerId,
    TimeSpan.FromSeconds(4));
Require(
    OpenOmsiLanStateCodec.TryDecode(nativeStateBytes, out var nativeState),
    "Native OMSI remote STATE did not decode.");
Near(nativeState.X, 325.0, 0.006, "native world x");
Near(nativeState.Y, 442.0, 0.006, "native world ground y");
Near(nativeState.Z, 1.5, 0.006, "native world height z");
Require(nativeState.RearSections.Count == 1, "Native articulated rear section was lost.");
Near(nativeState.RearSections[0].X, 298.0, 0.02, "native rear world x");
Near(nativeState.RearSections[0].Y, 440.0, 0.02, "native rear world y");
Near(nativeState.RearSections[0].Z, 1.4, 0.02, "native rear height");
Near(nativeState.RearSections[0].HeadingDegrees, 0.0, 0.01, "native rear heading");

await gateway.RemoveRemoteAsync("native-omsi2");
var nativeBye = await ReceiveUntilTextAsync(
    client,
    text => text == $"BYE|{nativeInfo.PlayerId}",
    TimeSpan.FromSeconds(4));
Require(
    nativeBye == $"BYE|{nativeInfo.PlayerId}",
    "Native OMSI remote BYE did not carry the assigned LAN id.");

await gateway.RemoveRemoteAsync("remote-1");
var bye = await ReceiveUntilTextAsync(
    client,
    text => text == $"BYE|{remoteInfo.PlayerId}",
    TimeSpan.FromSeconds(4));
Require(
    bye == $"BYE|{remoteInfo.PlayerId}",
    "Remote BYE did not carry the assigned LAN id.");
Require(!gateway.HasRemote("remote-1"), "Remote bus remained registered after BYE.");

var status = gateway.GetStatus();
Require(status.Running, "Gateway stopped unexpectedly.");
Require(status.ClientConnected, "Gateway lost the local openOMSI client.");
Require(status.Port == port, "Gateway status port changed.");
Require(status.ClientName == "Local Driver", "Gateway client name mismatch.");
Require(status.VehiclePath == "Vehicles/MAN_NL_NG/MAN_EN92_main.bus", "Gateway status vehicle path mismatch.");
Require(status.RemotePlayers == 0, "Gateway status retained removed remotes.");
Require(status.LocalStateFrames >= 2, "Gateway status lost local STATE frame count.");
Require(status.LastLocalStateSequence == 12, "Gateway status lost latest local STATE sequence.");
Require(status.LocalStateRateHz is > 0d, "Gateway status lost local STATE rate.");
Require(status.LastLocalStateUtc is not null, "Gateway status lost local STATE timestamp.");
Require(string.IsNullOrWhiteSpace(status.LastError), $"Gateway reported an error: {status.LastError}");

await using (var replayGateway =
             new OpenOmsiLanGateway(TimeSpan.FromSeconds(5)))
{
    replayGateway.Start();
    var replayPort = replayGateway.Port
        ?? throw new InvalidOperationException(
            "Replay-freshness gateway did not bind a loopback UDP port.");
    using var replayClient = new UdpClient(
        new IPEndPoint(IPAddress.Loopback, 0));
    var replayEndpoint =
        new IPEndPoint(IPAddress.Loopback, replayPort);

    await SendTextAsync(replayClient, replayEndpoint, hello);
    _ = await ReceiveUntilTextAsync(
        replayClient,
        text => text.StartsWith(
            "WELCOME|6|2|",
            StringComparison.Ordinal),
        TimeSpan.FromSeconds(2));

    await replayGateway.UpsertRemoteAsync(remoteFrame);
    var replayInfoText = await ReceiveUntilTextAsync(
        replayClient,
        text =>
            text.StartsWith("INFO|", StringComparison.Ordinal) &&
            text.Contains(
                "|Remote Driver|",
                StringComparison.Ordinal),
        TimeSpan.FromSeconds(2));
    Require(
        OpenOmsiLanProtocol.TryDecodeInfo(
            replayInfoText,
            out var replayInfo),
        "Replay-freshness INFO did not decode.");

    _ = await ReceiveUntilBytesAsync(
        replayClient,
        bytes =>
            bytes.Length >= OpenOmsiLanProtocol.StateHeaderBytes &&
            bytes[0] == OpenOmsiLanProtocol.StateMagic &&
            BitConverter.ToUInt16(bytes, 2) ==
                replayInfo.PlayerId,
        TimeSpan.FromSeconds(2));

    await Task.Delay(TimeSpan.FromMilliseconds(2150));

    await SendTextAsync(replayClient, replayEndpoint, hello);
    _ = await ReceiveUntilTextAsync(
        replayClient,
        text => text.StartsWith(
            "WELCOME|6|2|",
            StringComparison.Ordinal),
        TimeSpan.FromSeconds(2));

    await RequireNoMatchingTextAsync(
        replayClient,
        text =>
            text.StartsWith("INFO|", StringComparison.Ordinal) &&
            text.Contains(
                "|Remote Driver|",
                StringComparison.Ordinal),
        TimeSpan.FromMilliseconds(450),
        "Reconnect replayed a stale openOMSI remote pose.");

    await replayGateway.UpsertRemoteAsync(
        remoteFrame with
        {
            Telemetry = remoteFrame.Telemetry with
            {
                Timestamp = DateTimeOffset.UtcNow,
                X = remoteFrame.Telemetry.X + 1d,
                SourceTimestampUnixMilliseconds =
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        });

    _ = await ReceiveUntilTextAsync(
        replayClient,
        text =>
            text.StartsWith("INFO|", StringComparison.Ordinal) &&
            text.Contains(
                "|Remote Driver|",
                StringComparison.Ordinal),
        TimeSpan.FromSeconds(2));
}

await using (var staleGateway =
             new OpenOmsiLanGateway(TimeSpan.FromMilliseconds(300)))
{
    staleGateway.Start();
    var stalePort = staleGateway.Port
        ?? throw new InvalidOperationException(
            "Stale-peer gateway did not bind a loopback UDP port.");
    using var staleClient = new UdpClient(
        new IPEndPoint(IPAddress.Loopback, 0));
    var staleEndpoint =
        new IPEndPoint(IPAddress.Loopback, stalePort);

    await SendTextAsync(staleClient, staleEndpoint, hello);
    _ = await ReceiveUntilTextAsync(
        staleClient,
        text => text.StartsWith(
            "WELCOME|6|2|",
            StringComparison.Ordinal),
        TimeSpan.FromSeconds(2));

    await staleGateway.UpsertRemoteAsync(remoteFrame);
    var staleInfoText = await ReceiveUntilTextAsync(
        staleClient,
        text =>
            text.StartsWith("INFO|", StringComparison.Ordinal) &&
            text.Contains(
                "|Remote Driver|",
                StringComparison.Ordinal),
        TimeSpan.FromSeconds(2));
    Require(
        OpenOmsiLanProtocol.TryDecodeInfo(
            staleInfoText,
            out var staleInfo),
        "Stale-peer INFO did not decode.");

    _ = await ReceiveUntilBytesAsync(
        staleClient,
        bytes =>
            bytes.Length >= OpenOmsiLanProtocol.StateHeaderBytes &&
            bytes[0] == OpenOmsiLanProtocol.StateMagic &&
            BitConverter.ToUInt16(bytes, 2) ==
                staleInfo.PlayerId,
        TimeSpan.FromSeconds(2));

    var staleBye = await ReceiveUntilTextAsync(
        staleClient,
        text => text == $"BYE|{staleInfo.PlayerId}",
        TimeSpan.FromSeconds(3));
    Require(
        staleBye == $"BYE|{staleInfo.PlayerId}",
        "Stale openOMSI remote was not retired with BYE.");
    Require(
        !staleGateway.HasRemote("remote-1"),
        "Stale openOMSI remote remained registered.");
}

var previousHome = Environment.GetEnvironmentVariable("HOME");
var previousUserProfile =
    Environment.GetEnvironmentVariable("USERPROFILE");
var statusHome = Path.Combine(
    Path.GetTempPath(),
    "NavBR-openOMSI-status-" + Guid.NewGuid().ToString("N"));
var lanStatusDirectory = Path.Combine(
    statusHome,
    ".openomsi",
    "lan");
Directory.CreateDirectory(lanStatusDirectory);
Environment.SetEnvironmentVariable("HOME", statusHome);
Environment.SetEnvironmentVariable("USERPROFILE", statusHome);
try
{
    var nowUnix =
        DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var statusPath = Path.Combine(
        lanStatusDirectory,
        "navbr-smoke.json");
    await File.WriteAllTextAsync(
        statusPath,
        $$"""
        {
          "pid": 4242,
          "role": "client",
          "connected": true,
          "map": "maps/Grundorf/global.cfg",
          "updated": {{nowUnix}},
          "players": [
            {
              "id": 7,
              "name": "Remote Driver",
              "bus": "Vehicles/MAN_NL_NG/MAN_EN92_main.bus",
              "line": "76",
              "destination": "Rathaus Spandau",
              "passengers": 23,
              "where": "35 m ahead",
              "drawn": true
            },
            {
              "id": 8,
              "name": "Waiting Driver",
              "bus": "Vehicles/MAN_NL_NG/MAN_EN92_main.bus",
              "line": "77",
              "destination": "Bahnhof",
              "passengers": 4,
              "where": "1.2 km behind",
              "drawn": false
            }
          ]
        }
        """);

    var runtimeStatus =
        OpenOmsiLanRuntimeStatusReader.Read(
            4242,
            "navbr-smoke");
    Require(runtimeStatus is not null, "openOMSI LAN runtime status was not read.");
    Require(runtimeStatus!.Fresh, "Fresh openOMSI LAN runtime status was marked stale.");
    Require(runtimeStatus.Connected, "Connected openOMSI LAN runtime status was lost.");
    Require(runtimeStatus.Map == "maps/Grundorf/global.cfg", "openOMSI LAN runtime map mismatch.");
    Require(runtimeStatus.IsDrawn(7), "drawn=true peer was not confirmed.");
    Require(!runtimeStatus.IsDrawn(8), "drawn=false peer was incorrectly confirmed.");
    var drawnPeer =
        runtimeStatus.Players.Single(
            player => player.Id == 7);
    Require(
        drawnPeer.Bus ==
            "Vehicles/MAN_NL_NG/MAN_EN92_main.bus",
        "openOMSI LAN runtime bus path mismatch.");
    Require(
        drawnPeer.PassengerCount == 23,
        "openOMSI LAN runtime passenger count mismatch.");
    Require(
        drawnPeer.RelativePosition == "35 m ahead",
        "openOMSI LAN runtime relative-position text mismatch.");

    var waitingPeer =
        runtimeStatus.Players.Single(
            player => player.Id == 8);
    Require(
        waitingPeer.PassengerCount == 4,
        "drawn=false peer passenger count mismatch.");
    Require(
        waitingPeer.RelativePosition == "1.2 km behind",
        "drawn=false peer relative-position text mismatch.");

    var stalePath = Path.Combine(
        lanStatusDirectory,
        "navbr-stale.json");
    await File.WriteAllTextAsync(
        stalePath,
        $$"""
        {
          "pid": 4243,
          "role": "client",
          "connected": true,
          "map": "maps/Grundorf/global.cfg",
          "updated": {{nowUnix - 60}},
          "players": [
            { "id": 7, "drawn": true }
          ]
        }
        """);

    var staleStatus =
        OpenOmsiLanRuntimeStatusReader.Read(
            4243,
            "navbr-stale");
    Require(staleStatus is not null, "Stale openOMSI LAN runtime status was not readable.");
    Require(!staleStatus!.Fresh, "Old openOMSI LAN runtime status was marked fresh.");
    Require(!staleStatus.IsDrawn(7), "Stale drawn=true status was accepted as physical confirmation.");
}
finally
{
    Environment.SetEnvironmentVariable("HOME", previousHome);
    Environment.SetEnvironmentVariable(
        "USERPROFILE",
        previousUserProfile);
    try
    {
        Directory.Delete(statusHome, recursive: true);
    }
    catch
    {
    }
}

Console.WriteLine(
    $"openOMSI LAN gateway smoke passed on 127.0.0.1:{port}: DISCOVER/HELLO/WELCOME/INFO/STATE/PLACE/NEAR + pending identity gate + canonical remote + native OMSI D3D axis/articulation + live diagnostics + official drawn status + fresh-only reconnect replay + stale peer expiry + BYE.");

static async Task SendTextAsync(
    UdpClient client,
    IPEndPoint endpoint,
    string text) =>
    await SendBytesAsync(
        client,
        endpoint,
        Encoding.UTF8.GetBytes(text));

static async Task SendBytesAsync(
    UdpClient client,
    IPEndPoint endpoint,
    byte[] bytes) =>
    await client.SendAsync(bytes, endpoint);

static async Task RequireNoMatchingTextAsync(
    UdpClient client,
    Func<string, bool> predicate,
    TimeSpan timeout,
    string message)
{
    using var cts =
        new CancellationTokenSource(timeout);
    try
    {
        while (true)
        {
            var result =
                await client.ReceiveAsync(cts.Token);
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
                    new UTF8Encoding(false, true)
                        .GetString(result.Buffer);
            }
            catch (DecoderFallbackException)
            {
                continue;
            }

            if (predicate(text))
            {
                throw new InvalidOperationException(message);
            }
        }
    }
    catch (OperationCanceledException)
        when (cts.IsCancellationRequested)
    {
    }
}

static async Task RequireNoRemoteAnnouncementAsync(
    UdpClient client,
    ushort lanId,
    string displayName,
    TimeSpan timeout)
{
    using var cts =
        new CancellationTokenSource(timeout);
    try
    {
        while (true)
        {
            var result =
                await client.ReceiveAsync(cts.Token);
            if (result.Buffer.Length == 0)
            {
                continue;
            }

            if (result.Buffer[0] ==
                OpenOmsiLanProtocol.StateMagic)
            {
                if (result.Buffer.Length >=
                        OpenOmsiLanProtocol.StateHeaderBytes &&
                    BitConverter.ToUInt16(
                        result.Buffer,
                        2) == lanId)
                {
                    throw new InvalidOperationException(
                        "Pending-identity remote emitted STATE before a vehicle path existed.");
                }

                continue;
            }

            string text;
            try
            {
                text =
                    new UTF8Encoding(false, true)
                        .GetString(result.Buffer);
            }
            catch (DecoderFallbackException)
            {
                continue;
            }

            if (text.StartsWith(
                    $"INFO|{lanId}|",
                    StringComparison.Ordinal) ||
                text.Contains(
                    $"|{displayName}|",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Pending-identity remote emitted INFO before a vehicle path existed.");
            }
        }
    }
    catch (OperationCanceledException)
        when (cts.IsCancellationRequested)
    {
    }
}

static async Task<string> ReceiveUntilTextAsync(
    UdpClient client,
    Func<string, bool> predicate,
    TimeSpan timeout)
{
    using var cts = new CancellationTokenSource(timeout);
    while (true)
    {
        var result = await client.ReceiveAsync(cts.Token);
        if (result.Buffer.Length == 0 ||
            result.Buffer[0] == OpenOmsiLanProtocol.StateMagic)
        {
            continue;
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(result.Buffer);
        }
        catch (DecoderFallbackException)
        {
            continue;
        }

        if (predicate(text))
        {
            return text;
        }
    }
}

static async Task<byte[]> ReceiveUntilBytesAsync(
    UdpClient client,
    Func<byte[], bool> predicate,
    TimeSpan timeout)
{
    using var cts = new CancellationTokenSource(timeout);
    while (true)
    {
        var result = await client.ReceiveAsync(cts.Token);
        if (predicate(result.Buffer))
        {
            return result.Buffer;
        }
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void Near(double actual, double expected, double tolerance, string label)
{
    if (!double.IsFinite(actual) ||
        Math.Abs(actual - expected) > tolerance)
    {
        throw new InvalidOperationException(
            $"{label}: expected {expected}, got {actual} (tol {tolerance}).");
    }
}
