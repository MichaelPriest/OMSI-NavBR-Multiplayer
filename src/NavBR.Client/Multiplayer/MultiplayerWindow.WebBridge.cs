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
}
