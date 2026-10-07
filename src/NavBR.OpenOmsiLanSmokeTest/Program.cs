using System.Net;
using NavBR.Shared.OpenOmsi;

var state = new OpenOmsiLanVehicleState(
    PlayerId: 3,
    Sequence: 4711,
    Flags:
        OpenOmsiLanProtocol.FlagVehicle |
        OpenOmsiLanProtocol.FlagEngine |
        OpenOmsiLanProtocol.FlagElectrics |
        OpenOmsiLanProtocol.FlagHorn,
    X: 892_248.37,
    Y: 4_196_461.12,
    Z: 33.21,
    HeadingDegrees: 271.3f,
    PitchDegrees: -1.25f,
    BankDegrees: 0.4f,
    SpeedKph: -7.35f,
    SteeringDegrees: 12.5f,
    HeadLightLevel: 2,
    InteriorLightLevel: 3,
    TurnSignal: 1,
    EngineRpm: 1850f,
    Throttle: 0.6f,
    Brake: 0f,
    Passengers: 37,
    Doors: [1f, 0.8f, 0f, 0f, 0.2f],
    Suspension: [-0.105f, -0.1f, -0.02f, 0f],
    RearSections:
    [
        new OpenOmsiLanPartPose(
            892_240.0,
            4_196_461.5,
            33.3,
            268f)
    ],
    Lamps: [1f, 0f, 1f, 2f / 3f],
    Switches: [1f, 0f, -1f, 3f],
    Values: [1850f, 0.6f, 412.5f],
    Walker: new OpenOmsiLanWalker(
        892_248.7,
        4_196_461.8,
        34.2,
        10f,
        1.4f,
        100f,
        false,
        null,
        null,
        null),
    SentMilliseconds: 1234);

var packet = OpenOmsiLanStateCodec.Encode(state);
Require(packet.Length <= 90, $"STATE unexpectedly large: {packet.Length}");
Require(packet[0] == OpenOmsiLanProtocol.StateMagic, "STATE magic mismatch");
Require(packet[1] == OpenOmsiLanProtocol.ProtocolVersion, "STATE protocol mismatch");
Require(
    OpenOmsiLanStateCodec.TryDecode(packet, out var decoded),
    "STATE did not decode");

Require(decoded.PlayerId == 3, "player id mismatch");
Require(decoded.Sequence == 4711, "sequence mismatch");
Require(decoded.Flags == state.Flags, "flags mismatch");
Near(decoded.X, state.X, 0.006, "x");
Near(decoded.Y, state.Y, 0.006, "y");
Near(decoded.Z, state.Z, 0.006, "z");
Near(decoded.HeadingDegrees, state.HeadingDegrees, 0.01, "heading");
Near(decoded.PitchDegrees, state.PitchDegrees, 0.006, "pitch");
Near(decoded.BankDegrees, state.BankDegrees, 0.006, "bank");
Near(decoded.SpeedKph, state.SpeedKph, 0.03, "speed");
Near(decoded.SteeringDegrees, state.SteeringDegrees, 0.03, "steering");
Require(decoded.HeadLightLevel == 2, "headlight mismatch");
Require(decoded.InteriorLightLevel == 3, "interior light mismatch");
Require(decoded.TurnSignal == 1, "turn signal mismatch");
Near(decoded.EngineRpm, 1850, 0.01, "rpm");
Near(decoded.Throttle, 0.6, 0.02, "throttle");
Near(decoded.Brake, 0, 0.001, "brake");
Require(decoded.Passengers == 37, "passengers mismatch");
Require(decoded.Doors.Count == 5, "door count mismatch");
for (var index = 0; index < decoded.Doors.Count; index++)
{
    Near(decoded.Doors[index], state.Doors[index], 0.003, $"door {index}");
}

Require(decoded.Suspension.Count == 4, "wheel suspension count mismatch");
for (var index = 0; index < decoded.Suspension.Count; index++)
{
    Near(decoded.Suspension[index], state.Suspension[index], 0.003, $"suspension {index}");
}

