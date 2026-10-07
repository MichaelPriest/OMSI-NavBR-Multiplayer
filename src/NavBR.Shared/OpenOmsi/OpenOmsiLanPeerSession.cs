using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace NavBR.Shared.OpenOmsi;

/// <summary>
/// Direct NavBR multiplayer transport built on the public openOMSI LAN v6 wire format.
/// NavBR services such as CCO/company remain separate; vehicle/walker motion uses this path.
/// </summary>
public sealed class OpenOmsiLanPeerSession : IAsyncDisposable
{
    private const int DefaultPort = 27015;
    private const int PortAttempts = 10;
    private static readonly TimeSpan PeerTimeout = TimeSpan.FromSeconds(15);

    private readonly object _sync = new();
    private readonly ConcurrentDictionary<ushort, Peer> _peers = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _maintenanceTask;
    private IPEndPoint? _hostEndpoint;
    private TaskCompletionSource<bool>? _joinTcs;
    private TaskCompletionSource<IReadOnlyList<OpenOmsiLanFootprint>>? _nearTcs;
    private string? _joinHello;
    private DateTimeOffset _lastHostPacketUtc;
    private DateTimeOffset _lastLocalStateUtc;
    private DateTimeOffset? _hostLostAtUtc;
    private ushort _heartbeatSequence;
    private ushort _nextPlayerId = 2;
    private ulong _sessionId;
    private ulong? _requestedSessionId;
    private OpenOmsiLanVehicleInfo? _localInfo;
    private OpenOmsiLanVehicleState? _localState;

    public bool IsHost { get; private set; }
    public bool IsRunning => _udp is not null;
    public ushort LocalPlayerId { get; private set; }
    public int? Port => (_udp?.Client.LocalEndPoint as IPEndPoint)?.Port;
    public ulong SessionId => _sessionId;
    public string? SessionCode
    {
        get
        {
            if (!IsHost || Port is not int port)
            {
                return null;
            }

            var address = ResolveAdvertiseAddress();
            return new OpenOmsiSessionCode(
                OpenOmsiLanProtocol.ProtocolVersion,
                address,
                checked((ushort)port),
                _sessionId).Encode();
        }
    }

    public OpenOmsiLanWorld World { get; private set; } =
        new(string.Empty, string.Empty, 0d, string.Empty, string.Empty);

    public event Action<OpenOmsiLanVehicleInfo>? RemoteInfoReceived;
    public event Action<OpenOmsiLanVehicleState>? RemoteStateReceived;
    public event Action<OpenOmsiVarsFrame>? RemoteVarsReceived;
    public event Action<OpenOmsiWorldFrame>? WorldFrameReceived;
    public event Action<OpenOmsiLanClock>? ClockReceived;
    public event Action<OpenOmsiWorldDescription>? WorldDescriptionReceived;
    public event Action<ushort, IReadOnlyList<OpenOmsiWorldEntityRef>>?
        WorldDescriptionsRequested;
    public event Action<ushort, OpenOmsiWorldDescription.Person>?
        ClientWorldDescriptionReceived;
    public event Action<ushort, IReadOnlyList<uint>>?
        WorldPeopleClaimed;
    public event Action<IReadOnlyList<uint>, bool>?
        WorldPeopleClaimResult;
    public event Action<ushort, OpenOmsiWorldFrame>? ClientWorldFrameReceived;
    public event Action<ushort, string, string>? ChatReceived;
    public event Action<ushort, string>? CommandReceived;
    public event Action<ushort>? RemoteLeft;
    public event Action<string>? NoteReceived;

    public async Task StartHostAsync(
        OpenOmsiLanWorld world,
        CancellationToken cancellationToken = default)
    {
        ThrowIfRunning();
        World = world;
        IsHost = true;
        LocalPlayerId = 1;
        _sessionId = CreateSessionId();
        _udp = BindHost();
        StartLoops();
        await Task.CompletedTask;
    }

    public async Task JoinByCodeAsync(
        string sessionCode,
        OpenOmsiLanWorld world,
        string displayName,
        string? vehiclePath,
        CancellationToken cancellationToken = default)
    {
        if (!OpenOmsiSessionCode.TryDecode(sessionCode, out var decoded) ||
            decoded.Protocol != OpenOmsiLanProtocol.ProtocolVersion)
        {
            throw new ArgumentException(
                "Invalid or incompatible openOMSI session code.",
                nameof(sessionCode));
        }

        await JoinAsyncCore(
            new IPEndPoint(decoded.Address, decoded.Port),
            world,
            displayName,
            vehiclePath,
            decoded.Session,
            cancellationToken);
    }

    public static async Task<IPEndPoint?> DiscoverHostAsync(
        CancellationToken cancellationToken = default)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        udp.EnableBroadcast = true;
        var message = Encoding.UTF8.GetBytes(
            $"DISCOVER|{OpenOmsiLanProtocol.ProtocolVersion}");

        for (var port = DefaultPort; port < DefaultPort + PortAttempts; port++)
        {
            await udp.SendAsync(
                message,
                new IPEndPoint(IPAddress.Broadcast, port),
                cancellationToken);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        while (!timeout.IsCancellationRequested)
        {
            UdpReceiveResult response;
            try
            {
                response = await udp.ReceiveAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(response.Buffer);
            }
            catch (DecoderFallbackException)
            {
                continue;
            }

            var parts = text.Split('|');
            if (parts.Length >= 6 &&
                parts[0] == "HERE" &&
                byte.TryParse(parts[1], out var protocol) &&
                protocol == OpenOmsiLanProtocol.ProtocolVersion)
            {
                return response.RemoteEndPoint;
            }
        }

        return null;
    }

    public Task JoinAsync(
        IPEndPoint host,
        OpenOmsiLanWorld world,
        string displayName,
        string? vehiclePath,
        CancellationToken cancellationToken = default) =>
        JoinAsyncCore(
            host,
            world,
            displayName,
            vehiclePath,
            null,
            cancellationToken);

