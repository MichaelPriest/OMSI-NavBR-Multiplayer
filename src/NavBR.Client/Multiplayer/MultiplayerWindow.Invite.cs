using System.Windows;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private const string InviteHeader = "NAVBR_INVITE_V1";

    private void CopyInviteButton_Loaded(object sender, RoutedEventArgs e)
    {
        CopyInviteButton.Content = "📋";
        CopyInviteButton.IsEnabled = true;
    }

    private void PasteInviteButton_Loaded(object sender, RoutedEventArgs e)
    {
        PasteInviteButton.Content = "📥";
    }

    private void CopyInviteButton_Click(object sender, RoutedEventArgs e)
    {
        var relayActive = _settings.EnableApplicationRelay && _client.IsConnected && !_host.IsRunning;
        if (!_host.IsRunning && !relayActive)
        {
            StatusDetailText.Text = Localization.LocalizationService.Get("MultiplayerDisconnectedDetail");
            return;
        }

        var roomId = RoomTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(roomId))
        {
            StatusDetailText.Text = Localization.LocalizationService.Get("MultiplayerRequiredFields");
            return;
        }

        string serverUrl;
        string invite;
        if (relayActive)
        {
            serverUrl = ServerTextBox.Text.Trim();
            if (!TryNormalizeRelayUrl(serverUrl, out serverUrl))
            {
                StatusDetailText.Text = Localization.LocalizationService.Get("MultiplayerRequiredFields");
                return;
            }

            invite = string.Join(
                Environment.NewLine,
                InviteHeader,
                $"server={serverUrl}",
                $"room={roomId}",
                "mode=relay");
        }
        else
        {
            var lanUrls = _host.GetLanJoinUrls();
            serverUrl = lanUrls.FirstOrDefault() ?? _host.LocalServerUrl;
            invite = string.Join(
                Environment.NewLine,
                InviteHeader,
                $"server={serverUrl}",
                $"room={roomId}",
                $"port={DefaultHostPort}",
                "mode=peer-host");
        }

        try
        {
            Clipboard.SetText(invite);
            StatusDetailText.Text = Localization.LocalizationService.Format(
                "MultiplayerInviteAddress",
                serverUrl,
                roomId);
        }
        catch (Exception ex)
        {
            StatusDetailText.Text = Localization.LocalizationService.Format(
                "MultiplayerHostError",
                ex.Message);
        }
    }

    private void PasteInviteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_client.State != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Disconnected || _host.IsRunning)
        {
            return;
        }

        try
        {
            var clipboard = Clipboard.GetText()?.Trim();
            if (!TryParseInvite(clipboard, out var serverUrl, out var roomId, out var mode))
            {
                StatusDetailText.Text = Localization.LocalizationService.Get("MultiplayerRequiredFields");
                return;
            }

            ServerTextBox.Text = serverUrl;
            RoomTextBox.Text = roomId;
            SyncDraftSettings();
            StatusDetailText.Text = mode == "relay"
                ? RelayText(
                    $"Convite relay • {serverUrl} • {roomId}",
                    $"Relay invite • {serverUrl} • {roomId}",
                    $"Invitación relay • {serverUrl} • {roomId}",
                    $"Relay-Einladung • {serverUrl} • {roomId}",
                    $"Invitation relais • {serverUrl} • {roomId}")
                : $"{serverUrl} • {roomId}";
        }
        catch (Exception ex)
        {
            StatusDetailText.Text = Localization.LocalizationService.Format(
                "MultiplayerConnectionError",
                ex.Message);
        }
    }

    private static bool TryParseInvite(
        string? text,
        out string serverUrl,
        out string roomId,
        out string mode)
    {
        serverUrl = string.Empty;
        roomId = string.Empty;
        mode = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return false;
        }

        if (string.Equals(lines[0], InviteHeader, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var line in lines.Skip(1))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0 || separator >= line.Length - 1)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (string.Equals(key, "server", StringComparison.OrdinalIgnoreCase))
                {
                    serverUrl = value;
                }
                else if (string.Equals(key, "room", StringComparison.OrdinalIgnoreCase))
                {
                    roomId = value;
                }
                else if (string.Equals(key, "mode", StringComparison.OrdinalIgnoreCase))
                {
                    mode = value.ToLowerInvariant();
                }
            }
        }
        else
        {
            // Compatibility with invitations copied by 0.3.0-alpha.3.
            var serverIndex = Array.FindIndex(lines, line =>
                line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

            if (serverIndex >= 0)
            {
                serverUrl = lines[serverIndex];
                if (serverIndex + 1 < lines.Length &&
                    !lines[serverIndex + 1].StartsWith("TCP ", StringComparison.OrdinalIgnoreCase))
                {
                    roomId = lines[serverIndex + 1];
                }
            }
        }

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        return roomId.Length is > 0 and <= 64;
    }
}
