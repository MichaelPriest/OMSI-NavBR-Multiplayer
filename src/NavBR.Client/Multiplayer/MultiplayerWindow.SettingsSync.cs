namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        RoomTextBox.TextChanged -= RoomTextBox_SettingsChanged;
        RoomTextBox.TextChanged += RoomTextBox_SettingsChanged;
        NicknameTextBox.TextChanged -= NicknameTextBox_SettingsChanged;
        NicknameTextBox.TextChanged += NicknameTextBox_SettingsChanged;

        SyncDraftSettings();
    }

    private void RoomTextBox_SettingsChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        SyncDraftSettings();
        if (_host.IsRunning)
        {
            RenderInviteAddresses();
        }
    }

    private void NicknameTextBox_SettingsChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        SyncDraftSettings();
    }

    private void SyncDraftSettings()
    {
        _settings = _settings with
        {
            RoomId = RoomTextBox.Text.Trim(),
            DisplayName = NicknameTextBox.Text.Trim()
        };
    }
}