    private async Task JoinAsyncCore(
        IPEndPoint host,
        OpenOmsiLanWorld world,
        string displayName,
        string? vehiclePath,
        ulong? requestedSession,
        CancellationToken cancellationToken)
    {
        ThrowIfRunning();
        ArgumentNullException.ThrowIfNull(host);

        World = world;
        IsHost = false;
        LocalPlayerId = 0;
        _hostEndpoint = host;
        _requestedSessionId = requestedSession;
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        _joinTcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        StartLoops();

        var nonceBytes = RandomNumberGenerator.GetBytes(8);
        var nonce = BitConverter.ToUInt64(nonceBytes).ToString("X16", CultureInfo.InvariantCulture);
        var hello = string.Join(
            "|",
            "HELLO",
            OpenOmsiLanProtocol.ProtocolVersion,
            requestedSession is ulong requiredSession
                ? OpenOmsiLanProtocol.SessionHex(requiredSession)
                : "-",
            OpenOmsiLanProtocol.CleanText(displayName, 32),
            OpenOmsiLanProtocol.NormalizeVehiclePath(vehiclePath) ?? string.Empty,
            OpenOmsiLanProtocol.CleanText(world.Map, 260),
            world.Date,
            world.TimeSeconds.ToString("0.##", CultureInfo.InvariantCulture),
            OpenOmsiLanProtocol.CleanText(world.Weather, 260),
            OpenOmsiLanProtocol.CleanText(world.Season, 16),
            nonce);
        _joinHello = hello;
        _lastHostPacketUtc = DateTimeOffset.UtcNow;
        _hostLostAtUtc = null;

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (LocalPlayerId == 0 && DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SendTextAsync(hello, host, cancellationToken);
            var completed = await Task.WhenAny(
                _joinTcs.Task,
                Task.Delay(700, cancellationToken));
            if (completed == _joinTcs.Task)
            {
                await _joinTcs.Task;
                break;
            }
        }

        if (LocalPlayerId == 0)
        {
            throw new TimeoutException("openOMSI LAN v6 host did not answer within 10 seconds.");
        }
    }

    public async Task PublishInfoAsync(
        OpenOmsiLanVehicleInfo info,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        info = info with { PlayerId = LocalPlayerId };
        var text = OpenOmsiLanProtocol.EncodeInfo(info);
        _localInfo = info;

        if (IsHost)
        {
            await BroadcastTextAsync(text, null, cancellationToken);
        }
        else
        {
            await SendTextAsync(text, _hostEndpoint!, cancellationToken);
        }
    }

    public async Task PublishStateAsync(
        OpenOmsiLanVehicleState state,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        state = state with { PlayerId = LocalPlayerId };
        var bytes = OpenOmsiLanStateCodec.Encode(state);
        _localState = state;

        if (IsHost)
        {
            await BroadcastBytesAsync(bytes, null, cancellationToken);
        }
        else
        {
            await SendBytesAsync(bytes, _hostEndpoint!, cancellationToken);
        }

        _lastLocalStateUtc = DateTimeOffset.UtcNow;
    }


    public async Task<IReadOnlyList<OpenOmsiLanFootprint>> RequestNearAsync(
        OpenOmsiLanFootprint at,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (IsHost)
        {
            return BuildNearFootprints(LocalPlayerId, at);
        }

        if (_hostEndpoint is null)
        {
            return Array.Empty<OpenOmsiLanFootprint>();
        }

        var tcs =
            new TaskCompletionSource<IReadOnlyList<OpenOmsiLanFootprint>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        _nearTcs = tcs;

        var message = string.Join(
            "|",
            "PLACE",
            LocalPlayerId.ToString(CultureInfo.InvariantCulture),
            at.X.ToString("0.00", CultureInfo.InvariantCulture),
            at.Y.ToString("0.00", CultureInfo.InvariantCulture),
            at.Z.ToString("0.00", CultureInfo.InvariantCulture),
            (((at.HeadingDegrees % 360d) + 360d) % 360d)
                .ToString("0.0", CultureInfo.InvariantCulture),
            Math.Clamp(at.LengthMeters, 0d, 60d)
                .ToString("0.0", CultureInfo.InvariantCulture),
            Math.Clamp(at.WidthMeters, 0d, 8d)
                .ToString("0.0", CultureInfo.InvariantCulture));

        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(3);
        try
        {
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SendTextAsync(
                    message,
                    _hostEndpoint,
                    cancellationToken);

                var completed = await Task.WhenAny(
                    tcs.Task,
                    Task.Delay(500, cancellationToken));
                if (completed == tcs.Task)
                {
                    return await tcs.Task;
                }
            }
        }
        finally
        {
            if (ReferenceEquals(_nearTcs, tcs))
            {
                _nearTcs = null;
            }
        }

