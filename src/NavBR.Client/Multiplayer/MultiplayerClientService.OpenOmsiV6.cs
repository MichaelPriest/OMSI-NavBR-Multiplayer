using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Client.OpenOmsi;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.OpenOmsi;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

public sealed partial class MultiplayerClientService
{
    private readonly object _openOmsiV6Sync = new();
    private readonly Dictionary<ushort, PlayerPresence> _openOmsiPresenceByLanId = [];
    private readonly Dictionary<string, PlayerPresence> _openOmsiPresenceByPlayerId =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ushort, OpenOmsiLanVehicleInfo> _openOmsiInfoByLanId = [];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, OpenOmsiVarTableManifest>
        _openOmsiVarTableByVehicle = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, OpenOmsiSyncTableManifest>
        _openOmsiSyncTableByVehicle = new(StringComparer.OrdinalIgnoreCase);
    private OpenOmsiLanPeerSession? _openOmsiV6Session;
    private OpenOmsiWebSocketGateway? _openOmsiWebSocketGateway;
    private OpenOmsiWebSocketClient? _openOmsiWebSocketClient;
    private OpenOmsiQuickTunnel? _openOmsiQuickTunnel;
    private string? _openOmsiPublicWebSocketUrl;
    private ushort _openOmsiLocalSequence;
    private uint? _openOmsiConfiguredLocalVarHash;
    private string? _openOmsiConfiguredLocalSampleKey;
    private DateTimeOffset? _openOmsiLastPublishedLocalVarsAt;
    private RoleplayCharacterState? _openOmsiLocalRoleplayState;

    public bool UsesOpenOmsiV6Transport => _openOmsiV6Session?.IsRunning == true;
    public bool IsOpenOmsiV6Host => _openOmsiV6Session?.IsHost == true;
    public string? OpenOmsiV6SessionCode => _openOmsiV6Session?.SessionCode;
    public int? OpenOmsiV6Port => _openOmsiV6Session?.Port;
    public string? OpenOmsiV6WebSocketUrl => _openOmsiPublicWebSocketUrl;

    private async Task ConfigureOpenOmsiV6Async(
        RoomSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await StopOpenOmsiV6Async();

        lock (_openOmsiV6Sync)
        {
            _openOmsiPresenceByLanId.Clear();
            _openOmsiPresenceByPlayerId.Clear();
            foreach (var player in snapshot.Players)
            {
                RememberOpenOmsiPresenceCore(player);
            }
        }

        if (_joinRequest is null)
        {
            return;
        }

        var world = new OpenOmsiLanWorld(
            NormalizeOpenOmsiMapPath(_joinRequest.MapName),
            DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"),
            0d,
            string.Empty,
            string.Empty);

        var session = new OpenOmsiLanPeerSession();
        session.RemoteInfoReceived += HandleOpenOmsiRemoteInfo;
        session.RemoteStateReceived += HandleOpenOmsiRemoteState;
            session.RemoteVarsReceived += HandleOpenOmsiRemoteVars;
        session.RemoteLeft += HandleOpenOmsiRemoteLeft;
        _openOmsiV6Session = session;

        if (IsTrafficAuthority)
        {
            await session.StartHostAsync(world, cancellationToken);
            await StartOpenOmsiWebSocketGatewayAsync(session, cancellationToken);
            await PublishOpenOmsiTransportPresenceAsync(
                session,
                ResolveLanAdvertiseAddress(),
                cancellationToken);
            return;
        }

        var authority = snapshot.Players.FirstOrDefault(player =>
            string.Equals(
                player.PlayerId,
                snapshot.TrafficAuthorityPlayerId,
                StringComparison.OrdinalIgnoreCase));

        if (authority?.OpenOmsiTransport is { IsValid: true } transport)
        {
            await JoinAdvertisedOpenOmsiTransportAsync(
                transport,
                world,
                cancellationToken);
            if (_openOmsiV6Session?.IsRunning == true)
            {
                return;
            }
        }

        // Sidecar discovery can lag behind the physical session. Fall back to
        // the native openOMSI LAN discovery packet so local rooms remain usable
        // without waiting for the service plane.
        var discovered = await OpenOmsiLanPeerSession.DiscoverHostAsync(
            cancellationToken);
        if (discovered is not null)
        {
            await session.JoinAsync(
                discovered,
                world,
                _joinRequest.DisplayName,
                _joinRequest.Compatibility?.VehiclePath,
                cancellationToken);

            var connection = _connection;
            if (connection?.State == HubConnectionState.Connected)
            {
                await connection.InvokeAsync(
                    "UpdateOpenOmsiLanId",
                    (ushort?)session.LocalPlayerId,
                    cancellationToken);
            }
            return;
        }

        // Keep NavBR services connected and wait for the authority transport
        // advertisement if no LAN host answered discovery.
        await session.DisposeAsync();
        if (ReferenceEquals(_openOmsiV6Session, session))
        {
            _openOmsiV6Session = null;
        }
    }

