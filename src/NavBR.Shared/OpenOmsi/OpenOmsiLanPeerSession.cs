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
    private ushort _nextPlayerId = 2;
    private ulong _sessionId;

    public bool IsHost { get; private set; }
    public bool IsRunning => _udp is not null;
    public ushort LocalPlayerId { get; private set; }
    public int? Port => (_udp?.Client.LocalEndPoint as IPEndPoint)?.Port;
    public ulong SessionId => _sessionId;
    public OpenOmsiLanWorld World { get; private set; } =
        new(string.Empty, string.Empty, 0d, string.Empty, string.Empty);

    public event Action<OpenOmsiLanVehicleInfo>? RemoteInfoReceived;
    public event Action<OpenOmsiLanVehicleState>? RemoteStateReceived;
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

    public async Task JoinAsync(
        IPEndPoint host,
        OpenOmsiLanWorld world,
        string displayName,
        string? vehiclePath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfRunning();
        ArgumentNullException.ThrowIfNull(host);

        World = world;
        IsHost = false;
        LocalPlayerId = 0;
        _hostEndpoint = host;
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
            "-",
            OpenOmsiLanProtocol.CleanText(displayName, 32),
            OpenOmsiLanProtocol.NormalizeVehiclePath(vehiclePath) ?? string.Empty,
            OpenOmsiLanProtocol.CleanText(world.Map, 260),
            world.Date,
            world.TimeSeconds.ToString("0.##", CultureInfo.InvariantCulture),
            OpenOmsiLanProtocol.CleanText(world.Weather, 260),
            OpenOmsiLanProtocol.CleanText(world.Season, 16),
            nonce);

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

        if (IsHost)
        {
            await BroadcastBytesAsync(bytes, null, cancellationToken);
        }
        else
        {
            await SendBytesAsync(bytes, _hostEndpoint!, cancellationToken);
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

            if (received.Buffer[0] == OpenOmsiLanProtocol.StateMagic)
            {
                await HandleStateAsync(received.Buffer, received.RemoteEndPoint, cancellationToken);
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

            RemoteStateReceived?.Invoke(state);
        }
    }

    private async Task HandleTextAsync(
        string text,
        IPEndPoint from,
        CancellationToken cancellationToken)
    {
        if (IsHost && text.StartsWith("HELLO|", StringComparison.Ordinal))
        {
            await HandleHelloAsync(text, from, cancellationToken);
            return;
        }

        if (!IsHost && text.StartsWith("WELCOME|", StringComparison.Ordinal))
        {
            HandleWelcome(text, from);
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

        var existing = _peers.Values.FirstOrDefault(p => p.Endpoint.Equals(from));
        var peer = existing ?? new Peer(AllocatePlayerId(), from);
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

        LocalPlayerId = id;
        if (ulong.TryParse(parts[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var session))
        {
            _sessionId = session;
        }

        if (double.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out var time))
        {
            World = new OpenOmsiLanWorld(parts[5], parts[6], time, parts[8], parts[9]);
        }

        _joinTcs?.TrySetResult(true);
    }

    private async Task MaintenanceLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (!IsHost)
            {
                continue;
            }

            var cutoff = DateTimeOffset.UtcNow - PeerTimeout;
            foreach (var pair in _peers.ToArray())
            {
                if (pair.Value.LastSeenUtc >= cutoff)
                {
                    continue;
                }

                if (_peers.TryRemove(pair.Key, out _))
                {
                    RemoteLeft?.Invoke(pair.Key);
                    await BroadcastTextAsync($"BYE|{pair.Key}", pair.Key, cancellationToken);
                }
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
        public IPEndPoint Endpoint { get; } = endpoint;
        public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
        public OpenOmsiLanVehicleInfo? Info { get; set; }
    }
}
