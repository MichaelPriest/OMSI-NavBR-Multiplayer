namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    internal object BuildWebBridgeState()
    {
        var now = DateTimeOffset.UtcNow;
        var players = _players.Values
            .OrderBy(player => player.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(player =>
            {
                var roleplay = _remoteRoleplayCharacters.TryGetValue(player.PlayerId, out var frame) &&
                               frame.Character.IsActive &&
                               now - frame.Character.Timestamp <= TimeSpan.FromSeconds(3d);
                var speaking = _voiceActivity.TryGetValue(player.PlayerId, out var voiceAt) &&
                               now - voiceAt <= TimeSpan.FromSeconds(1.5d);

                return new
                {
                    playerId = player.PlayerId,
                    displayName = player.DisplayName,
                    roomId = player.RoomId,
                    mapName = player.MapName,
                    voiceEnabled = player.VoiceEnabled,
                    latencyMs = player.LatencyMs,
                    roleplayActive = roleplay,
                    speaking
                };
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
            roomIsPrivate = _client.CurrentRoomIsPrivate,
            inviteAddresses = _host.IsRunning ? _host.GetLanJoinUrls() : Array.Empty<string>(),
            latencyMs = _lastLatencyMs,
            voiceEnabled = VoiceEnabledCheckBox.IsChecked == true,
            voiceChannel = _settings.VoiceChannel,
            roleplayEnabled = _settings.ExperimentalRoleplayCharacterEnabled,
            localRoleplayActive = _localRoleplayCharacter?.IsActive == true,
            selectedRoleplayCharacter = SelectedRoleplayCharacter?.DisplayName,
            playerCount = players.Length,
            players,
            chat
        };
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
        string? roomPassword)
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
            EphemeralCreatePrivateRoom = createPrivateRoom
        };

        try
        {
            await _host.StartAsync(DefaultHostPort);
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
}

internal static class MultiplayerWebInput
{
    public static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