    private async Task JoinAdvertisedOpenOmsiTransportAsync(
        OpenOmsiTransportDescriptor transport,
        OpenOmsiLanWorld world,
        CancellationToken cancellationToken)
    {
        var session = _openOmsiV6Session;
        if (session is null)
        {
            session = new OpenOmsiLanPeerSession();
            session.RemoteInfoReceived += HandleOpenOmsiRemoteInfo;
            session.RemoteStateReceived += HandleOpenOmsiRemoteState;
            session.RemoteVarsReceived += HandleOpenOmsiRemoteVars;
            session.RemoteLeft += HandleOpenOmsiRemoteLeft;
            _openOmsiV6Session = session;
        }

        if (session.IsRunning)
        {
            return;
        }

        Exception? udpError = null;
        if (transport.HasUdpEndpoint &&
            IPAddress.TryParse(transport.Host, out var hostAddress))
        {
            try
            {
                await session.JoinAsync(
                    new IPEndPoint(hostAddress, transport.Port),
                    world,
                    _joinRequest?.DisplayName ?? "Driver",
                    _joinRequest?.Compatibility?.VehiclePath,
                    cancellationToken);
            }
            catch (Exception ex) when (
                transport.HasWebSocketEndpoint &&
                ex is TimeoutException or SocketException)
            {
                udpError = ex;
            }
        }

        if (!session.IsRunning || session.LocalPlayerId == 0)
        {
            if (!transport.HasWebSocketEndpoint)
            {
                if (udpError is not null)
                {
                    throw udpError;
                }
                return;
            }

            _openOmsiWebSocketClient = await OpenOmsiWebSocketClient.ConnectAsync(
                transport.WebSocketUrl!,
                cancellationToken);
            await session.JoinAsync(
                _openOmsiWebSocketClient.LocalEndpoint,
                world,
                _joinRequest?.DisplayName ?? "Driver",
                _joinRequest?.Compatibility?.VehiclePath,
                cancellationToken);
        }

        var connection = _connection;
        if (connection?.State == HubConnectionState.Connected)
        {
            await connection.InvokeAsync(
                "UpdateOpenOmsiLanId",
                (ushort?)session.LocalPlayerId,
                cancellationToken);
        }
    }

    private async Task PublishOpenOmsiTransportPresenceAsync(
        OpenOmsiLanPeerSession session,
        string address,
        CancellationToken cancellationToken)
    {
        var connection = _connection;
        if (connection?.State != HubConnectionState.Connected ||
            session.Port is not int port)
        {
            return;
        }

        var descriptor = new OpenOmsiTransportDescriptor(
            OpenOmsiLanProtocol.ProtocolVersion,
            address,
            port,
            OpenOmsiLanProtocol.SessionHex(session.SessionId))
        {
            SessionCode = session.SessionCode,
            WebSocketUrl = _openOmsiPublicWebSocketUrl
        };

        await connection.InvokeAsync(
            "UpdateOpenOmsiTransport",
            descriptor,
            cancellationToken);
        await connection.InvokeAsync(
            "UpdateOpenOmsiLanId",
            (ushort?)session.LocalPlayerId,
            cancellationToken);
    }

    private async Task HandleOpenOmsiPresenceAsync(PlayerPresence presence)
    {
        lock (_openOmsiV6Sync)
        {
            RememberOpenOmsiPresenceCore(presence);
        }

        if (IsTrafficAuthority ||
            _openOmsiV6Session?.IsRunning == true ||
            !string.Equals(
                presence.PlayerId,
                TrafficAuthorityPlayerId,
                StringComparison.OrdinalIgnoreCase) ||
            presence.OpenOmsiTransport is not { IsValid: true } transport)
        {
            return;
        }

        var world = new OpenOmsiLanWorld(
            NormalizeOpenOmsiMapPath(_joinRequest?.MapName),
            DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"),
            0d,
            string.Empty,
            string.Empty);

        try
        {
            await JoinAdvertisedOpenOmsiTransportAsync(
                transport,
                world,
                CancellationToken.None);
        }
        catch
        {
            // Sidecar stays alive; the reconnect path can retry when presence
            // or room metadata changes.
        }
    }

    private void RememberOpenOmsiPresenceCore(PlayerPresence player)
    {
        _openOmsiPresenceByPlayerId[player.PlayerId] = player;
        foreach (var stale in _openOmsiPresenceByLanId
                     .Where(pair => string.Equals(
                         pair.Value.PlayerId,
                         player.PlayerId,
                         StringComparison.OrdinalIgnoreCase))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _openOmsiPresenceByLanId.Remove(stale);
        }

        if (player.OpenOmsiLanId is ushort id && id != 0)
        {
            _openOmsiPresenceByLanId[id] = player;
        }
    }

    private void HandleOpenOmsiRemoteInfo(OpenOmsiLanVehicleInfo info)
    {
        lock (_openOmsiV6Sync)
        {
            _openOmsiInfoByLanId[info.PlayerId] = info;
        }
    }

