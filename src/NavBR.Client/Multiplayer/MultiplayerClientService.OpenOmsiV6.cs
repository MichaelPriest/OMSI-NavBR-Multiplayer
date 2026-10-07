using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR.Client;
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
    private OpenOmsiLanPeerSession? _openOmsiV6Session;
    private ushort _openOmsiLocalSequence;
    private RoleplayCharacterState? _openOmsiLocalRoleplayState;

    public bool UsesOpenOmsiV6Transport => _openOmsiV6Session?.IsRunning == true;

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
        session.RemoteLeft += HandleOpenOmsiRemoteLeft;
        _openOmsiV6Session = session;

        if (IsTrafficAuthority)
        {
            await session.StartHostAsync(world, cancellationToken);
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

        if (authority?.OpenOmsiTransport is not { IsValid: true } transport ||
            !IPAddress.TryParse(transport.Host, out var hostAddress))
        {
            // The sidecar may deliver the authority transport in the next
            // playerPresenceChanged event. Keep the NavBR services connected
            // and join v6 as soon as that advertisement arrives.
            await session.DisposeAsync();
            if (ReferenceEquals(_openOmsiV6Session, session))
            {
                _openOmsiV6Session = null;
            }
            return;
        }

        await JoinAdvertisedOpenOmsiTransportAsync(
            transport,
            hostAddress,
            world,
            cancellationToken);
    }

    private async Task JoinAdvertisedOpenOmsiTransportAsync(
        OpenOmsiTransportDescriptor transport,
        IPAddress hostAddress,
        OpenOmsiLanWorld world,
        CancellationToken cancellationToken)
    {
        var session = _openOmsiV6Session;
        if (session is null)
        {
            session = new OpenOmsiLanPeerSession();
            session.RemoteInfoReceived += HandleOpenOmsiRemoteInfo;
            session.RemoteStateReceived += HandleOpenOmsiRemoteState;
            session.RemoteLeft += HandleOpenOmsiRemoteLeft;
            _openOmsiV6Session = session;
        }

        if (session.IsRunning)
        {
            return;
        }

        await session.JoinAsync(
            new IPEndPoint(hostAddress, transport.Port),
            world,
            _joinRequest?.DisplayName ?? "Driver",
            _joinRequest?.Compatibility?.VehiclePath,
            cancellationToken);

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
            OpenOmsiLanProtocol.SessionHex(session.SessionId));

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
            presence.OpenOmsiTransport is not { IsValid: true } transport ||
            !IPAddress.TryParse(transport.Host, out var address))
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
                address,
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
            SourceTimestampUnixMilliseconds: timestamp.ToUnixTimeMilliseconds());

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

        var baseState = BuildRemoteState(
            session.LocalPlayerId,
            unchecked(++_openOmsiLocalSequence),
            new PlayerTelemetryFrame(localPresence, telemetry),
            (uint)Environment.TickCount64);

        var walker = BuildOpenOmsiLocalWalker(baseState, telemetry);
        var state = baseState with { Walker = walker };

        var info = BuildRemoteInfo(
            session.LocalPlayerId,
            new PlayerTelemetryFrame(localPresence, telemetry));
        await session.PublishInfoAsync(info, cancellationToken);
        await session.PublishStateAsync(state, cancellationToken);
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
            session.RemoteLeft += HandleOpenOmsiRemoteLeft;
            _openOmsiV6Session = session;

            var world = new OpenOmsiLanWorld(
                NormalizeOpenOmsiMapPath(_joinRequest.MapName),
                DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"),
                0d,
                string.Empty,
                string.Empty);
            await session.StartHostAsync(world);
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

    private async Task StopOpenOmsiV6Async()
    {
        var session = _openOmsiV6Session;
        _openOmsiV6Session = null;
        _openOmsiLocalRoleplayState = null;
        lock (_openOmsiV6Sync)
        {
            _openOmsiPresenceByLanId.Clear();
            _openOmsiPresenceByPlayerId.Clear();
            _openOmsiInfoByLanId.Clear();
        }

        if (session is not null)
        {
            await session.DisposeAsync();
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
