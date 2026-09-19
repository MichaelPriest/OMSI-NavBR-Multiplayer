using System.Windows;
using System.Windows.Input;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private bool _relayUiInitialized;

    private void InitializeRelayUi()
    {
        if (_relayUiInitialized)
        {
            return;
        }

        _relayUiInitialized = true;
        RelayEnabledCheckBox.IsChecked = _settings.EnableApplicationRelay;
        RelayServerTextBox.Text = ResolveInitialRelayUrl();
        ApplyRelayLocalization();
        ApplyRelayUiState(persist: false);

        CreateRoomButton.PreviewMouseLeftButtonDown += RelayCreateButton_PreviewMouseLeftButtonDown;
        CreateRoomButton.PreviewKeyDown += RelayCreateButton_PreviewKeyDown;
        Closed += RelayWindowClosed;
    }

    private void ApplyRelayLocalization()
    {
        RelayEnabledCheckBox.Content = RelayText(
            "Servidor NavBR oficial (gratuito/limitado)",
            "Official NavBR Server (free/limited)",
            "Servidor NavBR oficial (gratuito/limitado)",
            "Offizieller NavBR-Server (kostenlos/begrenzt)",
            "Serveur NavBR officiel (gratuit/limité)");

        RelayServerLabelText.Text = RelayText(
            "Servidor online",
            "Online server",
            "Servidor online",
            "Online-Server",
            "Serveur en ligne");

        RelayDescriptionText.Text = RelayText(
            "Modo 1 de 3. O NavBR.Server roda no servidor oficial; o PC do jogador é cliente e não abre a TCP 27730. A infraestrutura atual usa Render Free e é limitada para Alpha/testes. Futuramente poderá existir assinatura oficial com maior capacidade.",
            "Mode 1 of 3. NavBR.Server runs on the official server; the player's PC is a client and does not expose TCP 27730. The current infrastructure uses Render Free and is limited for Alpha/testing. An official higher-capacity subscription may be offered in the future.",
            "Modo 1 de 3. NavBR.Server se ejecuta en el servidor oficial; el PC del jugador es cliente y no abre TCP 27730. La infraestructura actual usa Render Free y es limitada para Alpha/pruebas. En el futuro podrá existir una suscripción oficial con mayor capacidad.",
            "Modus 1 von 3. NavBR.Server läuft auf dem offiziellen Server; der Spieler-PC ist Client und gibt TCP 27730 nicht frei. Die aktuelle Infrastruktur nutzt Render Free und ist für Alpha/Tests begrenzt. Künftig könnte ein offizielles Abo mit mehr Kapazität angeboten werden.",
            "Mode 1 sur 3. NavBR.Server s’exécute sur le serveur officiel ; le PC du joueur est client et n’ouvre pas TCP 27730. L’infrastructure actuelle utilise Render Free et reste limitée pour l’Alpha/les tests. Un abonnement officiel offrant plus de capacité pourra être proposé à l’avenir.");
    }

    private void RelayEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_relayUiInitialized)
        {
            return;
        }

        ApplyRelayUiState(persist: true);
    }

    private void RelayServerTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_relayUiInitialized)
        {
            return;
        }

        var value = (RelayServerTextBox.Text ?? string.Empty).Trim();
        _settings = _settings with { RelayServerUrl = value };
        MultiplayerSettingsStore.Save(_settings);
    }

    private void ApplyRelayUiState(bool persist)
    {
        var enabled = RelayEnabledCheckBox.IsChecked == true;
        RelayServerTextBox.IsEnabled = enabled;

        if (enabled)
        {
            UpnpEnabledCheckBox.IsChecked = false;
        }

        if (!persist)
        {
            return;
        }

        _settings = _settings with
        {
            EnableApplicationRelay = enabled,
            RelayServerUrl = (RelayServerTextBox.Text ?? _settings.RelayServerUrl).Trim(),
            EnableAutomaticUpnp = enabled ? false : _settings.EnableAutomaticUpnp
        };
        MultiplayerSettingsStore.Save(_settings);
    }

    private void RelayCreateButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (RelayEnabledCheckBox.IsChecked != true)
        {
            return;
        }

        e.Handled = true;
        _ = StartRelayRoomAsync();
    }

    private void RelayCreateButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (RelayEnabledCheckBox.IsChecked != true || (e.Key != Key.Enter && e.Key != Key.Space))
        {
            return;
        }

        e.Handled = true;
        _ = StartRelayRoomAsync();
    }

    private async Task StartRelayRoomAsync()
    {
        var relayUrl = (RelayServerTextBox.Text ?? string.Empty).Trim();
        if (!TryNormalizeRelayUrl(relayUrl, out var normalizedRelayUrl))
        {
            StatusDetailText.Text = RelayText(
                "Informe um endereço HTTP/HTTPS válido para o servidor online.",
                "Enter a valid HTTP/HTTPS address for the online server.",
                "Introduce una dirección HTTP/HTTPS válida para el servidor online.",
                "Geben Sie eine gültige HTTP/HTTPS-Adresse für den Online-Server ein.",
                "Saisissez une adresse HTTP/HTTPS valide pour le serveur en ligne.");
            RelayServerTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(RoomTextBox.Text))
        {
            RoomTextBox.Text = $"navbr-{Random.Shared.Next(1000, 9999)}";
        }

        if (string.IsNullOrWhiteSpace(NicknameTextBox.Text))
        {
            StatusDetailText.Text = LocalizationService.Get("MultiplayerRequiredFields");
            return;
        }

        if (!PrepareRoomPrivacyForAction(createPrivateRoom: PrivateRoomCheckBox.IsChecked == true))
        {
            return;
        }

        try
        {
            if (_client.State != Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Disconnected)
            {
                await DisconnectAsync();
            }

            if (_host.IsRunning)
            {
                await _host.StopAsync();
            }

            UpnpEnabledCheckBox.IsChecked = false;
            ServerTextBox.Text = normalizedRelayUrl;
            _settings = _settings with
            {
                EnableApplicationRelay = true,
                RelayServerUrl = normalizedRelayUrl,
                EnableAutomaticUpnp = false
            };
            MultiplayerSettingsStore.Save(_settings);

            var roomId = RoomTextBox.Text.Trim();
            var inviteText = RelayText(
                $"Servidor: {normalizedRelayUrl} • sala {roomId}",
                $"Server: {normalizedRelayUrl} • room {roomId}",
                $"Servidor: {normalizedRelayUrl} • sala {roomId}",
                $"Server: {normalizedRelayUrl} • Raum {roomId}",
                $"Serveur : {normalizedRelayUrl} • salle {roomId}");
            InviteAddressText.Text = inviteText;
            RoomInviteAddressText.Text = inviteText;

            StatusDetailText.Text = RelayText(
                "Conectando ao servidor online NavBR…",
                "Connecting to the NavBR online server…",
                "Conectando al servidor online NavBR…",
                "Verbindung zum NavBR-Online-Server wird hergestellt…",
                "Connexion au serveur en ligne NavBR…");
            UpdateButtons();
            await ConnectToConfiguredServerAsync();
        }
        catch (Exception ex)
        {
            StatusDetailText.Text = LocalizationService.Format("MultiplayerConnectionError", ex.Message);
            SetInputsEnabled(true);
            UpdateButtons();
        }
    }

    private string ResolveInitialRelayUrl()
    {
        var configured = (_settings.RelayServerUrl ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var current = (ServerTextBox.Text ?? string.Empty).Trim();
        return IsLoopbackServerUrl(current) ? string.Empty : current;
    }

    private static bool TryNormalizeRelayUrl(string value, out string normalized)
    {
        normalized = string.Empty;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        normalized = uri.ToString().TrimEnd('/');
        return true;
    }

    private static bool IsLoopbackServerUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private void RelayWindowClosed(object? sender, EventArgs e)
    {
        CreateRoomButton.PreviewMouseLeftButtonDown -= RelayCreateButton_PreviewMouseLeftButtonDown;
        CreateRoomButton.PreviewKeyDown -= RelayCreateButton_PreviewKeyDown;
        Closed -= RelayWindowClosed;
    }

    private static string RelayText(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