    private void HandleOpenOmsiRemoteState(OpenOmsiLanVehicleState state)
    {
        PlayerPresence? presence;
        OpenOmsiLanVehicleInfo? info;
        lock (_openOmsiV6Sync)
        {
            _openOmsiPresenceByLanId.TryGetValue(state.PlayerId, out presence);
            _openOmsiInfoByLanId.TryGetValue(state.PlayerId, out info);
        }

        if (presence is null || _joinRequest is null)
        {
            return;
        }

        var doors = VehicleDoorFlags.None;
        for (var index = 0; index < Math.Min(5, state.Doors.Count); index++)
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
        if (state.HeadLightLevel >= 1) lights |= VehicleLightFlags.Position;
        if (state.HeadLightLevel >= 2) lights |= VehicleLightFlags.LowBeam;
        if (state.HeadLightLevel >= 3) lights |= VehicleLightFlags.HighBeam;
        if (state.InteriorLightLevel > 0) lights |= VehicleLightFlags.Interior;
        if ((state.Flags & OpenOmsiLanProtocol.FlagFog) != 0) lights |= VehicleLightFlags.Fog;
        if ((state.Flags & OpenOmsiLanProtocol.FlagBrake) != 0) lights |= VehicleLightFlags.Brake;
        if ((state.Flags & OpenOmsiLanProtocol.FlagReverse) != 0) lights |= VehicleLightFlags.Reverse;

        var turn = state.TurnSignal switch
        {
            1 => TurnSignalState.Left,
            2 => TurnSignalState.Right,
            3 => TurnSignalState.Hazard,
            _ => TurnSignalState.Off
        };

        var timestamp = DateTimeOffset.UtcNow;
        var telemetry = new VehicleTelemetry(
            presence.PlayerId,
            timestamp,
            presence.MapName,
            info?.VehiclePath is { Length: > 0 } path
                ? Path.GetFileNameWithoutExtension(path)
                : presence.Compatibility?.VehiclePath is { Length: > 0 } compatPath
                    ? Path.GetFileNameWithoutExtension(compatPath)
                    : null,
            info?.Line,
            info?.Tour,
            state.X,
            state.Z,
            state.Y,
            state.HeadingDegrees,
            state.SpeedKph,
            (state.Flags & OpenOmsiLanProtocol.FlagVehicle) != 0,
            MapCompatibilityId: presence.MapCompatibilityId,
            DestinationName: info?.Destination,
            VehiclePath: (info?.VehiclePath ?? presence.Compatibility?.VehiclePath)?.Replace('/', '\\'),
            ThrottlePercent: state.Throttle * 100d,
            BrakePercent: state.Brake * 100d,
            SteeringDegrees: state.SteeringDegrees,
            Doors: doors,
            Lights: lights,
            TurnSignal: turn,
            HornActive: (state.Flags & OpenOmsiLanProtocol.FlagHorn) != 0,
            WipersActive: (state.Flags & OpenOmsiLanProtocol.FlagWipers) != 0,
            ParkingBrakeActive: (state.Flags & OpenOmsiLanProtocol.FlagStopBrake) != 0,
            ReverseGear: (state.Flags & OpenOmsiLanProtocol.FlagReverse) != 0,
            VehicleCompatibilityId: presence.Compatibility?.VehicleCompatibilityId,
            HofCompatibilityId: presence.Compatibility?.HofCompatibilityId,
            SourceTimestampUnixMilliseconds: timestamp.ToUnixTimeMilliseconds(),
            RearSections: state.RearSections
                .Take(OpenOmsiLanProtocol.MaxRearSections)
                .Select(section =>
                {
                    var half = section.HeadingDegrees * (Math.PI / 360d);
                    return new VehicleSectionPose(
                        LocalX: section.X,
                        LocalY: section.Z,
                        LocalZ: section.Y,
                        RotationX: 0d,
                        RotationY: Math.Sin(half),
                        RotationZ: 0d,
                        RotationW: Math.Cos(half),
                        GridX: 0,
                        GridY: 0,
                        MapTileIndex: null);
                })
                .ToArray(),
            SyncTableHash: info?.SyncTableHash,
            OpenOmsiLamps: state.Lamps.ToArray(),
            OpenOmsiSwitches: state.Switches.ToArray(),
            OpenOmsiValues: state.Values.ToArray());

        var frame = new PlayerTelemetryFrame(presence, telemetry);
        TelemetryReceived?.Invoke(frame);
        _ = OmsiPluginBridgeRelay.ForwardRemoteTelemetryAsync(frame);
        _ = RouteRemotePhysicalTelemetryAsync(frame);

        if (state.Walker is { } walker)
        {
            ApplyOpenOmsiWalkerPresence(presence, walker, timestamp);
        }
    }

    private void ApplyOpenOmsiWalkerPresence(
        PlayerPresence presence,
        OpenOmsiLanWalker walker,
        DateTimeOffset timestamp)
    {
        var state = new RoleplayCharacterState(
            presence.PlayerId,
            timestamp,
            presence.MapName,
            presence.MapCompatibilityId,
            walker.X,
            walker.Z,
            walker.Y,
            walker.HeadingDegrees,
            walker.SpeedMps,
            walker.SpeedMps > 0.2f
                ? RoleplayCharacterActivity.Walking
                : RoleplayCharacterActivity.Idle,
            true,
            null,
            presence.DisplayName,
            null);
        ApplyRoleplayCharacter(new RoleplayCharacterFrame(presence, state));
    }

    private void HandleOpenOmsiRemoteVars(OpenOmsiVarsFrame vars) =>
        _ = HandleOpenOmsiRemoteVarsAsync(vars);

