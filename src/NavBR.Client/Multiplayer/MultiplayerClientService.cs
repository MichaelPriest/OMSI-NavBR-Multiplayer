using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

public sealed class MultiplayerClientService : IAsyncDisposable
{
    private HubConnection? _connection;
    private JoinRoomRequest? _joinRequest;

    public event Action<HubConnectionState>? ConnectionStateChanged;
    public event Action<RoomSnapshot>? RoomSnapshotReceived;
    public event Action<PlayerPresence>? PlayerJoined;
    public event Action<PlayerPresence>? PlayerPresenceChanged;
    public event Action<string>? PlayerLeft;
    public event Action<PlayerTelemetryFrame>? TelemetryReceived;
    public event Action<ChatMessage>? ChatMessageReceived;
    public event Action<VoiceFrame>? VoiceFrameReceived;

    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;

    public bool IsConnected => State == HubConnectionState.Connected;

    public async Task<RoomSnapshot> ConnectAsync(
        MultiplayerSettings settings,
        string? currentMapName,
        string? currentMapCompatibilityId = null,
        CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        var hubUrl = NormalizeHubUrl(settings.ServerUrl);
        _joinRequest = new JoinRoomRequest(
            settings.RoomId.Trim(),
            settings.PlayerId.Trim(),
            settings.DisplayName.Trim(),
            NormalizeOptional(currentMapName),
            NormalizeOptional(currentMapCompatibilityId));

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

            ConnectionStateChanged?.Invoke(connection.State);
            RoomSnapshotReceived?.Invoke(snapshot);
            return snapshot;
        }
        catch
        {
            await DisposeConnectionAsync(connection);
            if (ReferenceEquals(_connection, connection))
            {
                _connection = null;
            }

            _joinRequest = null;
            _ = OmsiPluginBridgeRelay.ClearRemotePlayersAsync();
            ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
            throw;
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

        await connection.SendAsync("PublishTelemetry", telemetry, cancellationToken);
    }

    public async Task SendChatMessageAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var connection = RequireConnectedConnection();
        await connection.SendAsync("SendChatMessage", text, cancellationToken);
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

        await connection.SendAsync("PublishVoiceFrame", sequence, opusPayload, cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        var connection = _connection;
        _connection = null;
        _joinRequest = null;
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
            _ = OmsiPluginBridgeRelay.RemoveRemotePlayerAsync(playerId);
        });
        connection.On<PlayerTelemetryFrame>("telemetry", frame =>
        {
            TelemetryReceived?.Invoke(frame);
            _ = OmsiPluginBridgeRelay.ForwardRemoteTelemetryAsync(frame);
        });
        connection.On<ChatMessage>("chatMessage", message => ChatMessageReceived?.Invoke(message));
        connection.On<VoiceFrame>("voiceFrame", frame => VoiceFrameReceived?.Invoke(frame));

        connection.Reconnecting += _ =>
        {
            _ = OmsiPluginBridgeRelay.ClearRemotePlayersAsync();
            ConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
            return Task.CompletedTask;
        };

        connection.Reconnected += async _ =>
        {
            if (_joinRequest is not null)
            {
                var snapshot = await connection.InvokeAsync<RoomSnapshot>("JoinRoom", _joinRequest);
                RoomSnapshotReceived?.Invoke(snapshot);
            }

            ConnectionStateChanged?.Invoke(HubConnectionState.Connected);
        };

        connection.Closed += _ =>
        {
            _ = OmsiPluginBridgeRelay.ClearRemotePlayersAsync();
            ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
            return Task.CompletedTask;
        };
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
