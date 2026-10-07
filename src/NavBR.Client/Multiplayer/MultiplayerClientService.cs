using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Client.PluginBridge;
using NavBR.Client.Maps;
using NavBR.Client.Network;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.OpenOmsi;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Network;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Multiplayer;

public sealed partial class MultiplayerClientService : IAsyncDisposable
{
    private const long RemoteSourceClockResetThresholdMs = 30_000;
    private readonly RemotePhysicalVehicleCoordinator _physicalVehicles;
    private readonly OmsiPhysicalRoadAnchorResolver _openOmsiWorldAnchorResolver;
    private readonly SemaphoreSlim _physicalVehicleStatusPublishGate = new(1, 1);
    private readonly object _remoteTelemetryOrderSync = new();
    private readonly Dictionary<string, long> _lastRemoteSourceTimestampByPlayer =
        new(StringComparer.OrdinalIgnoreCase);
    private long _physicalVehicleStatusRevision;
    private HubConnection? _connection;
    private JoinRoomRequest? _joinRequest;

    public MultiplayerClientService(
        Func<string?>? omsiInstallDirectorySource = null,
        Func<IReadOnlyList<string>>? openOmsiContentRootsSource = null)
    {
        var installDirectorySource =
            omsiInstallDirectorySource ?? static () => null;
        _physicalVehicles = new RemotePhysicalVehicleCoordinator(
            installDirectorySource);
        _openOmsiWorldAnchorResolver =
            new OmsiPhysicalRoadAnchorResolver(
                installDirectorySource);
        _physicalVehicles.PhysicalVehicleSetChanged += QueuePhysicalVehicleSetPublish;
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
    public event Action<string, string>? SessionCommandReceived;
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

        // The WebView controller can exist without ever rendering the retired
        // WPF window. Re-apply the persisted physical-bus preference at the
        // actual connection boundary so a partial/hidden UI initialization
        // can never leave settings=true while the runtime marker remains off.
        if (ExperimentalFeatureFlags.PhysicalVehiclesEnabled !=
            settings.ExperimentalPhysicalVehiclesEnabled)
        {
            ExperimentalFeatureFlags.SetPhysicalVehiclesEnabled(
                settings.ExperimentalPhysicalVehiclesEnabled);
        }

        string hubUrl;
        try
        {
            hubUrl = NormalizeHubUrl(settings.ServerUrl);
        }
        catch (Exception ex)
        {
            throw MultiplayerNetworkErrorClassifier.WrapConnection(ex);
        }

        var companyBadge = System.Windows.Application.Current is App app
            ? app.NetworkRuntime.CurrentBadge
            : null;
        CompanyBadgePresenceProof? companyBadgeProof = null;
        if (companyBadge is not null)
        {
            var identity = NavBRIdentityStore.LoadOrCreate(companyBadge.DisplayName);
            if (!string.Equals(
                    identity.PlayerId,
                    companyBadge.PlayerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                // A reset/recreated NavBR identity must not make multiplayer
                // unusable. Drop the stale company credential; the user can
                // rejoin the company to receive a badge for the new identity.
                companyBadge = null;
            }
            else
            {
                var badgeTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                companyBadgeProof = new CompanyBadgePresenceProof(
                    identity.PublicKeySpkiBase64,
                    badgeTimestamp,
                    NavBRIdentityStore.SignCompanyBadgePresence(
                        settings.PlayerId.Trim(),
                        companyBadge,
                        badgeTimestamp));
            }
        }

        _joinRequest = new JoinRoomRequest(
            settings.RoomId.Trim(),
            settings.PlayerId.Trim(),
            settings.DisplayName.Trim(),
            NormalizeOptional(currentMapName),
            NormalizeOptional(currentMapCompatibilityId),
            compatibility,
            settings.EphemeralRoomPassword,
            settings.EphemeralCreatePrivateRoom,
            companyBadge,
            companyBadgeProof);
        _physicalVehicles.SetLocalManifest(compatibility);
        _physicalVehicles.SetLocalTelemetry(null);

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(NavBrReconnectPolicy.Instance)
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
            await ConfigureOpenOmsiV6Async(snapshot, cancellationToken);
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
            ClearRemoteTelemetryOrder();
            _physicalVehicles.SetLocalManifest(null);
            _physicalVehicles.SetLocalTelemetry(null);
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

        // The in-process OMSI plugin is the authoritative fallback for the
        // physical RoadVehicle.Kachel identity. External memory telemetry can
        // legitimately miss the dynamic Kacheln index on some maps while the
        // plugin, running inside Omsi.exe, can still resolve the live tile.
        if (System.Windows.Application.Current is App app)
        {
            var pluginStatus = app.PluginBridge.GetConnectionInfo().LastStatus;
            if (pluginStatus?.TimestampUnixMilliseconds is long statusTimestamp &&
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - statusTimestamp <= 5_000)
            {
                outgoing = outgoing with
                {
                    MapTileIndex = outgoing.MapTileIndex ?? pluginStatus.MapTileIndex
                };

                if (pluginStatus.GridX is int physicalGridX &&
                    pluginStatus.GridY is int physicalGridY)
                {
                    outgoing = outgoing with
                    {
                        PhysicalGridX = outgoing.PhysicalGridX ?? physicalGridX,
                        PhysicalGridY = outgoing.PhysicalGridY ?? physicalGridY
                    };
                }
            }
        }

        // Keep physical rendering compatibility synchronized with the actual
        // live OMSI state. Players often connect before the final map/bus/HOF
        // identity is available, and may change vehicles without reconnecting.
        var liveManifest =
            OmsiCompatibilityManifestFactory.Create(
                outgoing,
                activeMap: null);
        _physicalVehicles.SetLocalManifest(liveManifest);
        _physicalVehicles.SetLocalTelemetry(outgoing);

        // Automatic SignalR reconnect reuses _joinRequest. Keep it aligned with
        // the live identity so a reconnect cannot temporarily revert the room
        // presence to the bus/HOF that existed when JoinRoom first ran.
        if (_joinRequest is { } joinRequest &&
            (!string.Equals(
                 joinRequest.MapName,
                 outgoing.MapName,
                 StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(
                 joinRequest.MapCompatibilityId,
                 compatibilityId,
                 StringComparison.OrdinalIgnoreCase) ||
             !CompatibilityManifestEquivalent(
                 joinRequest.Compatibility,
                 liveManifest)))
        {
            _joinRequest = joinRequest with
            {
                MapName =
                    outgoing.MapName ??
                    joinRequest.MapName,
                MapCompatibilityId =
                    compatibilityId ??
                    joinRequest.MapCompatibilityId,
                Compatibility = liveManifest
            };
        }

        _ = OmsiPluginBridgeRelay.ForwardLocalTelemetryAsync(
            outgoing,
            compatibilityId,
            cancellationToken);

        // Physical multiplayer now uses the openOMSI LAN v6 transport.
        // SignalR keeps this telemetry only for NavBR service features such as
        // CCO/company/map status; it no longer drives remote RoadVehicles.
        await PublishOpenOmsiTelemetryAsync(outgoing, cancellationToken);
        await connection.SendAsync("PublishTelemetry", outgoing, cancellationToken);
    }

    public Task PublishTrafficSnapshotAsync(
        TrafficSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        if (!IsTrafficAuthority)
        {
            return Task.CompletedTask;
        }

        // Physical/shared traffic is owned exclusively by openOMSI WORLD v6.
        // SignalR remains the NavBR service plane and must never carry a second
        // authoritative copy of AI movement.
        return PublishOpenOmsiTrafficSnapshotAsync(
            snapshot,
            cancellationToken);
    }

    public async Task SendChatMessageAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var openOmsi = _openOmsiV6Session;
        if (openOmsi?.IsRunning == true &&
            openOmsi.LocalPlayerId != 0)
        {
            await openOmsi.SendChatAsync(
                text,
                cancellationToken);
            return;
        }

        var connection = RequireConnectedConnection();
        await connection.SendAsync(
            "SendChatMessage",
            text,
            cancellationToken);
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

    private static bool CompatibilityManifestEquivalent(
        OmsiCompatibilityManifest? left,
        OmsiCompatibilityManifest? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return
            string.Equals(left.OmsiVersion, right.OmsiVersion, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.NavBRVersion, right.NavBRVersion, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.MapName, right.MapName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.MapCompatibilityId, right.MapCompatibilityId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.VehiclePath, right.VehiclePath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.VehicleCompatibilityId, right.VehicleCompatibilityId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.HofName, right.HofName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.HofCompatibilityId, right.HofCompatibilityId, StringComparison.OrdinalIgnoreCase) &&
            left.PluginProtocolVersion == right.PluginProtocolVersion &&
            string.Equals(left.PluginDeployment, right.PluginDeployment, StringComparison.OrdinalIgnoreCase) &&
            (left.Capabilities ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(
                    (right.Capabilities ?? Array.Empty<string>())
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);
    }

    private void QueuePhysicalVehicleSetPublish(
        IReadOnlyList<string> playerIds)
    {
        var snapshot = playerIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();
        var revision = Interlocked.Increment(
            ref _physicalVehicleStatusRevision);
        _ = PublishPhysicalVehicleSetAsync(snapshot, revision);
    }

    private async Task PublishPhysicalVehicleSetAsync(
        IReadOnlyList<string> playerIds,
        long revision,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _physicalVehicleStatusPublishGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            // Several physical vehicles can finish spawning almost at the same
            // time. Never let an older [A,B] snapshot arrive after the newer
            // [A,B,C] snapshot and overwrite server presence with stale state.
            if (revision != Volatile.Read(ref _physicalVehicleStatusRevision))
            {
                return;
            }

            var connection = _connection;
            if (connection is null ||
                connection.State != HubConnectionState.Connected)
            {
                return;
            }

            await connection.SendAsync(
                "UpdatePhysicalVehicleStatus",
                playerIds,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Diagnostic/status publishing must never break multiplayer.
        }
        finally
        {
            _physicalVehicleStatusPublishGate.Release();
        }
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
        await StopOpenOmsiV6Async();
        ClearRemoteTelemetryOrder();
        _physicalVehicles.SetLocalManifest(null);
        _physicalVehicles.SetLocalTelemetry(null);
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
        _physicalVehicleStatusPublishGate.Dispose();
    }

    private void RegisterHandlers(HubConnection connection)
    {
        connection.On<PlayerPresence>("playerJoined", player =>
        {
            PlayerJoined?.Invoke(player);
            _ = HandleOpenOmsiPresenceAsync(player);
        });
        connection.On<PlayerPresence>("playerPresenceChanged", player =>
        {
            PlayerPresenceChanged?.Invoke(player);
            _ = HandleOpenOmsiPresenceAsync(player);
        });
        connection.On<string>("playerLeft", playerId =>
        {
            ForgetRemoteTelemetryOrder(playerId);
            PlayerLeft?.Invoke(playerId);
            RemoveRoleplayCharacter(playerId);
            _ = _physicalVehicles.DespawnAsync(playerId);
            _ = OmsiPluginBridgeRelay.RemoveRemotePlayerAsync(playerId);
        });
        connection.On<PlayerTelemetryFrame>("telemetry", frame =>
        {
            if (!TryAcceptRemoteTelemetry(frame))
            {
                return;
            }

            // Service-side telemetry remains useful for CCO/company/status.
            // Never use it for physical movement: openOMSI LAN v6 is now the
            // single authoritative remote-motion path.
            TelemetryReceived?.Invoke(frame);
        });
        connection.On<string?>("trafficAuthorityChanged", authorityPlayerId =>
        {
            SetTrafficAuthority(authorityPlayerId);
            _ = HandleOpenOmsiAuthorityChangedAsync();
        });
        connection.On<string?>("roomOwnerChanged", ownerPlayerId =>
            SetRoomOwner(ownerPlayerId));
        connection.On<ChatMessage>("chatMessage", message => ChatMessageReceived?.Invoke(message));
        connection.On<VoiceFrame>("voiceFrame", frame => VoiceFrameReceived?.Invoke(frame));

        connection.Reconnecting += error =>
        {
            // Keep the last confirmed remote entities during a transient
            // transport loss. Their native lifecycle has its own bounded stale
            // timeout, so a brief Wi-Fi/relay interruption no longer becomes an
            // immediate despawn/respawn and visible teleport.
            ConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
            return Task.CompletedTask;
        };

        connection.Reconnected += async connectionId =>
        {
            if (_joinRequest is not null)
            {
                var snapshot = await connection.InvokeAsync<RoomSnapshot>("JoinRoom", _joinRequest);
                ApplyRoomSnapshotMetadata(snapshot);
                lock (_openOmsiV6Sync)
                {
                    foreach (var player in snapshot.Players)
                    {
                        RememberOpenOmsiPresenceCore(player);
                    }
                }
                await RepublishOpenOmsiV6PresenceAsync();
                RoomSnapshotReceived?.Invoke(snapshot);
            }

            ConnectionStateChanged?.Invoke(HubConnectionState.Connected);
        };

        connection.Closed += error =>
        {
            ResetRoomMetadata();
            ClearRemoteTelemetryOrder();
            ClearRoleplayCharacters();
            _ = _physicalVehicles.ClearAsync();
            _ = OpenOmsiLanGateway.Shared.ClearRemotesAsync();
            _ = OmsiPluginBridgeRelay.ClearRemotePlayersAsync();
            ConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
            return Task.CompletedTask;
        };
    }

    private bool TryAcceptRemoteTelemetry(PlayerTelemetryFrame frame)
    {
        var playerId = frame.Player.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return false;
        }

        var sourceTimestampMs =
            frame.Telemetry.SourceTimestampUnixMilliseconds ??
            frame.Telemetry.Timestamp.ToUnixTimeMilliseconds();

        lock (_remoteTelemetryOrderSync)
        {
            if (!_lastRemoteSourceTimestampByPlayer.TryGetValue(
                    playerId,
                    out var previousSourceTimestampMs))
            {
                _lastRemoteSourceTimestampByPlayer[playerId] =
                    sourceTimestampMs;
                return true;
            }

            if (sourceTimestampMs > previousSourceTimestampMs)
            {
                _lastRemoteSourceTimestampByPlayer[playerId] =
                    sourceTimestampMs;
                return true;
            }

            // A substantial rollback means the sender restarted or corrected
            // its clock. Match the openOMSI strategy: reset the ordering anchor
            // instead of permanently rejecting a legitimate new session.
            if (previousSourceTimestampMs - sourceTimestampMs >
                RemoteSourceClockResetThresholdMs)
            {
                _lastRemoteSourceTimestampByPlayer[playerId] =
                    sourceTimestampMs;
                return true;
            }

            // Ignore duplicates and ordinary out-of-order arrivals before they
            // can reach the physical RoadVehicle or plugin bridge.
            return false;
        }
    }

    private void ForgetRemoteTelemetryOrder(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        lock (_remoteTelemetryOrderSync)
        {
            _lastRemoteSourceTimestampByPlayer.Remove(playerId);
        }

    }

    private void ClearRemoteTelemetryOrder()
    {
        lock (_remoteTelemetryOrderSync)
        {
            _lastRemoteSourceTimestampByPlayer.Clear();
        }

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

    private sealed class NavBrReconnectPolicy : IRetryPolicy
    {
        private static readonly TimeSpan MaximumReconnectWindow =
            TimeSpan.FromSeconds(60);

        public static NavBrReconnectPolicy Instance { get; } = new();

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            var remaining =
                MaximumReconnectWindow -
                retryContext.ElapsedTime;
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            var desired = retryContext.PreviousRetryCount switch
            {
                0 => TimeSpan.Zero,
                1 => TimeSpan.FromSeconds(2),
                2 => TimeSpan.FromSeconds(5),
                _ => TimeSpan.FromSeconds(10)
            };

            return desired <= remaining
                ? desired
                : remaining;
        }
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