    private async Task HandleOpenOmsiRemoteVarsAsync(OpenOmsiVarsFrame vars)
    {
        PlayerPresence? presence;
        OpenOmsiLanVehicleInfo? info;
        lock (_openOmsiV6Sync)
        {
            _openOmsiPresenceByLanId.TryGetValue(vars.PlayerId, out presence);
            _openOmsiInfoByLanId.TryGetValue(vars.PlayerId, out info);
        }

        if (presence is null)
        {
            return;
        }

        var vehiclePath =
            info?.VehiclePath ??
            presence.Compatibility?.VehiclePath;
        if (string.IsNullOrWhiteSpace(vehiclePath))
        {
            return;
        }

        var manifest = await ResolveLocalOpenOmsiVarTableAsync(vehiclePath);
        if (manifest is null ||
            manifest.Hash != vars.TableHash)
        {
            return;
        }

        await OmsiPluginBridgeRelay.ForwardRemoteVarsAsync(
            presence.PlayerId,
            vars);
    }

    private async Task<OpenOmsiVarTableManifest?>
        ResolveLocalOpenOmsiVarTableAsync(string vehiclePath)
    {
        if (_openOmsiVarTableByVehicle.TryGetValue(
                vehiclePath,
                out var cached))
        {
            return cached;
        }

        var roots =
            OpenOmsiEnvironmentLocator.ResolveContentSearchRoots();
        var manifest = await Task.Run(
            () =>
            {
                foreach (var root in roots)
                {
                    var candidate =
                        OpenOmsiVarTableManifestBuilder.TryBuild(
                            root,
                            vehiclePath);
                    if (candidate is not null)
                    {
                        return candidate;
                    }
                }

                return null;
            });

        if (manifest is not null)
        {
            _openOmsiVarTableByVehicle[vehiclePath] = manifest;
        }

        return manifest;
    }


    private async Task<OpenOmsiSyncTableManifest?>
        ResolveLocalOpenOmsiSyncTableAsync(
            string vehiclePath,
            OpenOmsiVarTableManifest variables)
    {
        var key =
            $"{vehiclePath}|{variables.Hash:X8}";
        if (_openOmsiSyncTableByVehicle.TryGetValue(
                key,
                out var cached))
        {
            return cached;
        }

        var roots =
            OpenOmsiEnvironmentLocator.ResolveContentSearchRoots();
        var manifest = await Task.Run(
            () =>
            {
                foreach (var root in roots)
                {
                    var candidate =
                        OpenOmsiSyncTableManifestBuilder.TryBuild(
                            root,
                            vehiclePath,
                            variables);
                    if (candidate is not null)
                    {
                        return candidate;
                    }
                }

                return null;
            });

        if (manifest is not null)
        {
            _openOmsiSyncTableByVehicle[key] = manifest;
        }

        return manifest;
    }

    private async Task EnsureLocalOpenOmsiSamplingConfiguredAsync(
        OpenOmsiVarTableManifest variables,
        OpenOmsiSyncTableManifest? syncTable,
        CancellationToken cancellationToken)
    {
        var visualIds = syncTable is null
            ? Enumerable.Empty<ushort>()
            : syncTable.LampIds
                .Concat(syncTable.SwitchIds)
                .Concat(syncTable.ValueIds)
                .Concat(syncTable.DoorIds);

        var sampleIds = visualIds
            .Concat(variables.FloatIds)
            .Distinct()
            .Take(512)
            .ToArray();
        var stringIds = variables.StringIds
            .Distinct()
            .Take(64)
            .ToArray();

        var key =
            $"{variables.Hash:X8}|" +
            string.Join(',', sampleIds) +
            "|" +
            string.Join(',', stringIds);

        if (string.Equals(
                _openOmsiConfiguredLocalSampleKey,
                key,
                StringComparison.Ordinal))
        {
            return;
        }

        await OmsiPluginBridgeRelay.ConfigureLocalVarsAsync(
            variables.Hash,
            sampleIds,
            stringIds,
            cancellationToken);
        _openOmsiConfiguredLocalVarHash = variables.Hash;
        _openOmsiConfiguredLocalSampleKey = key;
        _openOmsiLastPublishedLocalVarsAt = null;
        LocalOmsiScriptVarsSnapshotStore.Clear();
    }

    private static bool IsSyncTableSnapshotReady(
        OpenOmsiSyncTableManifest? syncTable,
        LocalOmsiScriptVarsSnapshot? snapshot)
    {
        if (syncTable is null || snapshot is null)
        {
            return false;
        }

        var ids = snapshot.VariableIndices.ToHashSet();
        return syncTable.LampIds.All(ids.Contains) &&
               syncTable.SwitchIds.All(ids.Contains) &&
               syncTable.ValueIds.All(ids.Contains) &&
               syncTable.DoorIds.All(ids.Contains);
    }

    private void HandleOpenOmsiRemoteLeft(ushort lanId)
    {
        PlayerPresence? presence;
        lock (_openOmsiV6Sync)
        {
            _openOmsiInfoByLanId.Remove(lanId);
            if (!_openOmsiPresenceByLanId.Remove(lanId, out presence))
            {
                presence = null;
            }
        }

        if (presence is null)
        {
            return;
        }

        _ = _physicalVehicles.DespawnAsync(presence.PlayerId);
        RemoveRoleplayCharacter(presence.PlayerId);
    }

