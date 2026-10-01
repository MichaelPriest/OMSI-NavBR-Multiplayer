using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.OpenOmsi;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

internal sealed record OpenOmsiLanGatewayStatus(
    bool Running,
    int? Port,
    bool ClientConnected,
    string? ClientName,
    string? Map,
    string? VehiclePath,
    int RemotePlayers,
    DateTimeOffset? LastClientPacketUtc,
    string? LastError);

internal sealed class OpenOmsiLanGateway : IAsyncDisposable
{
    private const int FirstPort = 27015;
    private const int PortAttempts = 8;
    private const ushort LocalOpenOmsiPlayerId = 2;
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(15);

    private readonly object _sync = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly Dictionary<string, RemotePeer> _remotes =
        new(StringComparer.OrdinalIgnoreCase);

    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _clockTask;
    private IPEndPoint? _clientEndpoint;
    private string? _clientName;
    private OpenOmsiLanWorld _world =
        new(string.Empty, string.Empty, 0d, string.Empty, string.Empty);
    private OpenOmsiLanVehicleInfo? _localInfo;
    private VehicleTelemetry? _latestLocalTelemetry;
    private DateTimeOffset? _lastClientPacketUtc;
    private string? _lastError;
    private ushort _nextRemoteId = 3;
    private ulong _session;

    public static OpenOmsiLanGateway Shared { get; } = new();

    public event Action<VehicleTelemetry>? LocalTelemetryReceived;
    public event Action<OpenOmsiLanGatewayStatus>? StatusChanged;

    public int? Port
    {
        get
        {
            lock (_sync)
            {
                return (_udp?.Client.LocalEndPoint as IPEndPoint)?.Port;
            }
        }
    }

    public bool IsClientConnected
    {
        get
        {
            lock (_sync)
            {
                return IsClientConnectedCore(DateTimeOffset.UtcNow);
            }
        }
    }

    public VehicleTelemetry? LatestLocalTelemetry
    {
        get
        {
            lock (_sync)
            {
                return _latestLocalTelemetry;
            }
        }
    }

    public OpenOmsiLanGatewayStatus GetStatus()
    {
        lock (_sync)
        {
            return BuildStatusCore(DateTimeOffset.UtcNow);
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_udp is not null)
            {
                return;
            }

            _session = CreateSessionId();
            _lastError = null;
            _udp = BindLoopback();
            _cts = new CancellationTokenSource();
            _receiveTask = ReceiveLoopAsync(_udp, _cts.Token);
            _clockTask = ClockLoopAsync(_udp, _cts.Token);
        }

