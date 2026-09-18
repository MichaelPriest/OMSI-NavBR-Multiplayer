using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private void InitializePublicRoomBrowser()
    {
        RefreshPublicRoomBrowserLocalization();
    }

    private void RefreshPublicRoomBrowserLocalization()
    {
        PublicRoomsButton.Content = PublicRoomsButtonText();
        PublicRoomsButton.ToolTip = PublicRoomsToolTipText();
    }

    private void PublicRoomsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IsVisible || !ShowInTaskbar)
        {
            return;
        }

        var serverUrl = ServerTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            StatusDetailText.Text = LocalizationService.Get("MultiplayerRequiredFields");
            return;
        }

        var localManifest = OmsiCompatibilityManifestFactory.Create(
            _telemetrySource(),
            _activeMapSource());
        var browser = new PublicRoomBrowserWindow(serverUrl, localManifest)
        {
            Owner = this
        };

        if (browser.ShowDialog() != true ||
            string.IsNullOrWhiteSpace(browser.SelectedRoomId) ||
            browser.SelectedRoom is null)
        {
            return;
        }

        var selectedRoom = browser.SelectedRoom;
        RoomTextBox.Text = selectedRoom.RoomId;
        PrivateRoomCheckBox.IsChecked = false;
        RoomPasswordBox.Password = string.Empty;
        _settings = _settings with
        {
            RoomId = selectedRoom.RoomId,
            EphemeralRoomPassword = null,
            EphemeralCreatePrivateRoom = false
        };
        RoomPrivacyStateText.Text = RoomPrivacyText.DraftPublic;
        StatusDetailText.Text = PublicRoomSelectedText(selectedRoom.RoomId, selectedRoom.MapName);
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
            "pt" => "Procurar salas públicas e conferir o mapa obrigatório antes de entrar.",
            "es" => "Buscar salas públicas y comprobar el mapa obligatorio antes de entrar.",
            "de" => "Öffentliche Räume suchen und die erforderliche Karte vor dem Beitritt prüfen.",
            "fr" => "Rechercher les salons publics et vérifier la carte requise avant de rejoindre.",
            _ => "Browse public rooms and check the required map before joining."
        };

    private static string PublicRoomSelectedText(string roomId, string? mapName)
    {
        var map = string.IsNullOrWhiteSpace(mapName) ? "—" : mapName.Trim();
        return LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => $"Sala pública selecionada: {roomId} • Mapa obrigatório: {map}. Carregue esse mapa no OMSI antes de conectar.",
            "es" => $"Sala pública seleccionada: {roomId} • Mapa obligatorio: {map}. Carga ese mapa en OMSI antes de conectar.",
            "de" => $"Öffentlicher Raum ausgewählt: {roomId} • Erforderliche Karte: {map}. Lade diese Karte in OMSI, bevor du verbindest.",
            "fr" => $"Salon public sélectionné : {roomId} • Carte requise : {map}. Chargez cette carte dans OMSI avant la connexion.",
            _ => $"Public room selected: {roomId} • Required map: {map}. Load this map in OMSI before connecting."
        };
    }
}
