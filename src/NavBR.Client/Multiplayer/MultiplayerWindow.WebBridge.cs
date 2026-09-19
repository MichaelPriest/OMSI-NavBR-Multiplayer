using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    internal object BuildWebBridgeState()
    {
        var now = DateTimeOffset.UtcNow;
        var localTelemetry = _telemetrySource();
        var activeMap = _activeMapSource();
        var players = _players.Values
            .OrderBy(player => player.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(player =>
            {
                var isLocal = string.Equals(
                    player.PlayerId,
                    _settings.PlayerId,
                    StringComparison.OrdinalIgnoreCase);
                var telemetry = isLocal
                    ? localTelemetry
                    : _remoteTelemetry.TryGetValue(player.PlayerId, out var remoteTelemetry)
                        ? remoteTelemetry
                        : null;
                var roleplay = _remoteRoleplayCharacters.TryGetValue(player.PlayerId, out var frame) &&
                               frame.Character.IsActive &&
                               now - frame.Character.Timestamp <= TimeSpan.FromSeconds(3d);
                var speaking = isLocal
                    ? _voiceChat.IsPushToTalkActive
                    : _voiceActivity.TryGetValue(player.PlayerId, out var voiceAt) &&
                      now - voiceAt <= TimeSpan.FromSeconds(1.5d);
                var receivedAt = isLocal
                    ? telemetry is null ? null as DateTimeOffset? : now
                    : _playerCardTelemetryReceivedAt.TryGetValue(player.PlayerId, out var seenAt)
                        ? seenAt
                        : null;
                var telemetryAgeSeconds = receivedAt.HasValue
                    ? Math.Max(0d, (now - receivedAt.Value).TotalSeconds)
                    : null as double?;
                var telemetryStale = !isLocal &&
                                     telemetry is not null &&
                                     telemetryAgeSeconds is > 3d;
                var distanceText = GetDistanceText(
                    localTelemetry,
                    telemetry,
                    localTelemetry?.MapName,
                    player.MapName,
                    activeMap?.CompatibilityId,
                    player.MapCompatibilityId);

                var physicalVehicleStatus = isLocal
                    ? null
                    : _client.GetRemotePhysicalVehicleStatus(player.PlayerId);

                return new
                {
                    playerId = player.PlayerId,
                    displayName = player.DisplayName,
                    roomId = player.RoomId,
                    mapName = player.MapName,
                    voiceEnabled = player.VoiceEnabled,
                    latencyMs = player.LatencyMs,
                    roleplayActive = roleplay,
                    physicalVehicleSpawned =
                        !isLocal &&
                        _client.IsRemotePhysicalVehicleSpawned(player.PlayerId),
                    physicalVehicleState = physicalVehicleStatus?.State,
                    physicalVehicleErrorCode = physicalVehicleStatus?.ErrorCode,
                    physicalVehiclePartCount = physicalVehicleStatus?.PartCount,
                    physicalVehicleExpectedPartCount = physicalVehicleStatus?.ExpectedPartCount,
                    physicalVehicleUpdatedAtUtc = physicalVehicleStatus?.UpdatedAtUtc,
                    speaking,
                    isLocal,
                    line = telemetry?.Line,
                    route = telemetry?.Route,
                    destinationName = telemetry?.DestinationName,
                    nextStopName = telemetry?.NextStopName,
                    vehicleName = telemetry?.VehicleName,
                    speedKph = telemetry?.SpeedKph,
                    delaySeconds = telemetry?.DelaySeconds,
                    telemetryAgeSeconds,
                    telemetryStale,
                    distanceText
                };
            })
            .ToArray();

        var sessionPoints = BuildWebSessionPoints(now);
        var inputDevices = VoiceAudioDeviceCatalog.GetInputDevices()
            .Select(device => new
            {
                deviceNumber = device.DeviceNumber,
                displayName = device.DisplayName
            })
            .ToArray();
        var outputDevices = VoiceAudioDeviceCatalog.GetOutputDevices("Padrão do Windows")
            .Select(device => new
            {
                deviceNumber = device.DeviceNumber,
                displayName = device.DisplayName
            })
            .ToArray();
        var voiceMixers = players
            .Where(player => !string.Equals(
                player.playerId,
                _settings.PlayerId,
                StringComparison.OrdinalIgnoreCase))
            .Select(player => new
            {
                playerId = player.playerId,
                displayName = player.displayName,
                muted = _voiceChat.IsRemoteMuted(player.playerId),
                gain = _voiceChat.GetRemoteGain(player.playerId),
                speaking = player.speaking
            })
            .ToArray();

        var chat = _chatMessages
            .TakeLast(80)
            .Select(message => new
            {
                playerId = message.PlayerId,
                displayName = message.DisplayName,
                text = message.Text,
                timestampUtc = message.TimestampUtc,
                isSystem = message.IsSystem
            })
            .ToArray();
        var networkQuality = SessionNetworkQualityFeed.Snapshot();
        var voiceQuality = _voiceChat.GetQualitySnapshot();
        var sessionOperationalState = _client.CurrentSessionOperationalState;
        var roomCompatibility = BuildWebRoomCompatibility();
        var internetInviteAddress = _host.GetInternetInviteAddress();
        var upnpResult = _host.LastUpnpResult;
        var hostReachability = !_host.IsRunning
            ? "inactive"
            : !string.IsNullOrWhiteSpace(internetInviteAddress)
                ? "internet-address-available"
                : upnpResult?.Success == true
                    ? "upnp-mapped-unverified"
                    : _settings.EnableAutomaticUpnp && upnpResult is null
                        ? "checking"
                        : "lan-only";
        var transportMode = !_client.IsConnected
            ? "none"
            : _host.IsRunning
                ? "direct-host"
                : _settings.EnableApplicationRelay &&
                  !IsLoopbackServerUrl(ServerTextBox.Text.Trim())
                    ? "relay"
                    : "remote-host";

        return new
        {
            available = true,
            connected = _client.IsConnected,
            connectionState = _client.State.ToString(),
            serverUrl = _settings.ServerUrl,
            roomId = _settings.RoomId,
            displayName = _settings.DisplayName,
            hostRunning = _host.IsRunning,
            hostPort = _host.IsRunning ? _host.Port : null as int?,
            hostReachability,
            internetInviteAddress,
            upnpMapped = upnpResult?.Success == true,
            upnpMessage = upnpResult?.Message,
            externalProbeConfigured = ExternalPortProbeClient.IsConfiguredForCurrentEnvironment(),
            roomIsPrivate = _client.CurrentRoomIsPrivate,
            inviteAddresses = _host.IsRunning ? _host.GetLanJoinUrls() : Array.Empty<string>(),
            latencyMs = _lastLatencyMs,
            voiceEnabled = VoiceEnabledCheckBox.IsChecked == true,
            voiceChannel = _settings.VoiceChannel,
            voiceProximityMeters = _settings.VoiceProximityMeters,
            voiceDeafened = _settings.VoiceDeafened,
            voiceInputDeviceNumber = _settings.VoiceInputDeviceNumber,
            voiceOutputDeviceNumber = _settings.VoiceOutputDeviceNumber,
            voiceInputDevices = inputDevices,
            voiceOutputDevices = outputDevices,
            voiceMixers,
            voicePushToTalkActive = _voiceChat.IsPushToTalkActive,
            voiceQuality = new
            {
                activeStreams = voiceQuality.ActiveStreams,
                receivedPackets = voiceQuality.ReceivedPackets,
                playedPackets = voiceQuality.PlayedPackets,
                fecRecoveredPackets = voiceQuality.FecRecoveredPackets,
                estimatedLostPackets = voiceQuality.EstimatedLostPackets,
                latePackets = voiceQuality.LatePackets,
                duplicatePackets = voiceQuality.DuplicatePackets,
                averageJitterMilliseconds = voiceQuality.AverageJitterMilliseconds,
                targetBufferMilliseconds = voiceQuality.TargetBufferMilliseconds,
                estimatedLossPercent = voiceQuality.EstimatedLossPercent
            },
            chatHotkey = _settings.ChatHotkey,
            voiceHotkey = _settings.VoiceHotkey,
            hotkeyOptions = NavBR.Client.Overlay.NavBRHotkeyCatalog.Options
                .Select(option => option.Name)
                .ToArray(),
            relayEnabled = _settings.EnableApplicationRelay,
            relayServerUrl = _settings.RelayServerUrl,
            physicalVehiclesEnabled = _settings.ExperimentalPhysicalVehiclesEnabled,
            physicalVehiclesAvailable = _client.IsPhysicalMultiplayerAvailable,
            networkQuality = new
            {
                level = networkQuality.Level.ToString(),
                roundTripMs = networkQuality.RoundTripMs,
                jitterMs = networkQuality.JitterMs,
                lossPercent = networkQuality.LossPercent,
                samples = networkQuality.Samples,
                updatedAtUtc = networkQuality.UpdatedAtUtc
            },
            sessionAuthority = new
            {
                roomOwnerPlayerId = _client.RoomOwnerPlayerId,
                roomOwnerDisplayName = ResolveAuthorityDisplayName(_client.RoomOwnerPlayerId),
                trafficAuthorityPlayerId = _client.TrafficAuthorityPlayerId,
                trafficAuthorityDisplayName = ResolveAuthorityDisplayName(_client.TrafficAuthorityPlayerId),
                isRoomOwner = _client.IsRoomOwner,
                isTrafficAuthority = _client.IsTrafficAuthority
            },
            transportMode,
            roomCompatibility,
            sessionOperationalState = sessionOperationalState is null
                ? null
                : new
                {
                    authorityPlayerId = sessionOperationalState.AuthorityPlayerId,
                    sequence = sessionOperationalState.Sequence,
                    serverTimestampUtc = sessionOperationalState.ServerTimestampUtc,
                    mapName = sessionOperationalState.MapName,
                    mapCompatibilityId = sessionOperationalState.MapCompatibilityId,
                    line = sessionOperationalState.Line,
                    route = sessionOperationalState.Route,
                    destinationName = sessionOperationalState.DestinationName,
                    nextStopName = sessionOperationalState.NextStopName
                },
            roleplayEnabled = _settings.ExperimentalRoleplayCharacterEnabled,
            localRoleplayActive = _localRoleplayCharacter?.IsActive == true,
            selectedRoleplayCharacter = SelectedRoleplayCharacter?.DisplayName,
            playerCount = players.Length,
            players,
            sessionPoints,
            chat
        };
    }

    private IReadOnlyList<WebSessionPoint> BuildWebSessionPoints(DateTimeOffset now)
    {
        var points = new List<WebSessionPoint>();
        var local = _telemetrySource();
        var activeMap = _activeMapSource();

        if (_localRoleplayCharacter is { IsActive: true } localRoleplay &&
            now - localRoleplay.Timestamp <= TimeSpan.FromSeconds(3d) &&
            IsRoleplaySessionCompatible(local, activeMap?.CompatibilityId, localRoleplay))
        {
            points.Add(new WebSessionPoint(
                _settings.PlayerId,
                _settings.DisplayName,
                "roleplay",
                localRoleplay.LocalX,
                localRoleplay.LocalY,
                localRoleplay.HeadingDegrees,
                localRoleplay.SpeedMps * 3.6d,
                null,
                true,
                localRoleplay.Activity.ToString()));
        }
        else if (local is not null &&
                 local.IsInGame &&
                 TryGetSessionCoordinates(local, out var localX, out var localY))
        {
            points.Add(new WebSessionPoint(
                _settings.PlayerId,
                _settings.DisplayName,
                "bus",
                localX,
                localY,
                local.HeadingDegrees,
                local.SpeedKph,
                local.Line,
                true,
                null));
        }

        foreach (var item in _remoteTelemetry)
        {
            if (_remoteRoleplayCharacters.TryGetValue(item.Key, out var roleplayFrame) &&
                roleplayFrame.Character.IsActive &&
                now - roleplayFrame.Character.Timestamp <= TimeSpan.FromSeconds(3d) &&
                IsRoleplaySessionCompatible(local, activeMap?.CompatibilityId, roleplayFrame.Character))
            {
                var displayName = _players.TryGetValue(item.Key, out var roleplayPlayer)
                    ? roleplayPlayer.DisplayName
                    : roleplayFrame.Player.DisplayName;
                points.Add(new WebSessionPoint(
                    item.Key,
                    displayName,
                    "roleplay",
                    roleplayFrame.Character.LocalX,
                    roleplayFrame.Character.LocalY,
                    roleplayFrame.Character.HeadingDegrees,
                    roleplayFrame.Character.SpeedMps * 3.6d,
                    null,
                    false,
                    roleplayFrame.Character.Activity.ToString()));
                continue;
            }

            var telemetry = item.Value;
            if (!telemetry.IsInGame ||
                now - telemetry.Timestamp > TimeSpan.FromSeconds(3d) ||
                !IsSessionTelemetryCompatible(local, activeMap?.CompatibilityId, item.Key, telemetry) ||
                !TryGetSessionCoordinates(telemetry, out var x, out var y))
            {
                continue;
            }

            var remoteName = _players.TryGetValue(item.Key, out var player)
                ? player.DisplayName
                : item.Key;
            points.Add(new WebSessionPoint(
                item.Key,
                remoteName,
                "bus",
                x,
                y,
                telemetry.HeadingDegrees,
                telemetry.SpeedKph,
                telemetry.Line,
                false,
                null));
        }

        foreach (var item in _remoteRoleplayCharacters)
        {
            if (_remoteTelemetry.ContainsKey(item.Key))
            {
                continue;
            }

            var frame = item.Value;
            if (!frame.Character.IsActive ||
                now - frame.Character.Timestamp > TimeSpan.FromSeconds(3d) ||
                !IsRoleplaySessionCompatible(local, activeMap?.CompatibilityId, frame.Character))
            {
                continue;
            }

            var displayName = _players.TryGetValue(item.Key, out var player)
                ? player.DisplayName
                : frame.Player.DisplayName;
            points.Add(new WebSessionPoint(
                item.Key,
                displayName,
                "roleplay",
                frame.Character.LocalX,
                frame.Character.LocalY,
                frame.Character.HeadingDegrees,
                frame.Character.SpeedMps * 3.6d,
                null,
                false,
                frame.Character.Activity.ToString()));
        }

        return points;
    }

    internal void SetVoiceEnabledFromWeb(bool enabled)
    {
        VoiceEnabledCheckBox.IsChecked = enabled;
    }

    internal void ConfigureVoiceFromWeb(
        string? channel,
        double? proximityMeters,
        bool deafened)
    {
        var normalizedChannel = VoiceChannelSession.NormalizeChannel(channel);
        var radius = Math.Clamp(
            proximityMeters is double value && double.IsFinite(value)
                ? value
                : _settings.VoiceProximityMeters,
            20d,
            1000d);

        _settings = _settings with
        {
            VoiceChannel = normalizedChannel,
            VoiceProximityMeters = radius,
            VoiceDeafened = deafened
        };
        MultiplayerSettingsStore.Save(_settings);
        VoiceChannelSession.Configure(
            _settings.VoiceChannel,
            _settings.VoiceProximityMeters,
            ShouldReceiveProximityVoice);
        _voiceChat.SetDeafened(_settings.VoiceDeafened);
        RenderVoiceChannelButton();
    }

    internal void ConfigureVoiceDevicesFromWeb(
        int? inputDeviceNumber,
        int? outputDeviceNumber)
    {
        var input = VoiceAudioDeviceCatalog.NormalizeInputDevice(
            inputDeviceNumber ?? _settings.VoiceInputDeviceNumber);
        var output = VoiceAudioDeviceCatalog.NormalizeOutputDevice(
            outputDeviceNumber ?? _settings.VoiceOutputDeviceNumber);

        _settings = _settings with
        {
            VoiceInputDeviceNumber = input,
            VoiceOutputDeviceNumber = output
        };
        MultiplayerSettingsStore.Save(_settings);
        _voiceChat.ConfigureDevices(input, output);
        RenderVoiceChannelButton();
    }

    internal void ConfigureRemoteVoiceFromWeb(
        string? playerId,
        bool muted,
        double? gain)
    {
        if (string.IsNullOrWhiteSpace(playerId) ||
            string.Equals(playerId, _settings.PlayerId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _voiceChat.SetRemoteMuted(playerId, muted);
        _voiceChat.SetRemoteGain(
            playerId,
            Math.Clamp(
                gain is double value && double.IsFinite(value) ? value : 1d,
                0d,
                2d));
    }

    internal bool IsHostRunningForWeb => _host.IsRunning;

    internal void ConfigureHotkeysFromWeb(string? chatHotkey, string? voiceHotkey)
    {
        var chat = NavBR.Client.Overlay.NavBRHotkeyCatalog.Resolve(
            chatHotkey,
            NavBR.Client.Overlay.NavBRHotkeyCatalog.DefaultChatHotkey).Name;
        var voice = NavBR.Client.Overlay.NavBRHotkeyCatalog.Resolve(
            voiceHotkey,
            NavBR.Client.Overlay.NavBRHotkeyCatalog.DefaultVoiceHotkey).Name;

        if (string.Equals(chat, voice, StringComparison.OrdinalIgnoreCase))
        {
            voice = PickDistinctHotkey(
                chat,
                NavBR.Client.Overlay.NavBRHotkeyCatalog.DefaultVoiceHotkey,
                NavBR.Client.Overlay.NavBRHotkeyCatalog.DefaultChatHotkey);
        }

        _settings = _settings with
        {
            ChatHotkey = chat,
            VoiceHotkey = voice
        };
        MultiplayerSettingsStore.Save(_settings);

        if (_hotkeyUiReady)
        {
            _hotkeyUiReady = false;
            ChatHotkeyComboBox.SelectedItem = chat;
            VoiceHotkeyComboBox.SelectedItem = voice;
            _hotkeyUiReady = true;
            RefreshHotkeyUiText();
        }
    }

    internal void ConfigureRelayFromWeb(bool enabled, string? relayServerUrl)
    {
        if (_host.IsRunning)
        {
            throw new InvalidOperationException(
                "Pare a hospedagem local antes de alterar o modo relay.");
        }

        var relayUrl = (relayServerUrl ?? _settings.RelayServerUrl ?? string.Empty).Trim();
        if (enabled && !string.IsNullOrWhiteSpace(relayUrl) &&
            !TryNormalizeRelayUrl(relayUrl, out relayUrl))
        {
            throw new InvalidOperationException(
                "Informe um endereço HTTP/HTTPS válido para o servidor relay.");
        }

        _settings = _settings with
        {
            EnableApplicationRelay = enabled,
            RelayServerUrl = relayUrl,
            EnableAutomaticUpnp = enabled ? false : _settings.EnableAutomaticUpnp
        };
        MultiplayerSettingsStore.Save(_settings);

        RelayEnabledCheckBox.IsChecked = enabled;
        RelayServerTextBox.Text = relayUrl;
        if (enabled)
        {
            UpnpEnabledCheckBox.IsChecked = false;
        }
    }

    internal async Task StartRelayRoomFromWebAsync(
        string? roomId,
        string? displayName,
        bool createPrivateRoom,
        string? roomPassword,
        string? relayServerUrl)
    {
        var password = NormalizePassword(roomPassword);
        if (createPrivateRoom && (password is null || password.Length < 4))
        {
            throw new InvalidOperationException(RoomPrivacyText.PasswordTooShort);
        }

        RoomTextBox.Text = MultiplayerWebInput.Normalize(
            roomId,
            $"navbr-{Random.Shared.Next(1000, 9999)}");
        NicknameTextBox.Text = MultiplayerWebInput.Normalize(displayName, _settings.DisplayName);
        PrivateRoomCheckBox.IsChecked = createPrivateRoom;
        RoomPasswordBox.Password = password ?? string.Empty;
        RelayServerTextBox.Text = (relayServerUrl ?? _settings.RelayServerUrl ?? string.Empty).Trim();
        RelayEnabledCheckBox.IsChecked = true;
        _settings = _settings with
        {
            EphemeralRoomPassword = password,
            EphemeralCreatePrivateRoom = createPrivateRoom
        };

        await StartRelayRoomAsync();
    }

    internal void SetAutomaticUpnpFromWeb(bool enabled)
    {
        if (_host.IsRunning)
        {
            throw new InvalidOperationException(
                "Pare a hospedagem atual antes de alterar o UPnP automático.");
        }

        _settings = _settings with
        {
            EnableAutomaticUpnp = enabled
        };
        MultiplayerSettingsStore.Save(_settings);

        _loadingUpnpUi = true;
        try
        {
            UpnpEnabledCheckBox.IsChecked = enabled;
        }
        finally
        {
            _loadingUpnpUi = false;
        }
    }

    internal async Task SendChatFromWebAsync(string text)
    {
        var normalized = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || !_client.IsConnected)
        {
            return;
        }

        await _client.SendChatMessageAsync(normalized);
    }

    internal async Task ConnectFromWebAsync(
        string? serverUrl,
        string? roomId,
        string? displayName,
        string? roomPassword)
    {
        if (_client.IsConnected)
        {
            return;
        }

        ServerTextBox.Text = MultiplayerWebInput.Normalize(serverUrl, _settings.ServerUrl);
        RoomTextBox.Text = MultiplayerWebInput.Normalize(roomId, _settings.RoomId);
        NicknameTextBox.Text = MultiplayerWebInput.Normalize(displayName, _settings.DisplayName);

        var password = NormalizePassword(roomPassword);
        RoomPasswordBox.Password = password ?? string.Empty;
        PrivateRoomCheckBox.IsChecked = false;
        _settings = _settings with
        {
            EphemeralRoomPassword = password,
            EphemeralCreatePrivateRoom = false
        };

        await ConnectToConfiguredServerAsync();
    }

    internal Task DisconnectFromWebAsync() => DisconnectAsync();

    internal async Task StartLocalHostFromWebAsync(
        string? roomId,
        string? displayName,
        bool createPrivateRoom,
        string? roomPassword,
        bool exposeInternet = true)
    {
        if (_host.IsRunning)
        {
            return;
        }

        var password = NormalizePassword(roomPassword);
        if (createPrivateRoom && (password is null || password.Length < 4))
        {
            throw new InvalidOperationException(RoomPrivacyText.PasswordTooShort);
        }

        RoomTextBox.Text = MultiplayerWebInput.Normalize(
            roomId,
            $"navbr-{Random.Shared.Next(1000, 9999)}");
        NicknameTextBox.Text = MultiplayerWebInput.Normalize(displayName, _settings.DisplayName);
        PrivateRoomCheckBox.IsChecked = createPrivateRoom;
        RoomPasswordBox.Password = password ?? string.Empty;
        _settings = _settings with
        {
            EphemeralRoomPassword = password,
            EphemeralCreatePrivateRoom = createPrivateRoom,
            EnableApplicationRelay = false,
            EnableAutomaticUpnp = exposeInternet
        };
        MultiplayerSettingsStore.Save(_settings);

        RelayEnabledCheckBox.IsChecked = false;
        UpnpEnabledCheckBox.IsChecked = exposeInternet;

        try
        {
            await _host.StartAsync(
                DefaultHostPort,
                enableAutomaticUpnp: exposeInternet);
            ServerTextBox.Text = _host.LocalServerUrl;
            RenderInviteAddresses();
            UpdateButtons();
            RefreshSessionSummary();
            await ConnectToConfiguredServerAsync();
        }
        catch
        {
            await _host.StopAsync();
            UpdateButtons();
            RefreshSessionSummary();
            throw;
        }
    }

    internal async Task StopLocalHostFromWebAsync()
    {
        if (_client.IsConnected)
        {
            await DisconnectAsync();
        }

        if (_host.IsRunning)
        {
            await _host.StopAsync();
        }

        InviteAddressText.Text = string.Empty;
        RoomInviteAddressText.Text = string.Empty;
        SetInputsEnabled(true);
        UpdateButtons();
        RefreshSessionSummary();
    }

    private object BuildWebRoomCompatibility()
    {
        var remotes = _players.Values
            .Where(player => !string.Equals(
                player.PlayerId,
                _settings.PlayerId,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (!_client.IsConnected || remotes.Length == 0)
        {
            return new
            {
                level = _client.IsConnected ? "waiting" : "none",
                remoteCount = remotes.Length,
                blocking = 0,
                warnings = 0,
                partial = 0,
                affectedAreas = Array.Empty<string>()
            };
        }

        var localManifest = OmsiCompatibilityManifestFactory.Create(
            _telemetrySource(),
            _activeMapSource());
        var requirePhysicalVehicle = _settings.ExperimentalPhysicalVehiclesEnabled;
        var reports = remotes
            .Select(player => OmsiCompatibilityEvaluator.Compare(
                localManifest,
                player.Compatibility,
                requirePhysicalVehicle))
            .ToArray();

        var blocking = reports.Count(report => report.HasBlockingIssues);
        var warnings = reports.Count(report =>
            !report.HasBlockingIssues &&
            report.Issues.Any(issue =>
                issue.Severity == CompatibilityIssueSeverity.Warning));
        var partial = reports.Count(report =>
            !report.HasBlockingIssues &&
            !report.Issues.Any(issue =>
                issue.Severity == CompatibilityIssueSeverity.Warning) &&
            report.Issues.Any(issue => IsUnknownCompatibilityIssue(issue.Code)));
        var affectedAreas = reports
            .SelectMany(report => report.Issues)
            .Where(issue =>
                issue.Severity != CompatibilityIssueSeverity.Info ||
                IsUnknownCompatibilityIssue(issue.Code))
            .Select(issue => CompatibilityArea(issue.Code))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Take(4)
            .ToArray();

        var level = blocking > 0
            ? "blocked"
            : warnings > 0
                ? "warning"
                : partial > 0
                    ? "partial"
                    : "compatible";

        return new
        {
            level,
            remoteCount = remotes.Length,
            blocking,
            warnings,
            partial,
            affectedAreas
        };
    }

    private sealed record WebSessionPoint(
        string PlayerId,
        string DisplayName,
        string Kind,
        double X,
        double Y,
        double HeadingDegrees,
        double SpeedKph,
        string? Line,
        bool IsLocal,
        string? Activity);
}

internal static class MultiplayerWebInput
{
    public static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
