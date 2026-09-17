using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private Button? _publicRoomsButton;

    private void InitializePublicRoomBrowser()
    {
        if (_publicRoomsButton is not null)
        {
            return;
        }

        Panel? actions = CopyInviteButton.Parent as Panel;
        if (actions is null)
        {
            actions = PasteInviteButton.Parent as Panel;
        }

        if (actions is null)
        {
            return;
        }

        _publicRoomsButton = new Button
        {
            Content = PublicRoomsButtonText(),
            Margin = new Thickness(8d, 0d, 0d, 0d),
            MinWidth = 112d,
            Height = 34d,
            ToolTip = PublicRoomsToolTipText()
        };
        _publicRoomsButton.Click += PublicRoomsButton_Click;

        if (actions is StackPanel stack)
        {
            stack.Children.Insert(0, _publicRoomsButton);
        }
        else
        {
            actions.Children.Add(_publicRoomsButton);
        }
    }

    private void RefreshPublicRoomBrowserLocalization()
    {
        if (_publicRoomsButton is not null)
        {
            _publicRoomsButton.Content = PublicRoomsButtonText();
            _publicRoomsButton.ToolTip = PublicRoomsToolTipText();
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
        StatusDetailText.Text = PublicRoomSelectedText(browser.SelectedRoomId);
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

    private static string PublicRoomsToolTipText() =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => "Procurar salas públicas disponíveis no servidor configurado.",
            "es" => "Buscar salas públicas disponibles en el servidor configurado.",
            "de" => "Öffentliche Räume auf dem konfigurierten Server suchen.",
            "fr" => "Rechercher les salons publics disponibles sur le serveur configuré.",
            _ => "Browse public rooms available on the configured server."
        };

    private static string PublicRoomSelectedText(string roomId) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => $"Sala pública selecionada: {roomId}. Confirme seu nome e entre na sala.",
            "es" => $"Sala pública seleccionada: {roomId}. Confirma tu nombre y entra en la sala.",
            "de" => $"Öffentlicher Raum ausgewählt: {roomId}. Namen prüfen und Raum beitreten.",
            "fr" => $"Salon public sélectionné : {roomId}. Vérifiez votre nom puis rejoignez la salle.",
            _ => $"Public room selected: {roomId}. Confirm your name and join the room."
        };
}