Require(decoded.RearSections.Count == 1, "rear section count mismatch");
Near(decoded.RearSections[0].X, 892_240.0, 0.006, "rear x");
Near(decoded.RearSections[0].Y, 4_196_461.5, 0.006, "rear y");
Near(decoded.RearSections[0].HeadingDegrees, 268, 0.01, "rear heading");
Require(decoded.Lamps.Count == 4, "lamp count mismatch");
Require(decoded.Switches.SequenceEqual(state.Switches), "switch values mismatch");
Near(decoded.Values[0], 1850, 0.01, "value 0");
Near(decoded.Values[1], 0.6, 0.001, "value 1");
Near(decoded.Values[2], 412.5, 0.01, "value 2");
Require(decoded.SentMilliseconds == 1234, "sender clock mismatch");
Require(decoded.Walker is not null, "walker was lost");
Near(decoded.Walker!.HeadingDegrees, 10, 0.01, "walker heading");
Near(decoded.Walker.CourseDegrees, 100, 0.01, "walker course");

var heartbeat = OpenOmsiLanStateCodec.Encode(
    OpenOmsiLanVehicleState.Empty(9, 1) with
    {
        SentMilliseconds = 55
    });
Require(
    heartbeat.Length <= OpenOmsiLanProtocol.StateHeaderBytes + 7,
    "heartbeat is too large");
Require(
    OpenOmsiLanStateCodec.TryDecode(heartbeat, out var decodedHeartbeat),
    "heartbeat did not decode");
Require(
    (decodedHeartbeat.Flags & OpenOmsiLanProtocol.FlagVehicle) == 0,
    "heartbeat unexpectedly contains a vehicle");

var clamped = state with
{
    Sequence = 0,
    X = double.NaN,
    Y = 1.0e12,
    HeadingDegrees = float.PositiveInfinity,
    SpeedKph = 5000f,
    EngineRpm = -3f,
    HeadLightLevel = 9,
    Passengers = 255,
    Doors = Enumerable.Repeat(2f, 20).ToArray(),
    Lamps = Enumerable.Repeat(0.5f, 400).ToArray(),
    Values = [float.NaN, 1.0e30f],
    Walker = null
};
Require(
    OpenOmsiLanStateCodec.TryDecode(
        OpenOmsiLanStateCodec.Encode(clamped),
        out var decodedClamped),
    "clamped STATE did not decode");
Require(decodedClamped.X == 0d, "NaN x was not normalized");
Near(decodedClamped.Y, 21_474_836.47, 0.01, "clamped y");
Require(decodedClamped.HeadingDegrees == 0f, "invalid heading was not normalized");
Near(decodedClamped.SpeedKph, 409.55, 0.01, "clamped speed");
Require(decodedClamped.HeadLightLevel == 3, "headlights were not clamped");
Require(decodedClamped.Doors.Count == OpenOmsiLanProtocol.MaxDoors, "doors were not capped");
Require(decodedClamped.Lamps.Count == OpenOmsiLanProtocol.MaxLamps, "lamps were not capped");
Require(decodedClamped.Values.Count == 2, "value count mismatch");
Require(decodedClamped.Values[0] == 0f, "NaN half float was not normalized");
Require(decodedClamped.Values[1] == 65_504f, "half float was not clamped");

Require(!OpenOmsiLanStateCodec.TryDecode("POSE|1|a|b"u8, out _), "text packet decoded as STATE");
var oversize = packet.Concat(new byte[OpenOmsiLanProtocol.MaxStateBytes + 1]).ToArray();
Require(!OpenOmsiLanStateCodec.TryDecode(oversize, out _), "oversize STATE was accepted");
var wrongProtocol = packet.ToArray();
wrongProtocol[1] = 5;
Require(!OpenOmsiLanStateCodec.TryDecode(wrongProtocol, out _), "wrong LAN protocol was accepted");

Require(OpenOmsiLanStateCodec.IsSequenceNewer(0, ushort.MaxValue), "wrapped sequence comparison regressed");
Require(!OpenOmsiLanStateCodec.IsSequenceNewer(10, 10), "equal sequence counted as newer");

var info = new OpenOmsiLanVehicleInfo(
    7,
    "Michael",
    @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
    "",
    "76",
    "Rathaus Spandau",
    12.2,
    2.5,
    -1.2,
    0,
    "76/1",
    ["76", "RATHAUS"],
    @"Humans\axyz.hum",
    []);
var infoText = OpenOmsiLanProtocol.EncodeInfo(info);
Require(
    OpenOmsiLanProtocol.TryDecodeInfo(infoText, out var decodedInfo),
    "INFO did not round-trip");
Require(decodedInfo.PlayerId == 7, "INFO id mismatch");
Require(
    decodedInfo.VehiclePath == "Vehicles/MAN_NL_NG/MAN_EN92_main.bus",
    "INFO vehicle path mismatch");
