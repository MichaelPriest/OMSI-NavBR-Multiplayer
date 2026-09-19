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
            "Usar servidor online dedicado quando a conexão direta não for possível (experimental)",
            "Use dedicated online server when direct connection is not possible (experimental)",
            "Usar servidor online dedicado cuando la conexión directa no sea posible (experimental)",
            "dedizierten Online-Server verwenden, wenn keine direkte Verbindung möglich ist (experimentell)",
            "Utiliser le serveur en ligne dédié lorsque la connexion directe est impossible (expérimental)");

        RelayServerLabelText.Text = RelayText(
            "Servidor online",
            "Online server",
            "Servidor online",
            "Online-Server",
            "Serveur en ligne");

        RelayDescriptionText.Text = RelayText(
            "No modo servidor online, o NavBR.Server roda no servidor configurado. O PC do jogador é cliente e não precisa abrir a TCP 27730 nem usar UPnP.",
            "In online-server mode, NavBR.Server runs on the configured server. The player's PC is a client and does not need TCP 27730 forwarding or UPnP.",
            "En modo servidor online, NavBR.Server se ejecuta en el servidor configurado. El PC del jugador es un cliente y no necesita abrir TCP 27730 ni usar UPnP.",
            "Im Online-Server-Modus läuft NavBR.Server auf dem konfigurierten Server. Der Spieler-PC ist nur Client und benötigt weder TCP-27730-Freigabe noch UPnP.",
            "En mode serveur en ligne, NavBR.Server s’exécute sur le serveur configuré. Le PC du joueur est un client et ne nécessite ni ouverture TCP 27730 ni UPnP.");
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
                $"Relay: {normalizedRelayUrl} • sala {roomId}",
                $"Relay: {normalizedRelayUrl} • room {roomId}",
                $"Relay: {normalizedRelayUrl} • sala {roomId}",
                $"Relay: {normalizedRelayUrl} • Raum {roomId}",
                $"Relais : {normalizedRelayUrl} • salle {roomId}");
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
