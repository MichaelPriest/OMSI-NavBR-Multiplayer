using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private Button? _publicRoomsButton;

    private void InitializePublicRoomBrowser()
    {
        if (_publicRoomsButton is not null || ConnectButton.Parent is not Grid actions)
        {
            return;
        }

        actions.ColumnDefinitions.Insert(2, new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(CopyInviteButton, 3);
        Grid.SetColumn(PasteInviteButton, 4);
        Grid.SetColumn(InviteAddressText, 5);

        _publicRoomsButton = new Button
        {
            Content = PublicRoomsButtonText(),
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 112
        };
        _publicRoomsButton.Click += PublicRoomsButton_Click;
        Grid.SetColumn(_publicRoomsButton, 2);
        actions.Children.Add(_publicRoomsButton);
    }

    private void RefreshPublicRoomBrowserLocalization()
    {
        if (_publicRoomsButton is not null)
        {
            _publicRoomsButton.Content = PublicRoomsButtonText();
        }
    }

    private void PublicRoomsButton_Click(object sender, RoutedEventArgs e)
    {
        var serverUrl = ServerTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            StatusDetailText.Text = LocalizationService.Get("MultiplayerRequiredFields");
            return;
        }

        var browser = new PublicRoomBrowserWindow(serverUrl)
        {
            Owner = this
        };

        if (browser.ShowDialog() != true || string.IsNullOrWhiteSpace(browser.SelectedRoomId))
        {
            return;
        }

        RoomTextBox.Text = browser.SelectedRoomId;
        PrivateRoomCheckBox.IsChecked = false;
        RoomPasswordBox.Password = string.Empty;
        _settings = _settings with
        {
            RoomId = browser.SelectedRoomId,
            EphemeralRoomPassword = null,
            EphemeralCreatePrivateRoom = false
        };
        RoomPrivacyStateText.Text = RoomPrivacyText.DraftPublic;
    }

    private static string PublicRoomsButtonText() =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => "Salas públicas",
            "es" => "Salas públicas",
            "de" => "Öffentliche Räume",
            "fr" => "Salons publics",
            _ => "Public rooms"
        };
}