Require(decodedInfo.Line == "76", "INFO line mismatch");
Require(decodedInfo.Destination == "Rathaus Spandau", "INFO destination mismatch");
Require(decodedInfo.DisplayTexts.SequenceEqual(["76", "RATHAUS"]), "INFO display texts mismatch");

var helloText =
    "HELLO|6|-|Michael|Vehicles/MAN_NL_NG/MAN_EN92_main.bus|maps/Grundorf/global.cfg|2026-10-01|36000||autumn|0011223344556677";
Require(
    OpenOmsiLanProtocol.TryDecodeHello(helloText, out var hello),
    "HELLO did not parse");
Require(hello.Protocol == 6, "HELLO protocol mismatch");
Require(hello.RequestedSession is null, "HELLO direct join should not require a session id");
Require(hello.World.Map == "maps/Grundorf/global.cfg", "HELLO map mismatch");
Require(hello.Nonce == 0x0011223344556677UL, "HELLO nonce mismatch");



var codeFixture = new OpenOmsiSessionCode(
    OpenOmsiLanProtocol.ProtocolVersion,
    IPAddress.Loopback,
    27015,
    0x0011_2233_4455UL);
var encodedCode = codeFixture.Encode();
Require(encodedCode.StartsWith("OMSI-", StringComparison.Ordinal), "session code prefix missing");
Require(OpenOmsiSessionCode.TryDecode(encodedCode, out var decodedCode), "session code did not decode");
Require(decodedCode.Protocol == OpenOmsiLanProtocol.ProtocolVersion, "session code protocol mismatch");
Require(decodedCode.Address.Equals(IPAddress.Loopback), "session code address mismatch");
Require(decodedCode.Port == 27015, "session code port mismatch");
Require(decodedCode.Session == 0x0011_2233_4455UL, "session code session mismatch");