        return Array.Empty<OpenOmsiLanFootprint>();
    }

    public async Task PublishWorldAsync(
        OpenOmsiWorldFrame frame,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        ArgumentNullException.ThrowIfNull(frame);

        var outgoing = IsHost
            ? frame
            : frame with
            {
                Cars = Array.Empty<OpenOmsiWorldCarState>(),
                Lights = Array.Empty<OpenOmsiWorldLightState>(),
                Gone = frame.Gone
                    .Where(item => item.IsPerson)
                    .ToArray(),
                ParkedComplete = null,
                ParkedMapIds = null
            };

        foreach (var packet in OpenOmsiWorldCodec.Encode(outgoing))
        {
            if (IsHost)
            {
                await BroadcastBytesAsync(
                    packet,
                    null,
                    cancellationToken);
            }
            else if (_hostEndpoint is not null)
            {
                await SendBytesAsync(
                    packet,
                    _hostEndpoint,
                    cancellationToken);
            }
        }
    }

    public async Task SendWorldDescriptionAsync(
        ushort targetPlayerId,
        OpenOmsiWorldDescription description,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (!IsHost ||
            !_peers.TryGetValue(targetPlayerId, out var target))
        {
            return;
        }

        await SendTextAsync(
            OpenOmsiWorldDescriptionCodec.Encode(description),
            target.Endpoint,
            cancellationToken);
    }

    public async Task BroadcastWorldDescriptionAsync(
        OpenOmsiWorldDescription description,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (!IsHost)
        {
            return;
        }

        await BroadcastTextAsync(
            OpenOmsiWorldDescriptionCodec.Encode(description),
            null,
            cancellationToken);
    }

    public async Task SendWorldDescriptionUpAsync(
        OpenOmsiWorldDescription.Person description,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (IsHost || _hostEndpoint is null)
        {
            return;
        }

        await SendTextAsync(
            OpenOmsiWorldDescriptionCodec.Encode(description),
            _hostEndpoint,
            cancellationToken);
    }

    public async Task RequestWorldDescriptionsAsync(
        IEnumerable<OpenOmsiWorldEntityRef> references,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (IsHost ||
            _hostEndpoint is null ||
            LocalPlayerId == 0)
        {
            return;
        }

        var pending = references
            .Where(item =>
                item.Id <= OpenOmsiWorldCodec.MaxId)
            .Distinct()
            .ToArray();

        for (var offset = 0; offset < pending.Length; offset += 64)
        {
            var chunk = pending
                .Skip(offset)
                .Take(64)
                .ToArray();
            if (chunk.Length == 0)
            {
                continue;
            }

            await SendTextAsync(
                OpenOmsiWorldDescriptionCodec.EncodeWant(
                    LocalPlayerId,
                    chunk),
                _hostEndpoint,
                cancellationToken);
        }
    }

    public async Task ClaimWorldPeopleAsync(
        IEnumerable<uint> people,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (IsHost ||
            _hostEndpoint is null ||
            LocalPlayerId == 0)
        {
            return;
        }

        var pending = people
            .Where(id =>
                id <= OpenOmsiWorldCodec.MaxId)
            .Distinct()
            .ToArray();

        for (var offset = 0;
             offset < pending.Length;
             offset += 64)
        {
            var chunk = pending
                .Skip(offset)
                .Take(64)
                .ToArray();
            if (chunk.Length == 0)
            {
                continue;
            }

            await SendTextAsync(
                OpenOmsiWorldDescriptionCodec.EncodeClaim(
                    LocalPlayerId,
                    chunk),
                _hostEndpoint,
                cancellationToken);
        }
    }

    public async Task AnswerWorldPeopleClaimAsync(
        ushort targetPlayerId,
        IEnumerable<uint> granted,
        IEnumerable<uint> denied,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (!IsHost ||
            !_peers.TryGetValue(
                targetPlayerId,
                out var target))
        {
            return;
        }

        foreach (var (isGranted, list) in new[]
                 {
                     (true, granted),
                     (false, denied)
                 })
        {
            var ids = list
                .Where(id =>
                    id <= OpenOmsiWorldCodec.MaxId)
                .Distinct()
                .ToArray();

            for (var offset = 0;
                 offset < ids.Length;
                 offset += 64)
            {
                var chunk = ids
                    .Skip(offset)
                    .Take(64)
                    .ToArray();
                if (chunk.Length == 0)
                {
                    continue;
                }

                await SendTextAsync(
                    OpenOmsiWorldDescriptionCodec
                        .EncodeClaimResult(
                            isGranted,
                            chunk),
                    target.Endpoint,
                    cancellationToken);
            }
        }
    }

    public async Task PublishVarsAsync(
        OpenOmsiVarsFrame vars,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (vars.PlayerId != LocalPlayerId)
        {
            vars = vars with { PlayerId = LocalPlayerId };
        }

        foreach (var packet in EncodeVarsFrame(vars))
        {
            if (IsHost)
            {
                await BroadcastBytesAsync(packet, null, cancellationToken);
            }
            else
            {
                await SendBytesAsync(packet, _hostEndpoint!, cancellationToken);
            }
        }
    }

    private static IReadOnlyList<byte[]> EncodeVarsFrame(OpenOmsiVarsFrame frame)
    {
        var packets = new List<byte[]>();

        var floatIndex = 0;
        while (floatIndex < frame.Floats.Count)
        {
            var data = new List<byte>(OpenOmsiVarsCodec.MaxPayloadBytes);
            data.AddRange(OpenOmsiVarsCodec.Header(frame.PlayerId, frame.TableHash, 0));
            data.Add(0);
            data.Add(0);
            ushort count = 0;

            while (floatIndex < frame.Floats.Count &&
                   data.Count + 6 <= OpenOmsiVarsCodec.MaxPayloadBytes)
            {
                var (index, value) = frame.Floats[floatIndex++];
                if (!float.IsFinite(value))
                {
                    continue;
                }

                data.Add((byte)index);
                data.Add((byte)(index >> 8));
                var bits = BitConverter.SingleToInt32Bits(value);
                data.Add((byte)bits);
                data.Add((byte)(bits >> 8));
                data.Add((byte)(bits >> 16));
                data.Add((byte)(bits >> 24));
                count++;
            }

            if (count > 0)
            {
                data[OpenOmsiVarsCodec.HeaderSize] = (byte)count;
                data[OpenOmsiVarsCodec.HeaderSize + 1] = (byte)(count >> 8);
                packets.Add(data.ToArray());
            }
        }

        var stringIndex = 0;
        while (stringIndex < frame.Strings.Count)
        {
            var data = new List<byte>(OpenOmsiVarsCodec.MaxPayloadBytes);
            data.AddRange(OpenOmsiVarsCodec.Header(frame.PlayerId, frame.TableHash, 2));
            data.Add(0);
            data.Add(0);
            ushort count = 0;

            while (stringIndex < frame.Strings.Count)
            {
                var (index, raw) = frame.Strings[stringIndex];
                var bytes = EncodeVarsUtf8(raw ?? string.Empty);
                if (data.Count + 4 + bytes.Length >
                    OpenOmsiVarsCodec.MaxPayloadBytes)
                {
                    if (count > 0)
                    {
                        break;
                    }

                    // EncodeVarsUtf8 already caps a single value to the wire
                    // limit, so this can only happen if the packet constants
                    // become inconsistent.
                    stringIndex++;
                    continue;
                }

                stringIndex++;
                data.Add((byte)index);
                data.Add((byte)(index >> 8));
                data.Add((byte)bytes.Length);
                data.Add((byte)(bytes.Length >> 8));
                data.AddRange(bytes);
                count++;
            }

            if (count > 0)
            {
                data[OpenOmsiVarsCodec.HeaderSize] = (byte)count;
                data[OpenOmsiVarsCodec.HeaderSize + 1] = (byte)(count >> 8);
                packets.Add(data.ToArray());
            }
        }

        return packets;
    }

    private static byte[] EncodeVarsUtf8(string value)
    {
        var clean = new string(
            value
                .Where(ch => !char.IsControl(ch))
                .ToArray());
        var bytes = Encoding.UTF8.GetBytes(clean);
        if (bytes.Length <= OpenOmsiVarsCodec.MaxTextBytes)
        {
            return bytes;
        }

        var length = OpenOmsiVarsCodec.MaxTextBytes;
        while (length > 0 &&
               (bytes[length] & 0xC0) == 0x80)
        {
            length--;
        }

        return length > 0
            ? bytes[..length]
            : Array.Empty<byte>();
    }

    public async Task SendChatAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        var clean = OpenOmsiLanProtocol.CleanText(text, 160);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return;
        }

        var name =
            OpenOmsiLanProtocol.CleanText(
                _localInfo?.Name ?? "Driver",
                32);

        ChatReceived?.Invoke(
            LocalPlayerId,
            string.IsNullOrWhiteSpace(name) ? "Driver" : name,
            clean);

        if (IsHost)
        {
            await BroadcastTextAsync(
                $"SAY|{LocalPlayerId}|{name}|{clean}",
                null,
                cancellationToken);
        }
        else if (_hostEndpoint is not null)
        {
            await SendTextAsync(
                $"CHAT|{LocalPlayerId}|{clean}",
                _hostEndpoint,
                cancellationToken);
        }
    }

    public async Task SendCommandAsync(
        ushort targetPlayerId,
        string command,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        if (targetPlayerId == 0 ||
            targetPlayerId == LocalPlayerId)
        {
            return;
        }

        var clean = OpenOmsiLanProtocol.CleanText(command, 160);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return;
        }

        if (IsHost)
        {
            if (_peers.TryGetValue(targetPlayerId, out var target))
            {
                await SendTextAsync(
                    $"CMD|{LocalPlayerId}|{targetPlayerId}|{clean}",
                    target.Endpoint,
                    cancellationToken);
            }
        }
        else if (_hostEndpoint is not null)
        {
            await SendTextAsync(
                $"CMD|{LocalPlayerId}|{targetPlayerId}|{clean}",
                _hostEndpoint,
                cancellationToken);
        }
    }

    public async Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning || LocalPlayerId == 0)
        {
            return;
        }

        var text = $"BYE|{LocalPlayerId}";
        if (IsHost)
        {
            await BroadcastTextAsync(text, null, cancellationToken);
        }
        else if (_hostEndpoint is not null)
        {
            await SendTextAsync(text, _hostEndpoint, cancellationToken);
        }
    }

    private void StartLoops()
    {
        _cts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_cts.Token);
        _maintenanceTask = MaintenanceLoopAsync(_cts.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await _udp!.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (received.Buffer.Length == 0 ||
                received.Buffer.Length > OpenOmsiLanProtocol.MaxDatagramBytes)
            {
                continue;
            }

            if (received.Buffer[0] == OpenOmsiWorldCodec.Magic)
            {
                HandleWorld(received.Buffer, received.RemoteEndPoint);
                continue;
            }

            if (received.Buffer[0] == OpenOmsiLanProtocol.StateMagic)
            {
                await HandleStateAsync(received.Buffer, received.RemoteEndPoint, cancellationToken);
                continue;
            }

            if (received.Buffer[0] == OpenOmsiVarsCodec.Magic)
            {
                await HandleVarsAsync(received.Buffer, received.RemoteEndPoint, cancellationToken);
                continue;
            }

            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(received.Buffer);
            }
            catch (DecoderFallbackException)
            {
                continue;
            }

            await HandleTextAsync(text, received.RemoteEndPoint, cancellationToken);
        }
    }

    private void HandleWorld(
        byte[] packet,
        IPEndPoint from)
    {
        if (!OpenOmsiWorldCodec.TryDecode(
                packet,
                out var frame))
        {
            return;
        }

        if (IsHost)
        {
            var peer = _peers.Values.FirstOrDefault(
                candidate => candidate.Endpoint.Equals(from));
            if (peer is null)
            {
                return;
            }

            peer.LastSeenUtc = DateTimeOffset.UtcNow;
            var clientFrame = frame with
            {
                Cars = Array.Empty<OpenOmsiWorldCarState>(),
                Lights = Array.Empty<OpenOmsiWorldLightState>(),
                Gone = frame.Gone
                    .Where(item => item.IsPerson)
                    .ToArray(),
                ParkedComplete = null,
                ParkedMapIds = null
            };
            ClientWorldFrameReceived?.Invoke(
                peer.Id,
                clientFrame);
            return;
        }

        if (_hostEndpoint is null ||
            !_hostEndpoint.Equals(from))
        {
            return;
        }

        _lastHostPacketUtc = DateTimeOffset.UtcNow;
        _hostLostAtUtc = null;
        WorldFrameReceived?.Invoke(frame);
    }

    private async Task HandleStateAsync(
        byte[] packet,
        IPEndPoint from,
        CancellationToken cancellationToken)
    {
        if (!OpenOmsiLanStateCodec.TryDecode(packet, out var state))
        {
            return;
        }

        if (IsHost)
        {
            if (!_peers.TryGetValue(state.PlayerId, out var peer) ||
                !peer.Endpoint.Equals(from))
            {
                return;
            }

            peer.LastSeenUtc = DateTimeOffset.UtcNow;
            peer.State = state;
            RemoteStateReceived?.Invoke(state);
            await BroadcastBytesAsync(packet, state.PlayerId, cancellationToken);
        }
        else
        {
            if (_hostEndpoint is null || !_hostEndpoint.Equals(from) ||
                state.PlayerId == LocalPlayerId)
            {
                return;
            }

            _lastHostPacketUtc = DateTimeOffset.UtcNow;
            _hostLostAtUtc = null;
            RemoteStateReceived?.Invoke(state);
        }
    }


    private async Task HandleVarsAsync(
        byte[] packet,
        IPEndPoint from,
        CancellationToken cancellationToken)
    {
        if (!OpenOmsiVarsCodec.TryDecode(packet, out var vars))
        {
            return;
        }

        if (IsHost)
        {
            if (!_peers.TryGetValue(vars.PlayerId, out var peer) ||
                !peer.Endpoint.Equals(from))
            {
                return;
            }

            peer.LastSeenUtc = DateTimeOffset.UtcNow;
            RemoteVarsReceived?.Invoke(vars);
            await BroadcastBytesAsync(packet, vars.PlayerId, cancellationToken);
            return;
        }

        if (_hostEndpoint is null ||
            !_hostEndpoint.Equals(from) ||
            vars.PlayerId == LocalPlayerId)
        {
            return;
        }

        _lastHostPacketUtc = DateTimeOffset.UtcNow;
        _hostLostAtUtc = null;
        RemoteVarsReceived?.Invoke(vars);
    }

    private async Task HandleTextAsync(
        string text,
        IPEndPoint from,
        CancellationToken cancellationToken)
    {
        if (IsHost && text.StartsWith("DISCOVER|", StringComparison.Ordinal))
        {
            var parts = text.Split('|');
            if (parts.Length >= 2 &&
                byte.TryParse(parts[1], out var protocol) &&
                protocol == OpenOmsiLanProtocol.ProtocolVersion)
            {
                await SendTextAsync(
                    $"HERE|{OpenOmsiLanProtocol.ProtocolVersion}|NavBR/openOMSI|{OpenOmsiLanProtocol.SessionHex(_sessionId)}|{OpenOmsiLanProtocol.CleanText(World.Map, 260)}|{_peers.Count + 1}",
                    from,
                    cancellationToken);
            }
            return;
        }

        if (IsHost && text.StartsWith("HELLO|", StringComparison.Ordinal))
        {
            await HandleHelloAsync(text, from, cancellationToken);
            return;
        }

        if (!IsHost && text.StartsWith("WELCOME|", StringComparison.Ordinal))
        {
            _lastHostPacketUtc = DateTimeOffset.UtcNow;
            _hostLostAtUtc = null;
            HandleWelcome(text, from);
            return;
        }
        if (!IsHost &&
            text.StartsWith("REJECT|", StringComparison.Ordinal) &&
            _hostEndpoint?.Equals(from) == true)
        {
            var parts = text.Split('|', 3);
            var reason = parts.Length >= 3
                ? OpenOmsiLanProtocol.CleanText(parts[2], 300)
                : "openOMSI host rejected the connection.";
            _joinTcs?.TrySetException(
                new InvalidOperationException(reason));
            return;
        }


        if (!IsHost && _hostEndpoint?.Equals(from) == true)
        {
            _lastHostPacketUtc = DateTimeOffset.UtcNow;
            _hostLostAtUtc = null;
        }

        if (!IsHost &&
            _hostEndpoint?.Equals(from) == true &&
            text.StartsWith("CLOCK|", StringComparison.Ordinal) &&
            OpenOmsiLanProtocol.TryDecodeClock(
                text,
                out var clock))
        {
            World = clock.World;
            _lastHostPacketUtc = DateTimeOffset.UtcNow;
            _hostLostAtUtc = null;
            ClockReceived?.Invoke(clock);
            return;
        }

        if (text.StartsWith("INFO|", StringComparison.Ordinal) &&
            OpenOmsiLanProtocol.TryDecodeInfo(text, out var info))
        {
            if (IsHost)
            {
                if (!_peers.TryGetValue(info.PlayerId, out var peer) ||
                    !peer.Endpoint.Equals(from))
                {
                    return;
                }

                peer.LastSeenUtc = DateTimeOffset.UtcNow;
                peer.Info = info;
                RemoteInfoReceived?.Invoke(info);
                await BroadcastTextAsync(text, info.PlayerId, cancellationToken);
            }
            else if (_hostEndpoint?.Equals(from) == true &&
                     info.PlayerId != LocalPlayerId)
            {
                RemoteInfoReceived?.Invoke(info);
            }
            return;
        }

        if (IsHost &&
            text.StartsWith("WANT|", StringComparison.Ordinal) &&
            OpenOmsiWorldDescriptionCodec.TryDecodeWant(
                text,
                out var wantRequesterId,
                out var requested) &&
            _peers.TryGetValue(
                wantRequesterId,
                out var requestingPeer) &&
            requestingPeer.Endpoint.Equals(from))
        {
            requestingPeer.LastSeenUtc =
                DateTimeOffset.UtcNow;
            WorldDescriptionsRequested?.Invoke(
                wantRequesterId,
                requested);
            return;
        }

        if (IsHost &&
            text.StartsWith("CLAIM|", StringComparison.Ordinal) &&
            OpenOmsiWorldDescriptionCodec.TryDecodeClaim(
                text,
                out var claimRequesterId,
                out var claimedPeople) &&
            _peers.TryGetValue(
                claimRequesterId,
                out var claimingPeer) &&
            claimingPeer.Endpoint.Equals(from))
        {
            claimingPeer.LastSeenUtc =
                DateTimeOffset.UtcNow;
            WorldPeopleClaimed?.Invoke(
                claimRequesterId,
                claimedPeople);
            return;
        }

        if (!IsHost &&
            _hostEndpoint?.Equals(from) == true &&
            (text.StartsWith("GRANT|", StringComparison.Ordinal) ||
             text.StartsWith("DENY|", StringComparison.Ordinal)) &&
            OpenOmsiWorldDescriptionCodec.TryDecodeClaimResult(
                text,
                out var granted,
                out var people))
        {
            _lastHostPacketUtc =
                DateTimeOffset.UtcNow;
            _hostLostAtUtc = null;
            WorldPeopleClaimResult?.Invoke(
                people,
                granted);
            return;
        }

        if (text.StartsWith("DESC|", StringComparison.Ordinal) &&
            OpenOmsiWorldDescriptionCodec.TryDecode(
                text,
                out var description))
        {
            if (IsHost)
            {
                var source = _peers.Values.FirstOrDefault(
                    peer => peer.Endpoint.Equals(from));
                if (source is not null &&
                    description is
                        OpenOmsiWorldDescription.Person person)
                {
                    source.LastSeenUtc =
                        DateTimeOffset.UtcNow;
                    ClientWorldDescriptionReceived?.Invoke(
                        source.Id,
                        person);
                }
            }
            else if (_hostEndpoint?.Equals(from) == true)
            {
                _lastHostPacketUtc =
                    DateTimeOffset.UtcNow;
                _hostLostAtUtc = null;
                WorldDescriptionReceived?.Invoke(
                    description);
            }
            return;
        }

        if (IsHost &&
            text.StartsWith("PLACE|", StringComparison.Ordinal))
        {
            var parts = text.Split('|');
            if (parts.Length >= 8 &&
                ushort.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var requesterId) &&
                _peers.TryGetValue(requesterId, out var requester) &&
                requester.Endpoint.Equals(from) &&
                TryParseFootprint(parts, 2, out var spawn))
            {
                requester.LastSeenUtc = DateTimeOffset.UtcNow;
                var near = BuildNearFootprints(requesterId, spawn);
                await SendTextAsync(
                    OpenOmsiLanProtocol.EncodeNear(requesterId, near),
                    from,
                    cancellationToken);
            }
            return;
        }

        if (!IsHost &&
            text.StartsWith("NEAR|", StringComparison.Ordinal) &&
            _hostEndpoint?.Equals(from) == true &&
            OpenOmsiLanProtocol.TryDecodeNear(
                text,
                LocalPlayerId,
                out var footprints))
        {
            _nearTcs?.TrySetResult(footprints);
            return;
        }

        if (IsHost &&
            text.StartsWith("CHAT|", StringComparison.Ordinal))
        {
            var parts = text.Split('|', 3);
            if (parts.Length == 3 &&
                ushort.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var senderId) &&
                _peers.TryGetValue(senderId, out var sender) &&
                sender.Endpoint.Equals(from))
            {
                var chat = OpenOmsiLanProtocol.CleanText(parts[2], 160);
                if (!string.IsNullOrWhiteSpace(chat))
                {
                    sender.LastSeenUtc = DateTimeOffset.UtcNow;
                    var name = OpenOmsiLanProtocol.CleanText(
                        sender.Info?.Name ?? $"Player {senderId}",
                        32);
                    ChatReceived?.Invoke(senderId, name, chat);
                    await BroadcastTextAsync(
                        $"SAY|{senderId}|{name}|{chat}",
                        senderId,
                        cancellationToken);
                }
            }
            return;
        }

        if (!IsHost &&
            text.StartsWith("SAY|", StringComparison.Ordinal) &&
            _hostEndpoint?.Equals(from) == true)
        {
            var parts = text.Split('|', 4);
            if (parts.Length == 4 &&
                ushort.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var senderId))
            {
                var name = OpenOmsiLanProtocol.CleanText(parts[2], 32);
                var chat = OpenOmsiLanProtocol.CleanText(parts[3], 160);
                if (!string.IsNullOrWhiteSpace(chat))
                {
                    ChatReceived?.Invoke(senderId, name, chat);
                }
            }
            return;
        }

        if (text.StartsWith("CMD|", StringComparison.Ordinal))
        {
            var parts = text.Split('|', 4);
            if (parts.Length == 4 &&
                ushort.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var senderId) &&
                ushort.TryParse(
                    parts[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var targetId))
            {
                var command =
                    OpenOmsiLanProtocol.CleanText(parts[3], 160);
                if (IsHost)
                {
                    if (_peers.TryGetValue(senderId, out var sender) &&
                        sender.Endpoint.Equals(from) &&
                        !string.IsNullOrWhiteSpace(command))
                    {
                        sender.LastSeenUtc = DateTimeOffset.UtcNow;
                        if (targetId == LocalPlayerId)
                        {
                            CommandReceived?.Invoke(senderId, command);
                        }
                        else if (_peers.TryGetValue(targetId, out var target))
                        {
                            await SendTextAsync(
                                $"CMD|{senderId}|{targetId}|{command}",
                                target.Endpoint,
                                cancellationToken);
                        }
                    }
                }
                else if (_hostEndpoint?.Equals(from) == true &&
                         targetId == LocalPlayerId &&
                         !string.IsNullOrWhiteSpace(command))
                {
                    CommandReceived?.Invoke(senderId, command);
                }
            }
            return;
        }

        if (text.StartsWith("BYE|", StringComparison.Ordinal) &&
            ushort.TryParse(text.AsSpan(4), out var left))
        {
            if (IsHost && _peers.TryRemove(left, out var peer) &&
                peer.Endpoint.Equals(from))
            {
                await BroadcastTextAsync(text, left, cancellationToken);
                RemoteLeft?.Invoke(left);
            }
            else if (!IsHost && _hostEndpoint?.Equals(from) == true)
            {
                RemoteLeft?.Invoke(left);
            }
            return;
        }

        if (text.StartsWith("NOTE|", StringComparison.Ordinal))
        {
            NoteReceived?.Invoke(text[5..]);
        }
    }

    private IReadOnlyList<OpenOmsiLanFootprint> BuildNearFootprints(
        ushort requesterId,
        OpenOmsiLanFootprint spawn)
    {
        var candidates = new List<OpenOmsiLanFootprint>();

        if (requesterId != LocalPlayerId &&
            TryBuildFootprint(_localInfo, _localState, out var local))
        {
            candidates.Add(local);
        }

        foreach (var pair in _peers)
        {
            if (pair.Key == requesterId ||
                !TryBuildFootprint(
                    pair.Value.Info,
                    pair.Value.State,
                    out var footprint))
            {
                continue;
            }

            candidates.Add(footprint);
        }

        const double radiusSquared = 250d * 250d;
        return candidates
            .Where(footprint =>
            {
                var dx = footprint.X - spawn.X;
                var dy = footprint.Y - spawn.Y;
                return dx * dx + dy * dy < radiusSquared;
            })
            .OrderBy(footprint =>
            {
                var dx = footprint.X - spawn.X;
                var dy = footprint.Y - spawn.Y;
                return dx * dx + dy * dy;
            })
            .Take(28)
            .ToArray();
    }

    private static bool TryBuildFootprint(
        OpenOmsiLanVehicleInfo? info,
        OpenOmsiLanVehicleState? state,
        out OpenOmsiLanFootprint footprint)
    {
        footprint = default!;
        if (info is null ||
            state is null ||
            (state.Flags & OpenOmsiLanProtocol.FlagVehicle) == 0)
        {
            return false;
        }

        var heading =
            ((state.HeadingDegrees % 360f) + 360f) % 360f;
        var radians = heading * Math.PI / 180d;
        var offset = info.BoxOffsetMeters;
        footprint = new OpenOmsiLanFootprint(
            state.X + Math.Sin(radians) * offset,
            state.Y + Math.Cos(radians) * offset,
            state.Z,
            heading,
            Math.Max(1d, info.LengthMeters),
            Math.Max(1d, info.WidthMeters));
        return true;
    }

    private static bool TryParseFootprint(
        IReadOnlyList<string> parts,
        int first,
        out OpenOmsiLanFootprint footprint)
    {
        footprint = default!;
        if (parts.Count < first + 6)
        {
            return false;
        }

        var values = new double[6];
        for (var i = 0; i < values.Length; i++)
        {
            if (!double.TryParse(
                    parts[first + i],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out values[i]) ||
                !double.IsFinite(values[i]))
            {
                return false;
            }
        }

        if (Math.Abs(values[0]) > 100_000_000d ||
            Math.Abs(values[1]) > 100_000_000d ||
            Math.Abs(values[2]) > 100_000d)
        {
            return false;
        }

        footprint = new OpenOmsiLanFootprint(
            values[0],
            values[1],
            values[2],
            ((values[3] % 360d) + 360d) % 360d,
            Math.Clamp(values[4], 0d, 60d),
            Math.Clamp(values[5], 0d, 8d));
        return true;
    }

    private async Task HandleHelloAsync(
        string text,
        IPEndPoint from,
        CancellationToken cancellationToken)
    {
        if (!OpenOmsiLanProtocol.TryDecodeHello(text, out var hello) ||
            hello.Protocol != OpenOmsiLanProtocol.ProtocolVersion)
        {
            await SendTextAsync(
                $"REJECT|{OpenOmsiLanProtocol.ProtocolVersion}|Protocol mismatch",
                from,
                cancellationToken);
            return;
        }

        if (hello.RequestedSession is ulong requested &&
            requested != _sessionId)
        {
            await SendTextAsync(
                $"REJECT|{OpenOmsiLanProtocol.ProtocolVersion}|Wrong session code",
                from,
                cancellationToken);
            return;
        }

        if (_peers.Count >= 32 &&
            !_peers.Values.Any(peer =>
                peer.Endpoint.Equals(from) ||
                (hello.Nonce is ulong nonce &&
                 peer.Nonce == nonce)))
        {
            await SendTextAsync(
                $"REJECT|{OpenOmsiLanProtocol.ProtocolVersion}|Session full",
                from,
                cancellationToken);
            return;
        }

        var existing = _peers.Values.FirstOrDefault(peer =>
            peer.Endpoint.Equals(from) ||
            (hello.Nonce is ulong nonce &&
             peer.Nonce == nonce));
        var peer = existing ?? new Peer(AllocatePlayerId(), from);
        peer.Endpoint = from;
        peer.Nonce = hello.Nonce;
        peer.LastSeenUtc = DateTimeOffset.UtcNow;
        _peers[peer.Id] = peer;

        var welcome = string.Join(
            "|",
            "WELCOME",
            OpenOmsiLanProtocol.ProtocolVersion,
            peer.Id,
            OpenOmsiLanProtocol.SessionHex(_sessionId),
            "NavBR/openOMSI",
            OpenOmsiLanProtocol.CleanText(World.Map, 260),
            World.Date,
            World.TimeSeconds.ToString("0.##", CultureInfo.InvariantCulture),
            OpenOmsiLanProtocol.CleanText(World.Weather, 260),
            OpenOmsiLanProtocol.CleanText(World.Season, 16),
            _peers.Count + 1);

        await SendTextAsync(welcome, from, cancellationToken);
        await SendTextAsync("NOTE|Connected through openOMSI LAN v6.", from, cancellationToken);
    }

    private void HandleWelcome(string text, IPEndPoint from)
    {
        if (_hostEndpoint is null || !_hostEndpoint.Equals(from))
        {
            return;
        }

        var parts = text.Split('|');
        if (parts.Length < 11 ||
            parts[0] != "WELCOME" ||
            !byte.TryParse(parts[1], out var protocol) ||
            protocol != OpenOmsiLanProtocol.ProtocolVersion ||
            !ushort.TryParse(parts[2], out var id) ||
            id < 2)
        {
            return;
        }

        if (!ulong.TryParse(
                parts[3],
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var session))
        {
            return;
        }

        session &= 0xFFFF_FFFF_FFFFUL;
        if (_requestedSessionId is ulong requiredSession &&
            session != requiredSession)
        {
            _joinTcs?.TrySetException(
                new InvalidOperationException(
                    "openOMSI host answered with a different session id."));
            return;
        }

        _sessionId = session;
        LocalPlayerId = id;

        if (double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
        {
            World = new OpenOmsiLanWorld(parts[5], parts[6], time, parts[8], parts[9]);
        }

        _lastHostPacketUtc = DateTimeOffset.UtcNow;
        _hostLostAtUtc = null;
        _joinTcs?.TrySetResult(true);
    }

    private async Task MaintenanceLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var lastClockUtc = DateTimeOffset.MinValue;

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;

            if (IsHost)
            {
                var cutoff = now - PeerTimeout;
                foreach (var pair in _peers.ToArray())
                {
                    if (pair.Value.LastSeenUtc >= cutoff)
                    {
                        continue;
                    }

                    if (_peers.TryRemove(pair.Key, out _))
                    {
                        RemoteLeft?.Invoke(pair.Key);
                        await BroadcastTextAsync(
                            $"BYE|{pair.Key}",
                            pair.Key,
                            cancellationToken);
                    }
                }

                if (now - lastClockUtc >= TimeSpan.FromSeconds(5))
                {
                    await BroadcastTextAsync(
                        OpenOmsiLanProtocol.EncodeClock(World),
                        null,
                        cancellationToken);
                    lastClockUtc = now;
                }

                continue;
            }

            if (_hostEndpoint is null || LocalPlayerId == 0)
            {
                continue;
            }

            if (now - _lastLocalStateUtc >= TimeSpan.FromSeconds(1))
            {
                var heartbeat = OpenOmsiLanVehicleState.Empty(
                    LocalPlayerId,
                    unchecked(++_heartbeatSequence)) with
                {
                    SentMilliseconds = unchecked((uint)Environment.TickCount64)
                };
                await SendBytesAsync(
                    OpenOmsiLanStateCodec.Encode(heartbeat),
                    _hostEndpoint,
                    cancellationToken);
                _lastLocalStateUtc = now;
            }

            if (now - _lastHostPacketUtc <= PeerTimeout)
            {
                continue;
            }

            _hostLostAtUtc ??= now;
            if (now - _hostLostAtUtc.Value > TimeSpan.FromSeconds(60))
            {
                NoteReceived?.Invoke(
                    "openOMSI host unreachable for 60 seconds; multiplayer transport is offline.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(_joinHello))
            {
                await SendTextAsync(
                    _joinHello,
                    _hostEndpoint,
                    cancellationToken);
            }
        }
    }

    private async Task BroadcastTextAsync(
        string text,
        ushort? except,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await BroadcastBytesAsync(bytes, except, cancellationToken);
    }

    private async Task BroadcastBytesAsync(
        byte[] bytes,
        ushort? except,
        CancellationToken cancellationToken)
    {
        foreach (var peer in _peers.Values)
        {
            if (except == peer.Id)
            {
                continue;
            }

            await SendBytesAsync(bytes, peer.Endpoint, cancellationToken);
        }
    }

    private async Task SendTextAsync(
        string text,
        IPEndPoint endpoint,
        CancellationToken cancellationToken) =>
        await SendBytesAsync(Encoding.UTF8.GetBytes(text), endpoint, cancellationToken);

    private async Task SendBytesAsync(
        byte[] bytes,
        IPEndPoint endpoint,
        CancellationToken cancellationToken)
    {
        if (bytes.Length > OpenOmsiLanProtocol.MaxDatagramBytes)
        {
            throw new InvalidOperationException("openOMSI LAN datagram exceeds protocol limit.");
        }

        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            await _udp!.SendAsync(bytes, endpoint, cancellationToken);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private static UdpClient BindHost()
    {
        for (var i = 0; i < PortAttempts; i++)
        {
            try
            {
                return new UdpClient(new IPEndPoint(IPAddress.Any, DefaultPort + i));
            }
            catch (SocketException)
            {
            }
        }

        throw new SocketException((int)SocketError.AddressAlreadyInUse);
    }

    private ushort AllocatePlayerId()
    {
        lock (_sync)
        {
            while (_nextPlayerId == 0 || _nextPlayerId == 1 || _peers.ContainsKey(_nextPlayerId))
            {
                _nextPlayerId++;
                if (_nextPlayerId == 0)
                {
                    _nextPlayerId = 2;
                }
            }

            return _nextPlayerId++;
        }
    }

    private static IPAddress ResolveAdvertiseAddress()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(address =>
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                ?? IPAddress.Loopback;
        }
        catch
        {
            return IPAddress.Loopback;
        }
    }

    private static ulong CreateSessionId()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt64(bytes) & 0x0000_FFFF_FFFF_FFFFUL;
    }

    private void EnsureConnected()
    {
        if (!IsRunning || LocalPlayerId == 0)
        {
            throw new InvalidOperationException("openOMSI LAN v6 session is not connected.");
        }
    }

    private void ThrowIfRunning()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Session is already running.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await LeaveAsync();
        }
        catch
        {
        }

        var cts = _cts;
        var receive = _receiveTask;
        var maintenance = _maintenanceTask;
        _cts = null;
        _receiveTask = null;
        _maintenanceTask = null;

        cts?.Cancel();
        _udp?.Dispose();
        _udp = null;

        foreach (var task in new[] { receive, maintenance })
        {
            if (task is null)
            {
                continue;
            }

            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        cts?.Dispose();
        _sendGate.Dispose();
    }

    private sealed class Peer(ushort id, IPEndPoint endpoint)
    {
        public ushort Id { get; } = id;
        public IPEndPoint Endpoint { get; set; } = endpoint;
        public ulong? Nonce { get; set; }
        public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
        public OpenOmsiLanVehicleInfo? Info { get; set; }
        public OpenOmsiLanVehicleState? State { get; set; }
    }
}
