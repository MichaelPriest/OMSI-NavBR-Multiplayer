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

var clockText = OpenOmsiLanProtocol.EncodeClock(
    new OpenOmsiLanWorld(
        "maps/Grundorf/global.cfg",
        "2026-10-07",
        43_210.5d,
        "weather.cfg",
        "autumn"),
    1.25d);
Require(
    OpenOmsiLanProtocol.TryDecodeClock(
        clockText,
        out var decodedClock),
    "CLOCK did not round-trip");
Require(
    decodedClock.World.Map == "maps/Grundorf/global.cfg" &&
    decodedClock.World.Date == "2026-10-07" &&
    Math.Abs(decodedClock.World.TimeSeconds - 43_210.5d) < 0.01d &&
    Math.Abs(decodedClock.Speed - 1.25d) < 0.01d,
    "CLOCK payload mismatch");

var carDesc = new OpenOmsiWorldDescription.Car(
    77,
    @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
    3,
    "76",
    "Rathaus Spandau");
var carDescText =
    OpenOmsiWorldDescriptionCodec.Encode(carDesc);
Require(
    OpenOmsiWorldDescriptionCodec.TryDecode(
        carDescText,
        out var decodedCarDesc) &&
    decodedCarDesc is OpenOmsiWorldDescription.Car decodedCar &&
    decodedCar.Id == 77 &&
    decodedCar.File ==
        "Vehicles/MAN_NL_NG/MAN_EN92_main.bus" &&
    decodedCar.Scheme == 3 &&
    decodedCar.Line == "76",
    "WORLD DESC car did not round-trip");
Require(
    !OpenOmsiWorldDescriptionCodec.TryDecode(
        "DESC|c|1|../../bad.bus|-||",
        out _),
    "WORLD DESC accepted a traversal path");
Require(
    OpenOmsiWorldDescriptionCodec.TryDecodeWant(
        "WANT|2|c77,p12,c16777215",
        out var wantRequester,
        out var wantedRefs) &&
    wantRequester == 2 &&
    wantedRefs.Count == 3 &&
    !wantedRefs[0].IsPerson &&
    wantedRefs[1].IsPerson,
    "WORLD WANT did not parse");



var worldFrame = new OpenOmsiWorldFrame(
    Sequence: ushort.MaxValue,
    HostMilliseconds: 123_456_789u,
    Cars:
    [
        new OpenOmsiWorldCarState(
            7,
            892_248.18,
            4_196_461.37,
            33.21,
            271.3f,
            -1.2f,
            0.4f,
            13.85f,
            -7.5f,
            2,
            true,
            true,
            -1),
        new OpenOmsiWorldCarState(
            OpenOmsiWorldCodec.MaxId,
            893_100.02,
            4_196_461.37,
            33.21,
            45f,
            0f,
            0f,
            0f,
            0f,
            0,
            false,
            false,
            0)
    ],
    People:
    [
        new OpenOmsiWorldPersonState(
            12,
            OpenOmsiWorldActivity.Sit,
            OpenOmsiWorldPersonPlaceKind.Foot,
            892_250.5,
            4_196_470.25,
            33.9,
            45f,
            0f,
            3_000_123_456L,
            3),
        new OpenOmsiWorldPersonState(
            13,
            OpenOmsiWorldActivity.Walk,
            OpenOmsiWorldPersonPlaceKind.Foot,
            892_240d,
            4_196_400d,
            34d,
            180f,
            1.4f),
        new OpenOmsiWorldPersonState(
            14,
            OpenOmsiWorldActivity.Sit,
            OpenOmsiWorldPersonPlaceKind.Vehicle,
            -0.62d,
            -8.4d,
            1.05d,
            180f,
            0f,
            VehicleId: 7,
            Seat: 12),
        new OpenOmsiWorldPersonState(
            15,
            OpenOmsiWorldActivity.Stand,
            OpenOmsiWorldPersonPlaceKind.PlayerBus,
            0.4d,
            2d,
            1d,
            90f,
            0f,
            VehicleId: 3,
            Seat: null)
    ],
    Lights:
    [
        new OpenOmsiWorldLightState(
            4711,
            63.45d,
            true)
    ],
    Gone:
    [
        (false, 3u),
        (true, 99u)
    ],
    ParkedComplete: true,
    ParkedMapIds:
    [
        242_685u,
        7u
    ]);

