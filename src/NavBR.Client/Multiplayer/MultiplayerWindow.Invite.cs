using System.Windows;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private void CopyInviteButton_Loaded(object sender, RoutedEventArgs e)
    {
        CopyInviteButton.Content = Localization.LocalizationService.Get("MultiplayerCopyInvite");
        CopyInviteButton.IsEnabled = true;
    }

    private void CopyInviteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_host.IsRunning)
        {
            StatusDetailText.Text = Localization.LocalizationService.Get("MultiplayerInviteHostFirst");
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
        var invite = Localization.LocalizationService.Format(
            "MultiplayerInviteClipboard",
            serverUrl,
            roomId,
            DefaultHostPort);

        try
        {
            Clipboard.SetText(invite);
            StatusDetailText.Text = Localization.LocalizationService.Get("MultiplayerInviteCopied");
        }
        catch (Exception ex)
        {
            StatusDetailText.Text = Localization.LocalizationService.Format(
                "MultiplayerInviteCopyError",
                ex.Message);
        }
    }
}