        PublishStatus();
    }

    public async Task UpsertRemoteAsync(
        PlayerTelemetryFrame frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);

        IPEndPoint? endpoint;
        OpenOmsiLanVehicleInfo info;
        OpenOmsiLanVehicleState state;
        string? previousInfo;
        RemotePeer remote;

        lock (_sync)
        {
            endpoint = GetConnectedEndpointCore(DateTimeOffset.UtcNow);
            if (endpoint is null)
            {
                return;
            }

            if (!frame.Telemetry.IsInGame ||
                !MapsMatch(_world.Map, frame.Telemetry.MapName))
            {
                _ = RemoveRemoteCoreAsync(
                    frame.Player.PlayerId,
                    endpoint,
                    cancellationToken);
                return;
            }

            if (!_remotes.TryGetValue(frame.Player.PlayerId, out remote!))
            {
                remote = new RemotePeer(AllocateRemoteId());
                _remotes[frame.Player.PlayerId] = remote;
            }

            info = BuildRemoteInfo(remote.Id, frame);
            state = BuildRemoteState(
                remote.Id,
                remote.NextSequence(),
                frame.Telemetry);
            previousInfo = remote.LastInfo;
            remote.LastInfo = OpenOmsiLanProtocol.EncodeInfo(info);
            remote.LastState = state;
            remote.LastSeenUtc = DateTimeOffset.UtcNow;
        }

        if (!string.Equals(
                previousInfo,
                remote.LastInfo,
                StringComparison.Ordinal))
        {
            await SendTextAsync(
                remote.LastInfo!,
                endpoint,
                cancellationToken);
        }

        // A vehicle path is mandatory for openOMSI to materialize the remote.
        // Keep the peer visible in diagnostics but do not send a vehicle STATE
        // that would claim physical readiness without an actual .bus/.ovh path.
        if (string.IsNullOrWhiteSpace(info.VehiclePath))
        {
            PublishStatus();
            return;
        }

        await SendBytesAsync(
            OpenOmsiLanStateCodec.Encode(state),
            endpoint,
            cancellationToken);
        PublishStatus();
    }

    public async Task RemoveRemoteAsync(
        string playerId,
        CancellationToken cancellationToken = default)
    {
        IPEndPoint? endpoint;
        ushort? id = null;

        lock (_sync)
        {
            endpoint = GetConnectedEndpointCore(DateTimeOffset.UtcNow);
            if (_remotes.Remove(playerId, out var remote))
            {
                id = remote.Id;
            }
        }

        if (id is ushort removedId && endpoint is not null)
        {
            await SendTextAsync(
                $"BYE|{removedId}",
                endpoint,
                cancellationToken);
        }

        PublishStatus();
    }

    public async Task ClearRemotesAsync(
        CancellationToken cancellationToken = default)
    {
        IPEndPoint? endpoint;
        ushort[] ids;

        lock (_sync)
        {
            endpoint = GetConnectedEndpointCore(DateTimeOffset.UtcNow);
            ids = _remotes.Values.Select(remote => remote.Id).ToArray();
            _remotes.Clear();
        }

        if (endpoint is not null)
        {
            foreach (var id in ids)
            {
                await SendTextAsync(
                    $"BYE|{id}",
                    endpoint,
                    cancellationToken);
            }
        }

        PublishStatus();
    }

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? cts;
        Task? receive;
        Task? clock;
        UdpClient? udp;

        lock (_sync)
        {
            cts = _cts;
            receive = _receiveTask;
            clock = _clockTask;
            udp = _udp;
            _cts = null;
            _receiveTask = null;
            _clockTask = null;
            _udp = null;
            _clientEndpoint = null;
            _latestLocalTelemetry = null;
            _remotes.Clear();
        }

        cts?.Cancel();
        udp?.Dispose();

        foreach (var task in new[] { receive, clock })
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
        PublishStatus();
    }

    private async Task ReceiveLoopAsync(
        UdpClient udp,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await udp.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                SetError(ex.Message);
                await Task.Delay(250, cancellationToken);
                continue;
            }

            if (!IPAddress.IsLoopback(received.RemoteEndPoint.Address) ||
                received.Buffer.Length == 0 ||
                received.Buffer.Length > OpenOmsiLanProtocol.MaxDatagramBytes)
            {
                continue;
            }

            try
            {
                await HandleDatagramAsync(
                    received.Buffer,
                    received.RemoteEndPoint,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
            }
        }
    }

    private async Task ClockLoopAsync(
        UdpClient udp,
        CancellationToken cancellationToken)
    {
        _ = udp;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            IPEndPoint? endpoint;
            OpenOmsiLanWorld world;
            bool timedOut = false;

            lock (_sync)
            {
                endpoint = GetConnectedEndpointCore(DateTimeOffset.UtcNow);
                world = _world;
                if (endpoint is null && _clientEndpoint is not null)
                {
                    timedOut = true;
                    _clientEndpoint = null;
                    _clientName = null;
                    _localInfo = null;
                    _latestLocalTelemetry = null;
                }
            }

            if (endpoint is not null)
            {
                await SendTextAsync(
                    OpenOmsiLanProtocol.EncodeClock(world),
                    endpoint,
                    cancellationToken);
            }

            if (timedOut)
            {
                PublishStatus();
            }
        }
    }

    private async Task HandleDatagramAsync(
        byte[] data,
        IPEndPoint from,
        CancellationToken cancellationToken)
    {
        if (data[0] == OpenOmsiLanProtocol.StateMagic)
        {
            HandleState(data, from);
            return;
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(data);
        }
        catch (DecoderFallbackException)
        {
            return;
        }

        if (text.StartsWith("DISCOVER|", StringComparison.Ordinal))
        {
            await HandleDiscoverAsync(text, from, cancellationToken);
            return;
        }

        if (text.StartsWith("HELLO|", StringComparison.Ordinal))
        {
            await HandleHelloAsync(text, from, cancellationToken);
            return;
        }

        if (!IsCurrentClient(from))
        {
            return;
        }

        MarkClientPacket();

        if (text.StartsWith("INFO|", StringComparison.Ordinal))
        {
            if (OpenOmsiLanProtocol.TryDecodeInfo(text, out var info) &&
                info.PlayerId == LocalOpenOmsiPlayerId)
            {
                lock (_sync)
                {
                    _localInfo = info;
                }

                PublishStatus();
            }

            return;
        }

        if (text.StartsWith(
                $"PLACE|{LocalOpenOmsiPlayerId}|",
                StringComparison.Ordinal))
        {
            await SendTextAsync(
                OpenOmsiLanProtocol.EncodeNear(LocalOpenOmsiPlayerId),
                from,
                cancellationToken);
            return;
        }

        if (string.Equals(
                text.Trim(),
                $"BYE|{LocalOpenOmsiPlayerId}",
                StringComparison.Ordinal))
        {
            lock (_sync)
            {
                _clientEndpoint = null;
                _clientName = null;
                _localInfo = null;
                _latestLocalTelemetry = null;
            }

            PublishStatus();
        }
    }

    private async Task HandleDiscoverAsync(
        string text,
        IPEndPoint from,
        CancellationToken cancellationToken)
    {
        var parts = text.Split('|');
        if (parts.Length < 2 ||
            !byte.TryParse(parts[1], out var protocol) ||
            protocol != OpenOmsiLanProtocol.ProtocolVersion)
        {
            return;
        }

        OpenOmsiLanWorld world;
        int players;
        lock (_sync)
        {
            world = _world;
            players = _remotes.Count + (_clientEndpoint is null ? 0 : 1);
        }

        await SendTextAsync(
            $"HERE|{OpenOmsiLanProtocol.ProtocolVersion}|NavBR Gateway|{OpenOmsiLanProtocol.SessionHex(_session)}|{OpenOmsiLanProtocol.CleanText(world.Map, 260)}|{Math.Max(1, players)}",
            from,
            cancellationToken);
    }

    private async Task HandleHelloAsync(
        string text,
        IPEndPoint from,
        CancellationToken cancellationToken)
    {
        if (!OpenOmsiLanProtocol.TryDecodeHello(text, out var hello))
        {
            return;
        }

        if (hello.Protocol != OpenOmsiLanProtocol.ProtocolVersion)
        {
            await SendTextAsync(
                $"REJECT|{OpenOmsiLanProtocol.ProtocolVersion}|NavBR gateway requires LAN protocol {OpenOmsiLanProtocol.ProtocolVersion}.",
                from,
                cancellationToken);
            return;
        }

        if (hello.RequestedSession is ulong requested &&
            requested != _session)
        {
            await SendTextAsync(
                $"REJECT|{OpenOmsiLanProtocol.ProtocolVersion}|Wrong NavBR gateway session.",
                from,
                cancellationToken);
            return;
        }

        OpenOmsiLanVehicleInfo? helloInfo = null;
        bool rejectBusy = false;
        lock (_sync)
        {
            if (_clientEndpoint is not null &&
                !_clientEndpoint.Equals(from) &&
                IsClientConnectedCore(DateTimeOffset.UtcNow))
            {
                rejectBusy = true;
            }
            else
            {
                _clientEndpoint = from;
                _clientName = string.IsNullOrWhiteSpace(hello.Name)
                    ? "Driver"
                    : hello.Name;
                _world = hello.World;
                _lastClientPacketUtc = DateTimeOffset.UtcNow;
                _lastError = null;

                if (!string.IsNullOrWhiteSpace(hello.VehiclePath))
                {
                    helloInfo = new OpenOmsiLanVehicleInfo(
                        LocalOpenOmsiPlayerId,
                        _clientName,
                        hello.VehiclePath,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        12d,
                        2.55d,
                        0d,
                        0,
                        string.Empty,
                        [],
                        null,
                        []);
                    _localInfo ??= helloInfo;
                }
            }
        }

        if (rejectBusy)
        {
            await SendTextAsync(
                $"REJECT|{OpenOmsiLanProtocol.ProtocolVersion}|Another local openOMSI instance is already linked to NavBR.",
                from,
                cancellationToken);
            return;
        }

        OpenOmsiLanWorld world;
        int players;
        RemotePeer[] remotes;
        lock (_sync)
        {
            world = _world;
            players = _remotes.Count + 1;
            remotes = _remotes.Values.ToArray();
        }

        await SendTextAsync(
            OpenOmsiLanProtocol.EncodeWelcome(
                LocalOpenOmsiPlayerId,
                _session,
                "NavBR Gateway",
                world,
                players),
            from,
            cancellationToken);

        await SendTextAsync(
            "NOTE|NavBR online bridge connected.",
            from,
            cancellationToken);

        foreach (var remote in remotes)
        {
            if (!string.IsNullOrWhiteSpace(remote.LastInfo))
            {
                await SendTextAsync(
                    remote.LastInfo,
                    from,
                    cancellationToken);
            }

            if (remote.LastState is not null)
            {
                await SendBytesAsync(
                    OpenOmsiLanStateCodec.Encode(remote.LastState),
                    from,
                    cancellationToken);
            }
        }

        _ = helloInfo;
        PublishStatus();
    }

    private void HandleState(byte[] data, IPEndPoint from)
    {
        if (!IsCurrentClient(from) ||
            !OpenOmsiLanStateCodec.TryDecode(data, out var state) ||
            state.PlayerId != LocalOpenOmsiPlayerId)
        {
            return;
        }

        VehicleTelemetry telemetry;
        lock (_sync)
        {
            _lastClientPacketUtc = DateTimeOffset.UtcNow;
            telemetry = BuildLocalTelemetry(state);
            _latestLocalTelemetry = telemetry;
        }

        LocalTelemetryReceived?.Invoke(telemetry);
        PublishStatus();
    }

    private VehicleTelemetry BuildLocalTelemetry(
        OpenOmsiLanVehicleState state)
    {
        var info = _localInfo;
        var timestamp = DateTimeOffset.UtcNow;
        var doors = VehicleDoorFlags.None;
        for (var index = 0;
             index < Math.Min(state.Doors.Count, 5);
             index++)
        {
            if (state.Doors[index] <= 0.02f)
            {
                continue;
            }

            doors |= index switch
            {
                0 => VehicleDoorFlags.Front,
                1 => VehicleDoorFlags.Middle,
                2 => VehicleDoorFlags.Rear,
                3 => VehicleDoorFlags.Extra1,
                4 => VehicleDoorFlags.Extra2,
                _ => VehicleDoorFlags.None
            };
        }

        var lights = VehicleLightFlags.None;
        lights |= state.HeadLightLevel switch
        {
            >= 3 => VehicleLightFlags.Position |
                    VehicleLightFlags.LowBeam |
                    VehicleLightFlags.HighBeam,
            2 => VehicleLightFlags.Position |
                 VehicleLightFlags.LowBeam,
            1 => VehicleLightFlags.Position,
            _ => VehicleLightFlags.None
        };
        if (state.InteriorLightLevel > 0)
        {
            lights |= VehicleLightFlags.Interior;
        }

        if ((state.Flags & OpenOmsiLanProtocol.FlagFog) != 0)
        {
            lights |= VehicleLightFlags.Fog;
        }

        if ((state.Flags & OpenOmsiLanProtocol.FlagBrake) != 0)
        {
            lights |= VehicleLightFlags.Brake;
        }

        if ((state.Flags & OpenOmsiLanProtocol.FlagReverse) != 0)
        {
            lights |= VehicleLightFlags.Reverse;
        }

        var turnSignal = state.TurnSignal switch
        {
            1 => TurnSignalState.Left,
            2 => TurnSignalState.Right,
            3 => TurnSignalState.Hazard,
            _ => TurnSignalState.Off
        };
        if (turnSignal == TurnSignalState.Hazard)
        {
            lights |= VehicleLightFlags.Hazard;
        }

        var vehiclePath = info?.VehiclePath;
        var vehicleName = string.IsNullOrWhiteSpace(vehiclePath)
            ? null
            : Path.GetFileNameWithoutExtension(
                vehiclePath.Replace('/', Path.DirectorySeparatorChar));

        return new VehicleTelemetry(
            "local",
            timestamp,
            ExtractMapName(_world.Map),
            vehicleName,
            EmptyToNull(info?.Line),
            EmptyToNull(info?.Tour),
            state.X,
            state.Y,
            state.Z,
            state.HeadingDegrees,
            state.SpeedKph,
            (state.Flags & OpenOmsiLanProtocol.FlagVehicle) != 0,
            DestinationName: EmptyToNull(info?.Destination),
            VehiclePath: vehiclePath?.Replace('/', '\'),
            ThrottlePercent: state.Throttle * 100d,
            BrakePercent: state.Brake * 100d,
            SteeringDegrees: state.SteeringDegrees,
            Doors: doors,
            Lights: lights,
            TurnSignal: turnSignal,
            HornActive:
                (state.Flags & OpenOmsiLanProtocol.FlagHorn) != 0,
            WipersActive:
                (state.Flags & OpenOmsiLanProtocol.FlagWipers) != 0,
            ParkingBrakeActive:
                (state.Flags & OpenOmsiLanProtocol.FlagStopBrake) != 0,
            ReverseGear:
                (state.Flags & OpenOmsiLanProtocol.FlagReverse) != 0,
            SourceTimestampUnixMilliseconds:
                timestamp.ToUnixTimeMilliseconds());
    }

    private static OpenOmsiLanVehicleInfo BuildRemoteInfo(
        ushort id,
        PlayerTelemetryFrame frame)
    {
        var telemetry = frame.Telemetry;
        var vehiclePath = OpenOmsiLanProtocol.NormalizeVehiclePath(
            telemetry.VehiclePath ??
            frame.Player.Compatibility?.VehiclePath);

        return new OpenOmsiLanVehicleInfo(
            id,
            frame.Player.DisplayName,
            vehiclePath,
            string.Empty,
            telemetry.Line ?? string.Empty,
            telemetry.DestinationName ?? string.Empty,
            12d,
            2.55d,
            0d,
            0,
            telemetry.Route ?? string.Empty,
            [],
            null,
            []);
    }

    private static OpenOmsiLanVehicleState BuildRemoteState(
        ushort id,
        ushort sequence,
        VehicleTelemetry telemetry)
    {
        var flags =
            OpenOmsiLanProtocol.FlagVehicle |
            OpenOmsiLanProtocol.FlagEngine |
            OpenOmsiLanProtocol.FlagElectrics;
        if (telemetry.HornActive)
        {
            flags |= OpenOmsiLanProtocol.FlagHorn;
        }

        if (telemetry.ReverseGear)
        {
            flags |= OpenOmsiLanProtocol.FlagReverse;
        }

        if (telemetry.WipersActive)
        {
            flags |= OpenOmsiLanProtocol.FlagWipers;
        }

        if (telemetry.ParkingBrakeActive)
        {
            flags |= OpenOmsiLanProtocol.FlagStopBrake;
        }

        if ((telemetry.Lights & VehicleLightFlags.Brake) != 0 ||
            telemetry.BrakePercent is > 1d)
        {
            flags |= OpenOmsiLanProtocol.FlagBrake;
        }

        if ((telemetry.Lights & VehicleLightFlags.Fog) != 0)
        {
            flags |= OpenOmsiLanProtocol.FlagFog;
        }

        var head = (byte)(
            (telemetry.Lights & VehicleLightFlags.HighBeam) != 0
                ? 3
                : (telemetry.Lights & VehicleLightFlags.LowBeam) != 0
                    ? 2
                    : (telemetry.Lights & VehicleLightFlags.Position) != 0
                        ? 1
                        : 0);
        var interior = (byte)(
            (telemetry.Lights & VehicleLightFlags.Interior) != 0
                ? 3
                : 0);
        var blinker = (byte)telemetry.TurnSignal;

        var doors = new float[5];
        doors[0] =
            (telemetry.Doors & VehicleDoorFlags.Front) != 0 ? 1f : 0f;
        doors[1] =
            (telemetry.Doors & VehicleDoorFlags.Middle) != 0 ? 1f : 0f;
        doors[2] =
            (telemetry.Doors & VehicleDoorFlags.Rear) != 0 ? 1f : 0f;
        doors[3] =
            (telemetry.Doors & VehicleDoorFlags.Extra1) != 0 ? 1f : 0f;
        doors[4] =
            (telemetry.Doors & VehicleDoorFlags.Extra2) != 0 ? 1f : 0f;

        var sent = unchecked(
            (uint)telemetry.Timestamp.ToUnixTimeMilliseconds());

        return new OpenOmsiLanVehicleState(
            id,
            sequence,
            flags,
            telemetry.X,
            telemetry.Y,
            telemetry.Z,
            (float)telemetry.HeadingDegrees,
            0f,
            0f,
            (float)telemetry.SpeedKph,
            (float)(telemetry.SteeringDegrees ?? 0d),
            head,
            interior,
            blinker,
            0f,
            (float)Math.Clamp(
                (telemetry.ThrottlePercent ?? 0d) / 100d,
                0d,
                1d),
            (float)Math.Clamp(
                (telemetry.BrakePercent ?? 0d) / 100d,
                0d,
                1d),
            0,
            doors,
            [],
            [],
            [],
            [],
            [],
            null,
            sent);
    }

    private async Task RemoveRemoteCoreAsync(
        string playerId,
        IPEndPoint endpoint,
        CancellationToken cancellationToken)
    {
        RemotePeer? remote;
        lock (_sync)
        {
            if (!_remotes.Remove(playerId, out remote))
            {
                return;
            }
        }

        await SendTextAsync(
            $"BYE|{remote.Id}",
            endpoint,
            cancellationToken);
        PublishStatus();
    }

    private async Task SendTextAsync(
        string text,
        IPEndPoint endpoint,
        CancellationToken cancellationToken) =>
        await SendBytesAsync(
            Encoding.UTF8.GetBytes(text),
            endpoint,
            cancellationToken);

    private async Task SendBytesAsync(
        byte[] bytes,
        IPEndPoint endpoint,
        CancellationToken cancellationToken)
    {
        UdpClient? udp;
        lock (_sync)
        {
            udp = _udp;
        }

        if (udp is null ||
            bytes.Length == 0 ||
            bytes.Length > OpenOmsiLanProtocol.MaxDatagramBytes)
        {
            return;
        }

        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            await udp.SendAsync(
                bytes,
                endpoint,
                cancellationToken);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private bool IsCurrentClient(IPEndPoint endpoint)
    {
        lock (_sync)
        {
            return _clientEndpoint?.Equals(endpoint) == true &&
                   IsClientConnectedCore(DateTimeOffset.UtcNow);
        }
    }

    private void MarkClientPacket()
    {
        lock (_sync)
        {
            _lastClientPacketUtc = DateTimeOffset.UtcNow;
        }
    }

    private IPEndPoint? GetConnectedEndpointCore(DateTimeOffset now) =>
        IsClientConnectedCore(now)
            ? _clientEndpoint
            : null;

    private bool IsClientConnectedCore(DateTimeOffset now) =>
        _clientEndpoint is not null &&
        _lastClientPacketUtc is DateTimeOffset last &&
        now - last <= ClientTimeout;

    private OpenOmsiLanGatewayStatus BuildStatusCore(DateTimeOffset now) =>
        new(
            _udp is not null,
            (_udp?.Client.LocalEndPoint as IPEndPoint)?.Port,
            IsClientConnectedCore(now),
            _clientName,
            EmptyToNull(_world.Map),
            _localInfo?.VehiclePath,
            _remotes.Count,
            _lastClientPacketUtc,
            _lastError);

    private void PublishStatus()
    {
        OpenOmsiLanGatewayStatus status;
        lock (_sync)
        {
            status = BuildStatusCore(DateTimeOffset.UtcNow);
        }

        StatusChanged?.Invoke(status);
    }

    private void SetError(string message)
    {
        lock (_sync)
        {
            _lastError = message;
        }

        PublishStatus();
    }

    private ushort AllocateRemoteId()
    {
        for (var attempt = 0; attempt < ushort.MaxValue - 3; attempt++)
        {
            var candidate = _nextRemoteId;
            _nextRemoteId =
                _nextRemoteId == ushort.MaxValue
                    ? (ushort)3
                    : (ushort)(_nextRemoteId + 1);
            if (_remotes.Values.All(remote => remote.Id != candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "No openOMSI LAN peer id is available.");
    }

    private static UdpClient BindLoopback()
    {
        SocketException? last = null;
        for (var offset = 0; offset < PortAttempts; offset++)
        {
            var udp = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                udp.Client.Bind(
                    new IPEndPoint(
                        IPAddress.Loopback,
                        FirstPort + offset));
                return udp;
            }
            catch (SocketException ex)
            {
                last = ex;
                udp.Dispose();
            }
        }

        var fallback = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            fallback.Client.Bind(
                new IPEndPoint(IPAddress.Loopback, 0));
            return fallback;
        }
        catch
        {
            fallback.Dispose();
            throw last ?? new SocketException();
        }
    }

    private static ulong CreateSessionId()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var value =
            BitConverter.ToUInt64(bytes) & 0xFFFF_FFFF_FFFFUL;
        return value == 0 ? 1UL : value;
    }

    private static bool MapsMatch(
        string? openOmsiMap,
        string? navBrMap)
    {
        var local = NormalizeMap(openOmsiMap);
        var remote = NormalizeMap(navBrMap);
        return string.IsNullOrWhiteSpace(local) ||
               string.IsNullOrWhiteSpace(remote) ||
               string.Equals(
                   local,
                   remote,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractMapName(string? map)
    {
        var normalized = map?.Trim().Replace('\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.StartsWith(
                "maps/",
                StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[5..];
        }

        if (normalized.EndsWith(
                "/global.cfg",
                StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^11];
        }

        return normalized.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }

    private static string NormalizeMap(string? map) =>
        ExtractMapName(map)?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private sealed class RemotePeer
    {
        private ushort _sequence;

        public RemotePeer(ushort id)
        {
            Id = id;
        }

        public ushort Id { get; }

        public string? LastInfo { get; set; }

        public OpenOmsiLanVehicleState? LastState { get; set; }

        public DateTimeOffset LastSeenUtc { get; set; }

        public ushort NextSequence()
        {
            _sequence++;
            return _sequence;
        }
    }
}
