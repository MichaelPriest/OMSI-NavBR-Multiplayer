using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

public sealed partial class MultiplayerClientService : IAsyncDisposable
{
    private readonly RemotePhysicalVehicleCoordinator _physicalVehicles;
    private HubConnection? _connection;
    private JoinRoomRequest? _joinRequest;

    public MultiplayerClientService(
        Func<string?>? omsiInstallDirectorySource = null)
    {
        _physicalVehicles = new RemotePhysicalVehicleCoordinator(
            omsiInstallDirectorySource);
    }

    public event Action<HubConnectionState>? ConnectionStateChanged;
    public event Action<RoomSnapshot>? RoomSnapshotReceived;
    public event Action<PlayerPresence>? PlayerJoined;
    public event Action<PlayerPresence>? PlayerPresenceChanged;
    public event Action<string>? PlayerLeft;
    public event Action<PlayerTelemetryFrame>? TelemetryReceived;
    public event Action<TrafficSnapshot>? TrafficSnapshotReceived;
    public event Action<string?>? TrafficAuthorityChanged;
    public event Action<string?>? RoomOwnerChanged;
    public event Action<ChatMessage>? ChatMessageReceived;
    public event Action<VoiceFrame>? VoiceFrameReceived;

    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;

    public bool IsConnected => State == HubConnectionState.Connected;
    public string? TrafficAuthorityPlayerId { get; private set; }
    public bool CurrentRoomIsPrivate { get; private set; }
    public string? RoomOwnerPlayerId { get; private set; }
    public bool IsRoomOwner =>
        _joinRequest is not null &&
        !string.IsNullOrWhiteSpace(RoomOwnerPlayerId) &&
        string.Equals(_joinRequest.PlayerId, RoomOwnerPlayerId, StringComparison.OrdinalIgnoreCase);
    public bool IsTrafficAuthority =>
        _joinRequest is not null &&
        !string.IsNullOrWhiteSpace(TrafficAuthorityPlayerId) &&
        string.Equals(
            _joinRequest.PlayerId,
            TrafficAuthorityPlayerId,
            StringComparison.OrdinalIgnoreCase);

    public Task<RoomSnapshot> ConnectAsync(
        MultiplayerSettings settings,
        string? currentMapName,
        string? currentMapCompatibilityId = null,
        CancellationToken cancellationToken = default)
    {
        var compatibility = OmsiCompatibilityManifestFactory.Create(
            currentMapName,
            currentMapCompatibilityId);
        return ConnectAsync(
            settings,
            currentMapName,
            currentMapCompatibilityId,
            compatibility,
            cancellationToken);
    }