    private async Task PublishOpenOmsiTelemetryAsync(
        VehicleTelemetry telemetry,
        CancellationToken cancellationToken)
    {
        var session = _openOmsiV6Session;
        if (session?.IsRunning != true || session.LocalPlayerId == 0 || _joinRequest is null)
        {
            return;
        }

        var localPresence = new PlayerPresence(
            _joinRequest.PlayerId,
            _joinRequest.DisplayName,
            _joinRequest.RoomId,
            telemetry.MapName,
            DateTimeOffset.UtcNow,
            telemetry.MapCompatibilityId,
            _joinRequest.Compatibility);

        var frame = new PlayerTelemetryFrame(localPresence, telemetry);
        OpenOmsiVarTableManifest? varTable = null;
        OpenOmsiSyncTableManifest? syncTable = null;
        LocalOmsiScriptVarsSnapshot? scriptSnapshot =
            LocalOmsiScriptVarsSnapshotStore.Latest;

        if (!string.IsNullOrWhiteSpace(telemetry.VehiclePath))
        {
            varTable = await ResolveLocalOpenOmsiVarTableAsync(
                telemetry.VehiclePath);
            if (varTable is not null)
            {
                syncTable = await ResolveLocalOpenOmsiSyncTableAsync(
                    telemetry.VehiclePath,
                    varTable);
                await EnsureLocalOpenOmsiSamplingConfiguredAsync(
                    varTable,
                    syncTable,
                    cancellationToken);
                scriptSnapshot =
                    LocalOmsiScriptVarsSnapshotStore.Latest;
            }
        }

        var visualSnapshot =
            BuildOpenOmsiVisualSnapshot(
                syncTable,
                scriptSnapshot);

        var baseState = BuildOpenOmsiLocalState(
            session.LocalPlayerId,
            unchecked(++_openOmsiLocalSequence),
            frame,
            visualSnapshot,
            (uint)Environment.TickCount64);

        var walker = BuildOpenOmsiLocalWalker(baseState, telemetry);
        var state = baseState with { Walker = walker };

        var info = BuildOpenOmsiLocalInfo(
            session.LocalPlayerId,
            frame,
            syncTable?.Hash ?? 0u);
        await session.PublishInfoAsync(info, cancellationToken);
        await session.PublishStateAsync(state, cancellationToken);

        await PublishOpenOmsiLocalVarsAsync(
            session,
            telemetry,
            cancellationToken);
    }

    private async Task PublishOpenOmsiLocalVarsAsync(
        OpenOmsiLanPeerSession session,
        VehicleTelemetry telemetry,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(telemetry.VehiclePath))
        {
            return;
        }

        var manifest = await ResolveLocalOpenOmsiVarTableAsync(
            telemetry.VehiclePath);
        if (manifest is null ||
            manifest.Hash == 0 ||
            (manifest.FloatIds.Length == 0 &&
             manifest.StringIds.Length == 0))
        {
            return;
        }

        if (_openOmsiConfiguredLocalVarHash != manifest.Hash)
        {
            await OmsiPluginBridgeRelay.ConfigureLocalVarsAsync(
                manifest.Hash,
                manifest.FloatIds,
                manifest.StringIds,
                cancellationToken);
            _openOmsiConfiguredLocalVarHash = manifest.Hash;
            _openOmsiLastPublishedLocalVarsAt = null;
        }

        var snapshot =
            LocalOmsiScriptVarsSnapshotStore.Latest;
        if (snapshot is null ||
            snapshot.VarTableHash != manifest.Hash ||
            snapshot.VariableIndices.Length !=
                snapshot.VariableValues.Length ||
            snapshot.StringVariableIndices.Length !=
                snapshot.StringVariableValues.Length ||
            DateTimeOffset.UtcNow - snapshot.CapturedAtUtc >
                TimeSpan.FromSeconds(2) ||
            _openOmsiLastPublishedLocalVarsAt is DateTimeOffset last &&
            snapshot.CapturedAtUtc <= last)
        {
            return;
        }

        var allowedFloatIds = manifest.FloatIds.ToHashSet();
        var floats = snapshot.VariableIndices
            .Zip(
                snapshot.VariableValues,
                static (id, value) => (id, value))
            .Where(item => allowedFloatIds.Contains(item.id))
            .ToArray();

        var allowedStringIds = manifest.StringIds.ToHashSet();
        var strings = snapshot.StringVariableIndices
            .Zip(
                snapshot.StringVariableValues,
                static (id, value) => (id, value))
            .Where(item => allowedStringIds.Contains(item.id))
            .ToArray();

        await session.PublishVarsAsync(
            new OpenOmsiVarsFrame(
                session.LocalPlayerId,
                snapshot.VarTableHash,
                floats,
                strings),
            cancellationToken);