var worldPackets = OpenOmsiWorldCodec.Encode(worldFrame);
Require(worldPackets.Count == 1, "WORLD fixture unexpectedly fragmented");
Require(
    worldPackets.All(packetValue =>
        packetValue.Length <= OpenOmsiWorldCodec.MaxDatagramBytes),
    "WORLD exceeded datagram limit");
Require(
    OpenOmsiWorldCodec.TryDecode(
        worldPackets[0],
        out var decodedWorld),
    "WORLD did not decode");
Require(
    decodedWorld.Sequence == ushort.MaxValue &&
    decodedWorld.HostMilliseconds == 123_456_789u,
    "WORLD header mismatch");
Require(decodedWorld.Cars.Count == 2, "WORLD car count mismatch");
Near(decodedWorld.Cars[0].X, 892_248.18d, 0.006d, "WORLD car x");
Near(decodedWorld.Cars[0].Y, 4_196_461.37d, 0.006d, "WORLD car y");
Near(decodedWorld.Cars[0].Z, 33.21d, 0.006d, "WORLD car z");
Near(decodedWorld.Cars[0].HeadingDegrees, 271.3d, 0.05d, "WORLD car heading");
Near(decodedWorld.Cars[0].SpeedMetersPerSecond, 13.85d, 0.026d, "WORLD car speed");
Require(
    decodedWorld.Cars[0].TurnSignal == 2 &&
    decodedWorld.Cars[0].Brake &&
    decodedWorld.Cars[0].Lights &&
    decodedWorld.Cars[0].AtStation == -1,
    "WORLD car visual state mismatch");
Require(
    decodedWorld.People.Count == 4,
    $"WORLD people count mismatch: actual={decodedWorld.People.Count}; " +
    $"people={string.Join(",", decodedWorld.People.Select(person => $"{person.Id}:{person.PlaceKind}"))}");
Require(
    decodedWorld.People[0].WaitingStopObjectId == 3_000_123_456L &&
    decodedWorld.People[0].WaitingPlace == 3,
    "WORLD waiting person mismatch");
Require(
    decodedWorld.People[2].PlaceKind ==
        OpenOmsiWorldPersonPlaceKind.Vehicle &&
    decodedWorld.People[2].VehicleId == 7 &&
    decodedWorld.People[2].Seat == 12,
    "WORLD onboard person mismatch");
Require(
    decodedWorld.People[3].PlaceKind ==
        OpenOmsiWorldPersonPlaceKind.PlayerBus &&
    decodedWorld.People[3].VehicleId == 3 &&
    decodedWorld.People[3].Seat is null,
    "WORLD player-bus person mismatch");
Require(
    decodedWorld.Lights.Count == 1 &&
    decodedWorld.Lights[0].Held,
    "WORLD light mismatch");
Near(
    decodedWorld.Lights[0].CycleSeconds,
    63.45d,
    0.026d,
    "WORLD light cycle");
Require(
    decodedWorld.Gone.SequenceEqual(
        new[]
        {
            (IsPerson: false, Id: 3u),
            (IsPerson: true, Id: 99u)
        }),
    "WORLD gone list mismatch");
Require(
    decodedWorld.ParkedComplete == true &&
    decodedWorld.ParkedMapIds?.SequenceEqual(
        new uint[] { 242_685u, 7u }) == true,
    "WORLD parked list mismatch");

var busyWorld = new OpenOmsiWorldFrame(
    77,
    50_000u,
    Enumerable.Range(0, 150)
        .Select(index =>
            new OpenOmsiWorldCarState(
                (uint)index,
                892_000d + index * 5d,
                4_196_000d,
                30d,
                0f,
                0f,
                0f,
                8f,
                0f,
                0,
                false,
                false,
                0))
        .ToArray(),
    Enumerable.Range(0, 300)
        .Select(index =>
            new OpenOmsiWorldPersonState(
                (uint)(1000 + index),
                OpenOmsiWorldActivity.Walk,
                OpenOmsiWorldPersonPlaceKind.Foot,
                892_000d + index,
                4_196_000d,
                30d,
                0f,
                1.2f,
                index % 3 == 0 ? index : null,
                index % 3 == 0 ? (byte)1 : null))
        .ToArray(),
    Enumerable.Range(0, 10)
        .Select(index =>
            new OpenOmsiWorldLightState(index, 1d, false))
        .ToArray(),
    Array.Empty<(bool IsPerson, uint Id)>());

