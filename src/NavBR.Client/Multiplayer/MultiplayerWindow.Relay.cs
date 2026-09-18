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
            "Usar relay de aplicação quando a conexão direta não for possível (experimental)",
            "Use application relay when direct connection is not possible (experimental)",
            "Usar relay de aplicación cuando la conexión directa no sea posible (experimental)",
            "Anwendungs-Relay verwenden, wenn keine direkte Verbindung möglich ist (experimentell)",
            "Utiliser le relais applicatif lorsque la connexion directe est impossible (expérimental)");

        RelayServerLabelText.Text = RelayText(
            "Servidor relay",
            "Relay server",
            "Servidor relay",
            "Relay-Server",
            "Serveur relais");

        RelayDescriptionText.Text = RelayText(
            "No modo relay não é necessário abrir a TCP 27730 nem usar UPnP. A sala continua pertencendo a quem a criou; telemetria, chat e voz passam pelo servidor NavBR configurado.",
            "Relay mode does not require opening TCP 27730 or using UPnP. The room still belongs to its creator; telemetry, chat and voice pass through the configured NavBR server.",
            "El modo relay no requiere abrir TCP 27730 ni UPnP. La sala sigue perteneciendo a quien la creó; telemetría, chat y voz pasan por el servidor NavBR configurado.",
            "Im Relay-Modus müssen TCP 27730 und UPnP nicht geöffnet werden. Der Raum bleibt beim Ersteller; Telemetrie, Chat und Sprache laufen über den konfigurierten NavBR-Server.",
            "Le mode relais ne nécessite pas l’ouverture de TCP 27730 ni UPnP. La salle reste au créateur ; télémétrie, chat et voix transitent par le serveur NavBR configuré.");
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
                "Informe um endereço HTTP/HTTPS válido para o servidor relay.",
                "Enter a valid HTTP/HTTPS address for the relay server.",
                "Introduce una dirección HTTP/HTTPS válida para el servidor relay.",
                "Geben Sie eine gültige HTTP/HTTPS-Adresse für den Relay-Server ein.",
                "Saisissez une adresse HTTP/HTTPS valide pour le serveur relais.");
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
                "Conectando a sala pelo relay NavBR…",
                "Connecting the room through the NavBR relay…",
                "Conectando la sala mediante el relay NavBR…",
                "Raum wird über das NavBR-Relay verbunden…",
                "Connexion de la salle via le relais NavBR…");
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