        _openOmsiLastPublishedLocalVarsAt =
            snapshot.CapturedAtUtc;
    }


    private static OpenOmsiLanVehicleInfo BuildOpenOmsiLocalInfo(
        ushort id,
        PlayerTelemetryFrame frame,
        uint syncTableHash)
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
            syncTableHash,
            telemetry.Route ?? string.Empty,
            [],
            null,
            []);
    }

    private static OpenOmsiLanVehicleState BuildOpenOmsiLocalState(
        ushort id,
        ushort sequence,
        PlayerTelemetryFrame frame,
        OpenOmsiLocalVisualSnapshot? visual,
        uint sentMilliseconds)
    {
        var telemetry = frame.Telemetry;

        var flags =
            OpenOmsiLanProtocol.FlagVehicle |
            OpenOmsiLanProtocol.FlagEngine |
            OpenOmsiLanProtocol.FlagElectrics;
        if (telemetry.HornActive) flags |= OpenOmsiLanProtocol.FlagHorn;
        if (telemetry.ReverseGear) flags |= OpenOmsiLanProtocol.FlagReverse;
        if (telemetry.WipersActive) flags |= OpenOmsiLanProtocol.FlagWipers;
        if (telemetry.ParkingBrakeActive) flags |= OpenOmsiLanProtocol.FlagStopBrake;
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
            (telemetry.Lights & VehicleLightFlags.HighBeam) != 0 ? 3 :
            (telemetry.Lights & VehicleLightFlags.LowBeam) != 0 ? 2 :
            (telemetry.Lights & VehicleLightFlags.Position) != 0 ? 1 : 0);
        var interior = (byte)(
            (telemetry.Lights & VehicleLightFlags.Interior) != 0 ? 3 : 0);

        IReadOnlyList<float> doors;
        if (visual is { Doors.Length: > 0 })
        {
            doors = visual.Doors;
        }
        else
        {
            var fallbackDoors = new float[5];
            fallbackDoors[0] =
                (telemetry.Doors & VehicleDoorFlags.Front) != 0 ? 1f : 0f;
            fallbackDoors[1] =
                (telemetry.Doors & VehicleDoorFlags.Middle) != 0 ? 1f : 0f;
            fallbackDoors[2] =
                (telemetry.Doors & VehicleDoorFlags.Rear) != 0 ? 1f : 0f;
            fallbackDoors[3] =
                (telemetry.Doors & VehicleDoorFlags.Extra1) != 0 ? 1f : 0f;
            fallbackDoors[4] =
                (telemetry.Doors & VehicleDoorFlags.Extra2) != 0 ? 1f : 0f;
            doors = fallbackDoors;
        }

        var useNativeAxes =
            telemetry.LocalX is double &&
            telemetry.LocalY is double &&
            telemetry.LocalZ is double;
        var rearSections = useNativeAxes
            ? BuildOpenOmsiLocalRearSections(telemetry)
            : Array.Empty<OpenOmsiLanPartPose>();

        return new OpenOmsiLanVehicleState(
            id,
            sequence,
            flags,
            telemetry.X,
            useNativeAxes ? telemetry.Z : telemetry.Y,
            useNativeAxes ? telemetry.Y : telemetry.Z,
            (float)telemetry.HeadingDegrees,
            0f,
            0f,
            (float)telemetry.SpeedKph,
            (float)(telemetry.SteeringDegrees ?? 0d),
            head,
            interior,
            (byte)telemetry.TurnSignal,
            0f,
            (float)Math.Clamp((telemetry.ThrottlePercent ?? 0d) / 100d, 0d, 1d),
            (float)Math.Clamp((telemetry.BrakePercent ?? 0d) / 100d, 0d, 1d),
            0,
            doors,
            [],
            rearSections,
            visual?.Lamps ?? [],
            visual?.Switches ?? [],
            visual?.Values ?? [],
            null,
            sentMilliseconds);
    }

    private sealed record OpenOmsiLocalVisualSnapshot(
        float[] Lamps,
        float[] Switches,
        float[] Values,
        float[] Doors);

    private static OpenOmsiLocalVisualSnapshot?
        BuildOpenOmsiVisualSnapshot(
            OpenOmsiSyncTableManifest? syncTable,
            LocalOmsiScriptVarsSnapshot? snapshot)
    {
        if (!IsSyncTableSnapshotReady(syncTable, snapshot) ||
            syncTable is null ||
            snapshot is null)
        {
            return null;
        }

        var valuesById = snapshot.VariableIndices
            .Zip(
                snapshot.VariableValues,
                static (id, value) => (id, value))
            .ToDictionary(
                item => item.id,
                item => item.value);

        float[] Resolve(IReadOnlyList<ushort> ids)
        {
            var values = new float[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                if (!valuesById.TryGetValue(ids[i], out values[i]) ||
                    !float.IsFinite(values[i]))
                {
                    return [];
                }
            }

            return values;
        }

        var lamps = Resolve(syncTable.LampIds);
        var switches = Resolve(syncTable.SwitchIds);
        var values = Resolve(syncTable.ValueIds);
        var doors = Resolve(syncTable.DoorIds);

        if ((syncTable.LampIds.Length > 0 && lamps.Length == 0) ||
            (syncTable.SwitchIds.Length > 0 && switches.Length == 0) ||
            (syncTable.ValueIds.Length > 0 && values.Length == 0) ||
            (syncTable.DoorIds.Length > 0 && doors.Length == 0))
        {
            return null;
        }

        return new OpenOmsiLocalVisualSnapshot(
            lamps,
            switches,
            values,
            doors);
    }

    private static IReadOnlyList<OpenOmsiLanPartPose>
        BuildOpenOmsiLocalRearSections(VehicleTelemetry telemetry)
    {
        if (telemetry.RearSections is not { Length: > 0 } sections ||
            telemetry.LocalX is not double frontLocalX ||
            telemetry.LocalY is not double frontLocalY ||
            telemetry.LocalZ is not double frontLocalZ)
        {
            return [];
        }

        var frontGridX = telemetry.PhysicalGridX ?? telemetry.GridX;
        var frontGridY = telemetry.PhysicalGridY ?? telemetry.GridY;
        if (frontGridX is not int gx || frontGridY is not int gy)
        {
            return [];
        }

        var tileSize = InferOpenOmsiTileSize(
            telemetry,
            gx,
            gy,
            frontLocalX,
            frontLocalZ);

        var result = new List<OpenOmsiLanPartPose>(
            Math.Min(
                sections.Length,
                OpenOmsiLanProtocol.MaxRearSections));

        foreach (var section in sections.Take(OpenOmsiLanProtocol.MaxRearSections))
        {
            var gridDeltaX = section.GridX - gx;
            var gridDeltaY = section.GridY - gy;
            if ((gridDeltaX != 0 || gridDeltaY != 0) &&
                tileSize is null)
            {
                continue;
            }

            var dx =
                section.LocalX - frontLocalX +
                gridDeltaX * (tileSize ?? 0d);
            var dy =
                section.LocalZ - frontLocalZ +
                gridDeltaY * (tileSize ?? 0d);
            var dz = section.LocalY - frontLocalY;
            var heading = OpenOmsiQuaternionHeading(
                section.RotationX,
                section.RotationY,
                section.RotationZ,
                section.RotationW);

            if (!double.IsFinite(dx) ||
                !double.IsFinite(dy) ||
                !double.IsFinite(dz) ||
                !double.IsFinite(heading))
            {
                continue;
            }

            result.Add(
                new OpenOmsiLanPartPose(
                    telemetry.X + dx,
                    telemetry.Z + dy,
                    telemetry.Y + dz,
                    (float)heading));
        }

        return result;
    }

    private static double? InferOpenOmsiTileSize(
        VehicleTelemetry telemetry,
        int gridX,
        int gridY,
        double localX,
        double localZ)
    {
        var candidates = new List<double>(2);
        if (gridX != 0)
        {
            var value = Math.Abs((telemetry.X - localX) / gridX);
            if (double.IsFinite(value) && value is >= 250d and <= 500d)
            {
                candidates.Add(value);
            }
        }

        if (gridY != 0)
        {
            var value = Math.Abs((telemetry.Z - localZ) / gridY);
            if (double.IsFinite(value) && value is >= 250d and <= 500d)
            {
                candidates.Add(value);
            }
        }

        return candidates.Count switch
        {
            0 => null,
            1 => candidates[0],
            _ => Math.Abs(candidates[0] - candidates[1]) <= 2d
                ? (candidates[0] + candidates[1]) * 0.5d
                : null
        };
    }

    private static double OpenOmsiQuaternionHeading(
        double x,
        double y,
        double z,
        double w)
    {
        var lengthSquared = x * x + y * y + z * z + w * w;
        if (!double.IsFinite(lengthSquared) || lengthSquared < 0.00000001d)
        {
            return double.NaN;
        }

        var inverseLength = 1d / Math.Sqrt(lengthSquared);
        x *= inverseLength;
        y *= inverseLength;
        z *= inverseLength;
        w *= inverseLength;

        var sinYaw = 2d * (w * y + x * z);
        var cosYaw = 1d - 2d * (y * y + z * z);
        var degrees = Math.Atan2(sinYaw, cosYaw) * (180d / Math.PI);
        return (degrees + 360d) % 360d;
    }

    private OpenOmsiLanWalker? BuildOpenOmsiLocalWalker(
        OpenOmsiLanVehicleState vehicle,
        VehicleTelemetry telemetry)
    {
        var rp = _openOmsiLocalRoleplayState;
        if (rp?.IsActive != true)
        {
            return null;
        }

        var worldX = rp.LocalX;
        var worldY = rp.LocalZ;
        var worldZ = rp.LocalY;

        if (telemetry.LocalX is double busLocalX &&
            telemetry.LocalY is double busLocalY &&
            telemetry.LocalZ is double busLocalZ)
        {
            worldX = vehicle.X + (rp.LocalX - busLocalX);
            worldY = vehicle.Y + (rp.LocalZ - busLocalZ);
            worldZ = vehicle.Z + (rp.LocalY - busLocalY);
        }

        return new OpenOmsiLanWalker(
            worldX,
            worldY,
            worldZ,
            (float)rp.HeadingDegrees,
            (float)rp.SpeedMps,
            (float)rp.HeadingDegrees,
            false,
            null,
            null,
            null);
    }

    private void SetOpenOmsiLocalRoleplayState(RoleplayCharacterState? state) =>
        _openOmsiLocalRoleplayState = state;

    private async Task RepublishOpenOmsiV6PresenceAsync(
        CancellationToken cancellationToken = default)
    {
        var session = _openOmsiV6Session;
        var connection = _connection;
        if (session?.IsRunning != true ||
            connection?.State != HubConnectionState.Connected)
        {
            return;
        }

        if (session.IsHost)
        {
            await PublishOpenOmsiTransportPresenceAsync(
                session,
                ResolveLanAdvertiseAddress(),
                cancellationToken);
        }
        else
        {
            await connection.InvokeAsync(
                "UpdateOpenOmsiLanId",
                (ushort?)session.LocalPlayerId,
                cancellationToken);
        }
    }

    private async Task HandleOpenOmsiAuthorityChangedAsync()
    {
        if (_joinRequest is null)
        {
            return;
        }

        if (IsTrafficAuthority)
        {
            if (_openOmsiV6Session?.IsHost == true)
            {
                await RepublishOpenOmsiV6PresenceAsync();
                return;
            }

            await StopOpenOmsiV6Async();
            var session = new OpenOmsiLanPeerSession();
            session.RemoteInfoReceived += HandleOpenOmsiRemoteInfo;
            session.RemoteStateReceived += HandleOpenOmsiRemoteState;
            session.RemoteVarsReceived += HandleOpenOmsiRemoteVars;
            session.RemoteLeft += HandleOpenOmsiRemoteLeft;
            _openOmsiV6Session = session;

            var world = new OpenOmsiLanWorld(
                NormalizeOpenOmsiMapPath(_joinRequest.MapName),
                DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"),
                0d,
                string.Empty,
                string.Empty);
            await session.StartHostAsync(world);
            await StartOpenOmsiWebSocketGatewayAsync(
                session,
                CancellationToken.None);
            await PublishOpenOmsiTransportPresenceAsync(
                session,
                ResolveLanAdvertiseAddress(),
                CancellationToken.None);
            return;
        }

        // Authority changed: a client must leave the previous v6 host as well,
        // otherwise it would keep receiving motion from a room authority that
        // no longer owns the shared session.
        if (_openOmsiV6Session?.IsRunning == true)
        {
            await StopOpenOmsiV6Async();
        }

        PlayerPresence? authority = null;
        lock (_openOmsiV6Sync)
        {
            if (!string.IsNullOrWhiteSpace(TrafficAuthorityPlayerId))
            {
                _openOmsiPresenceByPlayerId.TryGetValue(
                    TrafficAuthorityPlayerId,
                    out authority);
            }
        }

        if (authority is not null)
        {
            await HandleOpenOmsiPresenceAsync(authority);
        }
    }


    private async Task StartOpenOmsiWebSocketGatewayAsync(
        OpenOmsiLanPeerSession session,
        CancellationToken cancellationToken)
    {
        if (session.Port is not int udpPort)
        {
            return;
        }

        if (_openOmsiWebSocketGateway is not null)
        {
            await _openOmsiWebSocketGateway.DisposeAsync();
            _openOmsiWebSocketGateway = null;
        }

        var webPort = Math.Min(65535, udpPort + 10);
        _openOmsiWebSocketGateway =
            await OpenOmsiWebSocketGateway.StartAsync(
                webPort,
                new IPEndPoint(IPAddress.Loopback, udpPort),
                cancellationToken);

        _openOmsiPublicWebSocketUrl =
            $"http://127.0.0.1:{_openOmsiWebSocketGateway.Port}";

        if (_openOmsiQuickTunnel is not null)
        {
            await _openOmsiQuickTunnel.DisposeAsync();
            _openOmsiQuickTunnel = null;
        }

        _openOmsiQuickTunnel = await OpenOmsiQuickTunnel.StartAsync(
            _openOmsiWebSocketGateway.Port,
            cancellationToken);
        if (_openOmsiQuickTunnel is not null)
        {
            var publicUrl = await _openOmsiQuickTunnel.WaitForUrlAsync(
                TimeSpan.FromSeconds(15),
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(publicUrl))
            {
                _openOmsiPublicWebSocketUrl = publicUrl;
            }
        }
    }

    internal void SetOpenOmsiPublicWebSocketUrl(string? url)
    {
        _openOmsiPublicWebSocketUrl =
            string.IsNullOrWhiteSpace(url)
                ? null
                : url.Trim();
    }

    private async Task StopOpenOmsiV6Async()
    {
        var session = _openOmsiV6Session;
        var webSocketClient = _openOmsiWebSocketClient;
        var webSocketGateway = _openOmsiWebSocketGateway;
        var quickTunnel = _openOmsiQuickTunnel;
        _openOmsiV6Session = null;
        _openOmsiWebSocketClient = null;
        _openOmsiWebSocketGateway = null;
        _openOmsiQuickTunnel = null;
        _openOmsiPublicWebSocketUrl = null;
        _openOmsiLocalRoleplayState = null;
        _openOmsiConfiguredLocalVarHash = null;
        _openOmsiConfiguredLocalSampleKey = null;
        _openOmsiLastPublishedLocalVarsAt = null;
        LocalOmsiScriptVarsSnapshotStore.Clear();
        lock (_openOmsiV6Sync)
        {
            _openOmsiPresenceByLanId.Clear();
            _openOmsiPresenceByPlayerId.Clear();
            _openOmsiInfoByLanId.Clear();
        }
        _openOmsiVarTableByVehicle.Clear();
        _openOmsiSyncTableByVehicle.Clear();

        if (session is not null)
        {
            await session.DisposeAsync();
        }

        if (webSocketClient is not null)
        {
            await webSocketClient.DisposeAsync();
        }

        if (webSocketGateway is not null)
        {
            await webSocketGateway.DisposeAsync();
        }

        if (quickTunnel is not null)
        {
            await quickTunnel.DisposeAsync();
        }
    }

    private static string NormalizeOpenOmsiMapPath(string? mapName)
    {
        var value = mapName?.Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.EndsWith("/global.cfg", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (value.StartsWith("maps/", StringComparison.OrdinalIgnoreCase))
        {
            return value.TrimEnd('/') + "/global.cfg";
        }

        return $"maps/{value.Trim('/')}/global.cfg";
    }

    private static string ResolveLanAdvertiseAddress()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(address =>
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                ?.ToString() ?? IPAddress.Loopback.ToString();
        }
        catch
        {
            return IPAddress.Loopback.ToString();
        }
    }
}