var busyPackets = OpenOmsiWorldCodec.Encode(busyWorld);
Require(busyPackets.Count > 1, "busy WORLD did not fragment");
Require(
    busyPackets.All(packetValue =>
        packetValue.Length <= OpenOmsiWorldCodec.MaxDatagramBytes),
    "fragmented WORLD exceeded datagram limit");
var busyDecoded = busyPackets
    .Select(packetValue =>
    {
        Require(
            OpenOmsiWorldCodec.TryDecode(
                packetValue,
                out var decodedPacket),
            "fragmented WORLD packet did not decode");
        return decodedPacket;
    })
    .ToArray();
Require(
    busyDecoded.Sum(item => item.Cars.Count) == 150,
    "fragmented WORLD lost cars");
Require(
    busyDecoded.Sum(item => item.People.Count) == 300,
    "fragmented WORLD lost people");
Require(
    busyDecoded.Sum(item => item.Lights.Count) == 10,
    "fragmented WORLD lost lights");

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

    var multiAddressCode = new OpenOmsiSessionCode(
        OpenOmsiLanProtocol.ProtocolVersion,
        IPAddress.Parse("203.0.113.1"),
        checked((ushort)host.Port.Value),
        host.SessionId)
    {
        Addresses =
        [
            IPAddress.Parse("203.0.113.1"),
            IPAddress.Loopback
        ]
    }.Encode();

    Require(
        OpenOmsiSessionCode.TryDecode(
            multiAddressCode,
            out var decodedMultiCode),
        "multi-address session code did not decode");
    Require(
        decodedMultiCode.EffectiveAddresses.Count == 2 &&
        decodedMultiCode.EffectiveAddresses[0].Equals(
            IPAddress.Parse("203.0.113.1")) &&
        decodedMultiCode.EffectiveAddresses[1].Equals(
            IPAddress.Loopback),
        "multi-address session code lost endpoint order");

    await using (var multiClient = new OpenOmsiLanPeerSession())
    {
        await multiClient.JoinByCodeAsync(
            multiAddressCode,
            world,
            "Multi Client",
            @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus");

        Require(
            multiClient.LocalPlayerId >= 2,
            "multi-address client did not join through the reachable candidate");
        Require(
            multiClient.SessionId == host.SessionId,
            "multi-address client joined the wrong session");
        Require(
            string.Equals(
                multiClient.SessionCode,
                multiAddressCode,
                StringComparison.Ordinal),
            "multi-address client did not preserve the canonical invite code");
    }

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

    var clientSawDescription =
        new TaskCompletionSource<OpenOmsiWorldDescription>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var hostSawWant =
        new TaskCompletionSource<
            (ushort PlayerId, IReadOnlyList<OpenOmsiWorldEntityRef> Refs)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var hostSawClientDescription =
        new TaskCompletionSource<
            (ushort PlayerId, OpenOmsiWorldDescription.Person Description)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    client.WorldDescriptionReceived += description =>
        clientSawDescription.TrySetResult(description);
    host.WorldDescriptionsRequested += (playerId, refs) =>
        hostSawWant.TrySetResult((playerId, refs));
    host.ClientWorldDescriptionReceived +=
        (playerId, description) =>
            hostSawClientDescription.TrySetResult(
                (playerId, description));

    await host.SendWorldDescriptionAsync(
        client.LocalPlayerId,
        carDesc);
    var descAtClient =
        await clientSawDescription.Task.WaitAsync(
            TimeSpan.FromSeconds(3));
    Require(
        descAtClient is OpenOmsiWorldDescription.Car receivedCar &&
        receivedCar.Id == 77 &&
        receivedCar.File ==
            "Vehicles/MAN_NL_NG/MAN_EN92_main.bus",
        "host DESC did not reach client");

    await client.RequestWorldDescriptionsAsync(
        [
            new OpenOmsiWorldEntityRef(false, 77),
            new OpenOmsiWorldEntityRef(true, 12)
        ]);
    var wantAtHost =
        await hostSawWant.Task.WaitAsync(
            TimeSpan.FromSeconds(3));
    Require(
        wantAtHost.PlayerId == client.LocalPlayerId &&
        wantAtHost.Refs.Count == 2 &&
        wantAtHost.Refs[0].Id == 77 &&
        wantAtHost.Refs[1].IsPerson,
        "client WANT did not reach host");

    await client.SendWorldDescriptionUpAsync(
        new OpenOmsiWorldDescription.Person(
            12,
            @"Humans\axyz.hum"));
    var descUpAtHost =
        await hostSawClientDescription.Task.WaitAsync(
            TimeSpan.FromSeconds(3));
    Require(
        descUpAtHost.PlayerId == client.LocalPlayerId &&
        descUpAtHost.Description.Id == 12 &&
        descUpAtHost.Description.File ==
            "Humans/axyz.hum",
        "client person DESC did not reach host");

    Require(
        client.SessionId == host.SessionId,
        "session-code join did not keep the requested session id");

    Require(
        string.Equals(
            client.SessionCode,
            loopbackCode,
            StringComparison.Ordinal),
        "direct v6 client did not expose the canonical OMSI session code");


    await using (var wrongSessionClient = new OpenOmsiLanPeerSession())
    {
        var wrongSessionCode = new OpenOmsiSessionCode(
            OpenOmsiLanProtocol.ProtocolVersion,
            IPAddress.Loopback,
            checked((ushort)host.Port.Value),
            (host.SessionId ^ 1UL) & 0x0000_FFFF_FFFF_FFFFUL)
            .Encode();

        var rejected = false;
        try
        {
            await wrongSessionClient.JoinByCodeAsync(
                wrongSessionCode,
                world,
                "Wrong Session",
                @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus");
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(
            rejected,
            "host accepted a HELLO carrying the wrong session id");
    }

    var clientSawWorld =
        new TaskCompletionSource<OpenOmsiWorldFrame>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var hostSawClientWorld =
        new TaskCompletionSource<(ushort PlayerId, OpenOmsiWorldFrame Frame)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    client.WorldFrameReceived += frameValue =>
        clientSawWorld.TrySetResult(frameValue);
    host.ClientWorldFrameReceived += (playerId, frameValue) =>
        hostSawClientWorld.TrySetResult((playerId, frameValue));

    await host.PublishWorldAsync(worldFrame);
    var worldAtClient =
        await clientSawWorld.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Require(
        worldAtClient.Cars.Count == 2 &&
        worldAtClient.People.Count == 4 &&
        worldAtClient.Lights.Count == 1,
        "host WORLD did not reach client intact");

    var attemptedClientWorld = new OpenOmsiWorldFrame(
        9,
        222u,
        [
            new OpenOmsiWorldCarState(
                999,
                100d,
                100d,
                0d,
                0f,
                0f,
                0f,
                5f,
                0f,
                0,
                false,
                false,
                0)
        ],
        [
            new OpenOmsiWorldPersonState(
                901,
                OpenOmsiWorldActivity.Walk,
                OpenOmsiWorldPersonPlaceKind.Foot,
                100d,
                100d,
                0d,
                0f,
                1f)
        ],
        [
            new OpenOmsiWorldLightState(
                1234,
                2d,
                false)
        ],
        [
            (IsPerson: false, Id: 999u),
            (IsPerson: true, Id: 901u)
        ],
        true,
        [55u]);

    await client.PublishWorldAsync(attemptedClientWorld);
    var clientWorldAtHost =
        await hostSawClientWorld.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Require(
        clientWorldAtHost.PlayerId == client.LocalPlayerId,
        "client WORLD was attributed to wrong peer");
    Require(
        clientWorldAtHost.Frame.Cars.Count == 0 &&
        clientWorldAtHost.Frame.Lights.Count == 0,
        "host accepted client authority over cars/lights");
    Require(
        clientWorldAtHost.Frame.People.Count == 1 &&
        clientWorldAtHost.Frame.Gone.All(item => item.IsPerson) &&
        clientWorldAtHost.Frame.ParkedComplete is null,
        "client WORLD sanitation mismatch");

    var hostSawClaim =
        new TaskCompletionSource<
            (ushort PlayerId, IReadOnlyList<uint> People)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var clientSawGrant =
        new TaskCompletionSource<IReadOnlyList<uint>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var clientSawDeny =
        new TaskCompletionSource<IReadOnlyList<uint>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    host.WorldPeopleClaimed += (playerId, people) =>
        hostSawClaim.TrySetResult((playerId, people));
    client.WorldPeopleClaimResult += (people, granted) =>
    {
        if (granted)
        {
            clientSawGrant.TrySetResult(people);
        }
        else
        {
            clientSawDeny.TrySetResult(people);
        }
    };

    await client.ClaimWorldPeopleAsync(
        new uint[] { 12u, 13u });
    var claimAtHost =
        await hostSawClaim.Task.WaitAsync(
            TimeSpan.FromSeconds(3));
    Require(
        claimAtHost.PlayerId == client.LocalPlayerId &&
        claimAtHost.People.SequenceEqual(
            new uint[] { 12u, 13u }),
        "CLAIM client→host payload mismatch");

    await host.AnswerWorldPeopleClaimAsync(
        client.LocalPlayerId,
        new uint[] { 12u },
        new uint[] { 13u });
    var grantAtClient =
        await clientSawGrant.Task.WaitAsync(
            TimeSpan.FromSeconds(3));
    var denyAtClient =
        await clientSawDeny.Task.WaitAsync(
            TimeSpan.FromSeconds(3));
    Require(
        grantAtClient.SequenceEqual(new uint[] { 12u }) &&
        denyAtClient.SequenceEqual(new uint[] { 13u }),
        "GRANT/DENY host→client payload mismatch");

    var clientState = OpenOmsiLanVehicleState.Empty(client.LocalPlayerId, 1) with
    {
        Flags = OpenOmsiLanProtocol.FlagVehicle |
                OpenOmsiLanProtocol.FlagEngine,
        X = 100d,
        Y = 200d,
        Z = 3d,
        HeadingDegrees = 90f,
        SpeedKph = 32f,
        // Exercise physical articulated bus parts and real visual variables
        // over the host/client UDP session, not only the local codec.
        RearSections = [new OpenOmsiLanPartPose(94d, 200.4d, 3.1d, 88f)],
        Doors = [1f, 0.5f],
        Lamps = [1f, 0.25f],
        Switches = [1f, -1f],
        Values = [1200f, 0.75f],
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
    Require(clientAtHost.RearSections.Count == 1,
        "peer client articulated rear section missing");
    Near(clientAtHost.RearSections[0].X, 94d, 0.01,
        "peer client articulated section x");
    Require(clientAtHost.Doors.Count == 2 &&
            clientAtHost.Lamps.Count == 2 &&
            clientAtHost.Switches.Count == 2 &&
            clientAtHost.Values.Count == 2,
        "peer client visual SyncTable cardinality missing");
    Near(clientAtHost.Doors[1], 0.5f, 0.01,
        "peer client middle door");
    Near(clientAtHost.Lamps[1], 0.25f, 0.01,
        "peer client lamp intensity");
    Require(clientAtHost.Switches[1] == -1f,
        "peer client switch");
    Near(clientAtHost.Values[0], 1200f, 0.5,
        "peer client analog value");

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
        RearSections = [new OpenOmsiLanPartPose(106d, 220.7d, 4.1d, 179f)],
        Doors = [0f, 1f],
        Lamps = [0f, 1f],
        Switches = [-1f, 1f],
        Values = [650f, 1f],
        Walker = new OpenOmsiLanWalker(
            111d, 221d, 4.2d, 185f, 1.5f, 185f,
            false, null, null, null),
        SentMilliseconds = 75
    };
    await host.PublishStateAsync(hostState);

    var hostAtClient = await clientSawHostState.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Near(hostAtClient.Y, 220d, 0.01, "peer host y");
    Require(hostAtClient.Walker is not null,
        "peer host walker/RP state missing");
    Require(hostAtClient.RearSections.Count == 1,
        "peer host articulated rear section missing");
    Near(hostAtClient.RearSections[0].HeadingDegrees, 179f, 0.01,
        "peer host articulated section heading");
    Require(hostAtClient.Doors.Count == 2 &&
            hostAtClient.Lamps.Count == 2 &&
            hostAtClient.Switches.Count == 2 &&
            hostAtClient.Values.Count == 2,
        "peer host visual SyncTable cardinality missing");
    Near(hostAtClient.Doors[1], 1f, 0.01, "peer host door");
    Near(hostAtClient.Lamps[1], 1f, 0.01, "peer host lamp");
    Require(hostAtClient.Switches[0] == -1f, "peer host switch");
    Near(hostAtClient.Values[0], 650f, 0.5, "peer host analog value");

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

    var hostSawClientChat =
        new TaskCompletionSource<(ushort Id, string Name, string Text)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var clientSawHostChat =
        new TaskCompletionSource<(ushort Id, string Name, string Text)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    host.ChatReceived += (id, name, textValue) =>
    {
        if (id == client.LocalPlayerId)
        {
            hostSawClientChat.TrySetResult((id, name, textValue));
        }
    };
    client.ChatReceived += (id, name, textValue) =>
    {
        if (id == 1)
        {
            clientSawHostChat.TrySetResult((id, name, textValue));
        }
    };

    await client.PublishInfoAsync(
        new OpenOmsiLanVehicleInfo(
            client.LocalPlayerId,
            "Client",
            @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus",
            string.Empty,
            "76",
            "Rathaus",
            12d,
            2.5d,
            -2d,
            0u,
            "76/2",
            [],
            null,
            []));

    await using (var lateClient = new OpenOmsiLanPeerSession())
    {
        var lateSawHostInfo =
            new TaskCompletionSource<OpenOmsiLanVehicleInfo>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var lateSawClientInfo =
            new TaskCompletionSource<OpenOmsiLanVehicleInfo>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        lateClient.RemoteInfoReceived += infoValue =>
        {
            if (infoValue.PlayerId == 1)
            {
                lateSawHostInfo.TrySetResult(infoValue);
            }
            else if (infoValue.PlayerId == client.LocalPlayerId)
            {
                lateSawClientInfo.TrySetResult(infoValue);
            }
        };

        await lateClient.JoinByCodeAsync(
            loopbackCode,
            world,
            "Late Client",
            @"Vehicles\MAN_NL_NG\MAN_EN92_main.bus");

        var replayedHostInfo =
            await lateSawHostInfo.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var replayedClientInfo =
            await lateSawClientInfo.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Require(
            replayedHostInfo.Name == "Host" &&
            replayedHostInfo.Tour == "76/1",
            "late joiner did not receive cached host INFO");
        Require(
            replayedClientInfo.Name == "Client" &&
            replayedClientInfo.Tour == "76/2",
            "late joiner did not receive cached peer INFO");
    }

    await client.SendChatAsync("Olá pelo v6");
    var chatAtHost =
        await hostSawClientChat.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Require(
        chatAtHost.Text == "Olá pelo v6" &&
        chatAtHost.Name == "Client",
        "CHAT client→host did not preserve text/name");

    await host.SendChatAsync("Resposta do host");
    var chatAtClient =
        await clientSawHostChat.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Require(
        chatAtClient.Text == "Resposta do host",
        "SAY host→client did not preserve text");

    var hostSawClientCommand =
        new TaskCompletionSource<(ushort Id, string Command)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    var clientSawHostCommand =
        new TaskCompletionSource<(ushort Id, string Command)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    host.CommandReceived += (id, command) =>
    {
        if (id == client.LocalPlayerId)
        {
            hostSawClientCommand.TrySetResult((id, command));
        }
    };
    client.CommandReceived += (id, command) =>
    {
        if (id == 1)
        {
            clientSawHostCommand.TrySetResult((id, command));
        }
    };

    await client.SendCommandAsync(1, "cco:hold-position");
    var commandAtHost =
        await hostSawClientCommand.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Require(
        commandAtHost.Command == "cco:hold-position",
        "CMD client→host payload mismatch");

    await host.SendCommandAsync(
        client.LocalPlayerId,
        "cco:resume");
    var commandAtClient =
        await clientSawHostCommand.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Require(
        commandAtClient.Command == "cco:resume",
        "CMD host→client payload mismatch");

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
    $"openOMSI LAN v6 smoke passed: STATE {packet.Length} bytes + heartbeat + clamps + INFO/HELLO + host/join + session code + walker + fragmented VARS + CLOCK + WORLD DESC/WANT + CLAIM/GRANT/DENY.");

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