await using (var host = new OpenOmsiLanPeerSession())
await using (var client = new OpenOmsiLanPeerSession())
{
    var world = new OpenOmsiLanWorld(
        "maps/Grundorf/global.cfg",
        "2026-10-07",
        36_000d,
        string.Empty,
        "autumn");

    await host.StartHostAsync(world);
    Require(host.Port is not null, "host did not bind an openOMSI LAN port");

    var hostSawClientState =
        new TaskCompletionSource<OpenOmsiLanVehicleState>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var clientSawHostState =
        new TaskCompletionSource<OpenOmsiLanVehicleState>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    host.RemoteStateReceived += remote =>
    {
        if (remote.PlayerId >= 2)
        {
            hostSawClientState.TrySetResult(remote);
        }
    };
    client.RemoteStateReceived += remote =>
    {
        if (remote.PlayerId == 1)
        {
            clientSawHostState.TrySetResult(remote);
        }
    };

    var loopbackCode = new OpenOmsiSessionCode(
        OpenOmsiLanProtocol.ProtocolVersion,
        IPAddress.Loopback,
        checked((ushort)host.Port.Value),
        host.SessionId).Encode();
    await client.JoinByCodeAsync(
        loopbackCode,
        world,
        "Client",
        @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus");

    Require(client.LocalPlayerId >= 2, "client did not receive a LAN player id");

    var clientState = OpenOmsiLanVehicleState.Empty(client.LocalPlayerId, 1) with
    {
        Flags = OpenOmsiLanProtocol.FlagVehicle |
                OpenOmsiLanProtocol.FlagEngine,
        X = 100d,
        Y = 200d,
        Z = 3d,
        HeadingDegrees = 90f,
        SpeedKph = 32f,
        Walker = new OpenOmsiLanWalker(
            101d,
            201d,
            3.2d,
            95f,
            1.4f,
            95f,
            false,
            null,
            null,
            null),
        SentMilliseconds = 50
    };
    await client.PublishStateAsync(clientState);

    var clientAtHost = await hostSawClientState.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Near(clientAtHost.X, 100d, 0.01, "peer client x");
    Require(clientAtHost.Walker is not null, "peer client walker/RP state missing");

    await host.PublishInfoAsync(
        new OpenOmsiLanVehicleInfo(
            1,
            "Host",
            @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
            string.Empty,
            "76",
            "Rathaus",
            12d,
            2.5d,
            -2d,
            0u,
            "76/1",
            [],
            null,
            []));

    var hostState = OpenOmsiLanVehicleState.Empty(1, 1) with
    {
        Flags = OpenOmsiLanProtocol.FlagVehicle |
                OpenOmsiLanProtocol.FlagEngine,
        X = 110d,
        Y = 220d,
        Z = 4d,
        HeadingDegrees = 180f,
        SpeedKph = 20f,
        SentMilliseconds = 75
    };
    await host.PublishStateAsync(hostState);

    var hostAtClient = await clientSawHostState.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Near(hostAtClient.Y, 220d, 0.01, "peer host y");

    var nearby = await client.RequestNearAsync(
        new OpenOmsiLanFootprint(
            110d,
            220d,
            4d,
            180d,
            12d,
            2.5d));
    Require(nearby.Count >= 1, "PLACE/NEAR returned no host footprint");
    var hostFootprint = nearby[0];
    Near(hostFootprint.X, 110d, 0.05, "NEAR host footprint x");
    Near(hostFootprint.Y, 222d, 0.05, "NEAR host footprint box offset y");
    Near(hostFootprint.LengthMeters, 12d, 0.05, "NEAR host footprint length");
    Near(hostFootprint.WidthMeters, 2.5d, 0.05, "NEAR host footprint width");

    var clientSawHostVars =
        new TaskCompletionSource<OpenOmsiVarsFrame>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    client.RemoteVarsReceived += vars =>
    {
        if (vars.PlayerId == 1)
        {
            clientSawHostVars.TrySetResult(vars);
        }
    };

    var varsSender = new OpenOmsiVarsSender();
    var varsPackets = varsSender.Tick(
        1,
        0xA1B2C3D4u,
        new ushort[] { 10, 11, 12 },
        new float[] { 1f, 0.5f, -2.25f },
        new ushort[] { 3 },
        new[] { "Betriebsfahrt" },
        0.1f);
    Require(varsPackets.Count > 0, "VARS sender produced no datagrams");

    foreach (var varsPacket in varsPackets)
    {
        Require(
            OpenOmsiVarsCodec.TryDecode(varsPacket, out var decodedVars),
            "VARS datagram did not decode");
        await host.PublishVarsAsync(decodedVars);
    }

    var varsAtClient = await clientSawHostVars.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Require(varsAtClient.TableHash == 0xA1B2C3D4u, "VARS table hash mismatch");
    Require(varsAtClient.Floats.Any(item => item.Index == 10 && Math.Abs(item.Value - 1f) < 0.001f),
        "VARS float did not cross host/client session");


    var fragmentedFloats = Enumerable.Range(0, 240)
        .Select(i => ((ushort)(300 + i), (float)i / 10f))
        .ToArray();
    var fragmentedStrings = Enumerable.Range(0, 18)
        .Select(i => (
            (ushort)(70 + i),
            $"route-{i:D2}-" + new string('x', 90)))
        .ToArray();
    var receivedFloatIds = new HashSet<ushort>();
    var receivedStringIds = new HashSet<ushort>();
    var fragmentSync = new object();
    var fragmentedComplete =
        new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    client.RemoteVarsReceived += vars =>
    {
        if (vars.PlayerId != 1 ||
            vars.TableHash != 0xBEEFF00Du)
        {
            return;
        }

        lock (fragmentSync)
        {
            foreach (var item in vars.Floats)
            {
                receivedFloatIds.Add(item.Index);
            }
            foreach (var item in vars.Strings)
            {
                receivedStringIds.Add(item.Index);
            }

            if (receivedFloatIds.Count == fragmentedFloats.Length &&
                receivedStringIds.Count == fragmentedStrings.Length)
            {
                fragmentedComplete.TrySetResult(true);
            }
        }
    };

    await host.PublishVarsAsync(
        new OpenOmsiVarsFrame(
            1,
            0xBEEFF00Du,
            fragmentedFloats,
            fragmentedStrings));

    await fragmentedComplete.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Require(
        fragmentedFloats.All(item => receivedFloatIds.Contains(item.Item1)),
        "fragmented VARS lost float ids");
    Require(
        fragmentedStrings.All(item => receivedStringIds.Contains(item.Item1)),
        "fragmented VARS lost string ids");
}

Console.WriteLine(
    $"openOMSI LAN v6 smoke passed: STATE {packet.Length} bytes + heartbeat + clamps + INFO/HELLO + host/join + session code + walker + fragmented VARS.");

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