    public async Task<RoomSnapshot> ConnectAsync(
        MultiplayerSettings settings,
        string? currentMapName,
        string? currentMapCompatibilityId,
        OmsiCompatibilityManifest? compatibility,
        CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        string hubUrl;
        try
        {
            hubUrl = NormalizeHubUrl(settings.ServerUrl);
        }
        catch (Exception ex)
        {
            throw MultiplayerNetworkErrorClassifier.WrapConnection(ex);
        }

        _joinRequest = new JoinRoomRequest(
            settings.RoomId.Trim(),
            settings.PlayerId.Trim(),
            settings.DisplayName.Trim(),
            NormalizeOptional(currentMapName),
            NormalizeOptional(currentMapCompatibilityId),
            compatibility,
            settings.EphemeralRoomPassword,
            settings.EphemeralCreatePrivateRoom);
        _physicalVehicles.SetLocalManifest(compatibility);

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(
            [
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            ])
            .Build();

        RegisterHandlers(connection);
        _connection = connection;

        try
        {
            ConnectionStateChanged?.Invoke(HubConnectionState.Connecting);
            await connection.StartAsync(cancellationToken);

            var snapshot = await connection.InvokeAsync<RoomSnapshot>(
                "JoinRoom",
                _joinRequest,
                cancellationToken);

            ApplyRoomSnapshotMetadata(snapshot);
            ConnectionStateChanged?.Invoke(connection.State);
            RoomSnapshotReceived?.Invoke(snapshot);
            return snapshot;
        }
        catch (Exception ex)
        {
            await DisposeConnectionAsync(connection);
            if (ReferenceEquals(_connection, connection))
            {
                _connection = null;
            }

            _joinRequest = null;
            ResetRoomMetadata();
            _physicalVehicles.SetLocalManifest(null);
            _ = _physicalVehicles.ClearAsync();
            _ = OmsiPluginBridgeRelay.ClearRemotePlayersAsync();
            ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);

            if (ex is HubException)
            {
                throw new InvalidOperationException(
                    RoomPrivacyText.DescribeServerError(ex.Message),
                    ex);
            }

            throw MultiplayerNetworkErrorClassifier.WrapConnection(ex);
        }
    }

    public async Task PublishTelemetryAsync(
        VehicleTelemetry telemetry,
        CancellationToken cancellationToken = default)
    {
        var connection = _connection;
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            return;
        }

        var compatibilityId = OmsiPluginBridgeRelay.ResolveCurrentMapCompatibilityId(
            telemetry.MapCompatibilityId ?? _joinRequest?.MapCompatibilityId);
        var outgoing = telemetry with
        {
            MapCompatibilityId = compatibilityId
        };

        // Keep physical rendering compatibility synchronized with the actual
        // live OMSI state. Players often connect before the final map/bus/HOF
        // identity is available, and may change vehicles without reconnecting.
        _physicalVehicles.SetLocalManifest(
            OmsiCompatibilityManifestFactory.Create(outgoing, activeMap: null));

        _ = OmsiPluginBridgeRelay.ForwardLocalTelemetryAsync(
            outgoing,
            compatibilityId,
            cancellationToken);

        await connection.SendAsync("PublishTelemetry", outgoing, cancellationToken);
    }

    public async Task PublishTrafficSnapshotAsync(
        TrafficSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var connection = _connection;
        if (!IsTrafficAuthority ||
            connection is null || connection.State != HubConnectionState.Connected)
        {
            return;
        }

        await connection.SendAsync("PublishTrafficSnapshot", snapshot, cancellationToken);
    }

    public async Task SendChatMessageAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var connection = RequireConnectedConnection();
        await connection.SendAsync("SendChatMessage", text, cancellationToken);
    }

    public async Task<TimeSpan?> MeasureAndPublishLatencyAsync(
        bool voiceEnabled,
        CancellationToken cancellationToken = default)
    {
        var connection = _connection;
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            return null;
        }

        var started = Stopwatch.GetTimestamp();
        await connection.InvokeAsync("NavBrPing", cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var latencyMs = Math.Clamp(
            (int)Math.Round(elapsed.TotalMilliseconds),
            0,
            5000);

        await PublishClientStatusAsync(
            voiceEnabled,
            latencyMs,
            cancellationToken);
        return elapsed;
    }

    public async Task PublishClientStatusAsync(
        bool voiceEnabled,
        int? latencyMs,
        CancellationToken cancellationToken = default)
    {
        var connection = _connection;
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            return;
        }

        await connection.SendAsync(
            "UpdateClientStatus",
            voiceEnabled,
            latencyMs,
            cancellationToken);
    }

    public async Task PublishVoiceFrameAsync(
        long sequence,
        byte[] opusPayload,
        CancellationToken cancellationToken = default)
    {
        var connection = _connection;
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            return;
        }

        await connection.SendAsync(
            "PublishVoiceFrame",
            sequence,
            opusPayload,
            VoiceChannelSession.CurrentChannel,
            cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        var connection = _connection;
        _connection = null;
        _joinRequest = null;
        ResetRoomMetadata();
        _physicalVehicles.SetLocalManifest(null);
        ClearRoleplayCharacters();
        _ = _physicalVehicles.ClearAsync();
        _ = OmsiPluginBridgeRelay.ClearRemotePlayersAsync();

        if (connection is null)
        {
            ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
            return;
        }

        if (connection.State == HubConnectionState.Connected)
        {
            try
            {
                await connection.InvokeAsync("LeaveRoom");
            }
            catch
            {
                // The connection may already be closing. Stop/Dispose still follows.
            }
        }

        await DisposeConnectionAsync(connection);
        ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private void RegisterHandlers(HubConnection connection)
    {
        connection.On<PlayerPresence>("playerJoined", player => PlayerJoined?.Invoke(player));
        connection.On<PlayerPresence>("playerPresenceChanged", player => PlayerPresenceChanged?.Invoke(player));
        connection.On<string>("playerLeft", playerId =>
        {
            PlayerLeft?.Invoke(playerId);
            RemoveRoleplayCharacter(playerId);
            _ = _physicalVehicles.DespawnAsync(playerId);
            _ = OmsiPluginBridgeRelay.RemoveRemotePlayerAsync(playerId);
        });
        connection.On<PlayerTelemetryFrame>("telemetry", frame =>
        {
            TelemetryReceived?.Invoke(frame);
            _ = OmsiPluginBridgeRelay.ForwardRemoteTelemetryAsync(frame);
            _ = _physicalVehicles.ApplyAsync(frame);
        });
        connection.On<TrafficSnapshot>("trafficSnapshot", snapshot =>
        {
            if (!string.IsNullOrWhiteSpace(TrafficAuthorityPlayerId) &&
                !string.Equals(
                    snapshot.AuthorityPlayerId,
                    TrafficAuthorityPlayerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TrafficSnapshotReceived?.Invoke(snapshot);
        });
        connection.On<string?>("trafficAuthorityChanged", authorityPlayerId =>
            SetTrafficAuthority(authorityPlayerId));
        connection.On<string?>("roomOwnerChanged", ownerPlayerId =>
            SetRoomOwner(ownerPlayerId));
        connection.On<ChatMessage>("chatMessage", message => ChatMessageReceived?.Invoke(message));
        connection.On<VoiceFrame>("voiceFrame", frame => VoiceFrameReceived?.Invoke(frame));
        connection.On<RoleplayCharacterFrame>("roleplayCharacter", ApplyRoleplayCharacter);
        connection.On<string>("roleplayCharacterRemoved", RemoveRoleplayCharacter);

        connection.Reconnecting += error =>
        {
            ClearRoleplayCharacters();
            _ = _physicalVehicles.ClearAsync();
            _ = OmsiPluginBridgeRelay.ClearRemotePlayersAsync();
            ConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
            return Task.CompletedTask;
        };

        connection.Reconnected += async connectionId =>
        {
            if (_joinRequest is not null)
            {
                var snapshot = await connection.InvokeAsync<RoomSnapshot>("JoinRoom", _joinRequest);
                ApplyRoomSnapshotMetadata(snapshot);
                RoomSnapshotReceived?.Invoke(snapshot);
            }

            ConnectionStateChanged?.Invoke(HubConnectionState.Connected);
        };

        connection.Closed += error =>
        {
            ResetRoomMetadata();
            ClearRoleplayCharacters();
            _ = _physicalVehicles.ClearAsync();
            _ = OmsiPluginBridgeRelay.ClearRemotePlayersAsync();
            ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
            return Task.CompletedTask;
        };
    }

    private void ApplyRoomSnapshotMetadata(RoomSnapshot snapshot)
    {
        SetTrafficAuthority(snapshot.TrafficAuthorityPlayerId);
        CurrentRoomIsPrivate = snapshot.IsPrivate;
        SetRoomOwner(snapshot.OwnerPlayerId);
    }

    private void ResetRoomMetadata()
    {
        SetTrafficAuthority(null);
        CurrentRoomIsPrivate = false;
        SetRoomOwner(null);
    }

    private void SetTrafficAuthority(string? playerId)
    {
        var normalized = NormalizeOptional(playerId);
        if (string.Equals(
                TrafficAuthorityPlayerId,
                normalized,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TrafficAuthorityPlayerId = normalized;
        TrafficAuthorityChanged?.Invoke(normalized);
    }

    private void SetRoomOwner(string? playerId)
    {
        var normalized = NormalizeOptional(playerId);
        if (string.Equals(
                RoomOwnerPlayerId,
                normalized,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RoomOwnerPlayerId = normalized;
        RoomOwnerChanged?.Invoke(normalized);
    }

    private HubConnection RequireConnectedConnection()
    {
        var connection = _connection;
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Multiplayer is not connected.");
        }

        return connection;
    }

    private static async Task DisposeConnectionAsync(HubConnection connection)
    {
        try
        {
            await connection.StopAsync();
        }
        catch
        {
            // Best-effort shutdown.
        }

        await connection.DisposeAsync();
    }

    private static string NormalizeHubUrl(string? serverUrl)
    {
        var value = (serverUrl ?? string.Empty).Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Invalid multiplayer server URL.", nameof(serverUrl));
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/hubs/multiplayer", StringComparison.OrdinalIgnoreCase))
        {
            return uri.ToString().TrimEnd('/');
        }

        var builder = new UriBuilder(uri)
        {
            Path = $"{path}/hubs/multiplayer"
        };
        return builder.Uri.ToString().TrimEnd('/');
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
