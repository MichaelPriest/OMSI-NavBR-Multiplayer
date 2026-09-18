using System.Windows;
using System.Windows.Input;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private bool _roomPrivacyInitialized;
    private bool _roomPrivacyEventsHooked;

    private void PrivateRoomCheckBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_roomPrivacyInitialized)
        {
            _roomPrivacyInitialized = true;
            PrivateRoomCheckBox.IsChecked = false;
            RoomPasswordBox.Password = string.Empty;
            _settings = _settings with
            {
                EphemeralRoomPassword = null,
                EphemeralCreatePrivateRoom = false
            };
        }

        ApplyRoomPrivacyLocalization();
        RenderRoomPrivacyDraft();
        HookRoomPrivacyEvents();
    }

    private void PrivateRoomCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_roomPrivacyInitialized)
        {
            return;
        }

        _settings = _settings with
        {
            EphemeralCreatePrivateRoom = PrivateRoomCheckBox.IsChecked == true,
            EphemeralRoomPassword = NormalizePassword(RoomPasswordBox.Password)
        };
        RenderRoomPrivacyDraft();
    }

    private void RoomPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_roomPrivacyInitialized)
        {
            return;
        }

        _settings = _settings with
        {
            EphemeralRoomPassword = NormalizePassword(RoomPasswordBox.Password)
        };
    }

    private void CreateRoomButton_PrivacyPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_host.IsRunning)
        {
            return;
        }

        if (!PrepareRoomPrivacyForAction(createPrivateRoom: PrivateRoomCheckBox.IsChecked == true))
        {
            e.Handled = true;
        }
    }

    private void CreateRoomButton_PrivacyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_host.IsRunning || (e.Key != Key.Enter && e.Key != Key.Space))
        {
            return;
        }

        if (!PrepareRoomPrivacyForAction(createPrivateRoom: PrivateRoomCheckBox.IsChecked == true))
        {
            e.Handled = true;
        }
    }

    private void ConnectButton_PrivacyPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_client.State != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Disconnected)
        {
            return;
        }

        PrepareRoomPrivacyForAction(createPrivateRoom: false);
    }

    private void ConnectButton_PrivacyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_client.State != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Disconnected ||
            (e.Key != Key.Enter && e.Key != Key.Space))
        {
            return;
        }

        PrepareRoomPrivacyForAction(createPrivateRoom: false);
    }

    private bool PrepareRoomPrivacyForAction(bool createPrivateRoom)
    {
        var password = NormalizePassword(RoomPasswordBox.Password);
        if (createPrivateRoom && (password is null || password.Length < 4))
        {
            StatusDetailText.Text = RoomPrivacyText.PasswordTooShort;
            RoomPasswordBox.Focus();
            return false;
        }

        _settings = _settings with
        {
            EphemeralRoomPassword = password,
            EphemeralCreatePrivateRoom = createPrivateRoom
        };
        return true;
    }

    private void HookRoomPrivacyEvents()
    {
        if (_roomPrivacyEventsHooked)
        {
            return;
        }

        _roomPrivacyEventsHooked = true;
        _client.RoomSnapshotReceived += RoomPrivacy_RoomSnapshotReceived;
        Closed += RoomPrivacy_WindowClosed;
    }

    private void RoomPrivacy_RoomSnapshotReceived(RoomSnapshot snapshot)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            RoomPrivacyStateText.Text = snapshot.IsPrivate
                ? RoomPrivacyText.ConnectedPrivate
                : RoomPrivacyText.ConnectedPublic;
        });
    }

    private void RoomPrivacy_WindowClosed(object? sender, EventArgs e)
    {
        _client.RoomSnapshotReceived -= RoomPrivacy_RoomSnapshotReceived;
        _settings = _settings with
        {
            EphemeralRoomPassword = null,
            EphemeralCreatePrivateRoom = false
        };

        if (RoomPasswordBox is not null)
        {
            RoomPasswordBox.Password = string.Empty;
        }
    }

    private void ApplyRoomPrivacyLocalization()
    {
        PrivateRoomCheckBox.Content = RoomPrivacyText.PrivateRoomLabel;
        RoomPrivacyDescriptionText.Text = RoomPrivacyText.PrivacyDescription;
        RoomPasswordLabelText.Text = RoomPrivacyText.PasswordLabel;
        RoomPasswordHintText.Text = $"{RoomPrivacyText.PasswordHint}  {RoomPrivacyText.TransportNotice}";
    }

    private void RenderRoomPrivacyDraft()
    {
        RoomPrivacyStateText.Text = PrivateRoomCheckBox.IsChecked == true
            ? RoomPrivacyText.DraftPrivate
            : RoomPrivacyText.DraftPublic;
    }

    private static string? NormalizePassword(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
