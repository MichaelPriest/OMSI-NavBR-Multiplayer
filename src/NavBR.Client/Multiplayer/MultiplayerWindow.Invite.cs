using System.Windows;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private void CopyInviteButton_Loaded(object sender, RoutedEventArgs e)
    {
        CopyInviteButton.Content = "📋";
        CopyInviteButton.IsEnabled = true;
    }

    private void CopyInviteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_host.IsRunning)
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

        var lanUrls = _host.GetLanJoinUrls();
        var serverUrl = lanUrls.FirstOrDefault() ?? _host.LocalServerUrl;
        var invite = string.Join(
            Environment.NewLine,
            "OMSI NavBR Multiplayer",
            serverUrl,
            roomId,
            $"TCP {DefaultHostPort}");

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
}
