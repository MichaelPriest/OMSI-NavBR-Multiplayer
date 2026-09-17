using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private bool _relayUiInstalled;
    private CheckBox? _relayEnabledCheckBox;
    private TextBox? _relayServerTextBox;
    private Button? _relayWizardCreateButton;

    [ModuleInitializer]
    internal static void InitializeRelayBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(RelayWindowLoaded));
    }

    private static void RelayWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallRelayUi),
            DispatcherPriority.ApplicationIdle);
    }

    private void InstallRelayUi()
    {
        if (_relayUiInstalled)
        {
            return;
        }

        var peerHostLabel = FindRelayDescendants<TextBlock>(this)
            .FirstOrDefault(text => string.Equals(
                text.Text?.Trim(),
                "PEER-HOST • TCP 27730",
                StringComparison.OrdinalIgnoreCase));
        if (peerHostLabel is null || FindRelayAncestor<StackPanel>(peerHostLabel) is not StackPanel networkPage)
        {
            return;
        }

        _relayUiInstalled = true;

        var initialRelayUrl = ResolveInitialRelayUrl();
        _relayEnabledCheckBox = new CheckBox
        {
            IsChecked = _settings.EnableApplicationRelay,
            Content = RelayText(
                "Usar relay de aplicação quando a conexão direta não for possível (experimental)",
                "Use application relay when direct connection is not possible (experimental)",
                "Usar relay de aplicación cuando la conexión directa no sea posible (experimental)",
                "Anwendungs-Relay verwenden, wenn keine direkte Verbindung möglich ist (experimentell)",
                "Utiliser le relais applicatif lorsque la connexion directe est impossible (expérimental)"),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 9d, 0d, 4d)
        };

        _relayServerTextBox = new TextBox
        {
            Text = initialRelayUrl,
            Height = 34d,
            Padding = new Thickness(9d, 5d, 9d, 5d),
            Background = WizardBrush(5, 15, 24),
            Foreground = Brushes.White,
            BorderBrush = WizardBrush(35, 63, 82),
            BorderThickness = new Thickness(1d),
            IsEnabled = _relayEnabledCheckBox.IsChecked == true,
            ToolTip = RelayText(
                "Endereço HTTP/HTTPS de um servidor NavBR com o Hub Multiplayer ativo.",
                "HTTP/HTTPS address of a NavBR server with the Multiplayer Hub enabled.",
                "Dirección HTTP/HTTPS de un servidor NavBR con el Hub Multiplayer activo.",
                "HTTP/HTTPS-Adresse eines NavBR-Servers mit aktivem Multiplayer-Hub.",
                "Adresse HTTP/HTTPS d’un serveur NavBR avec le Hub Multiplayer actif.")
        };

        var relayPanel = new StackPanel
        {
            Margin = new Thickness(0d, 2d, 0d, 8d)
        };
        relayPanel.Children.Add(_relayEnabledCheckBox);
        relayPanel.Children.Add(new TextBlock
        {
            Text = RelayText("Servidor relay", "Relay server", "Servidor relay", "Relay-Server", "Serveur relais"),
            Foreground = WizardBrush(151, 176, 193),
            FontSize = 9.5d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(22d, 4d, 0d, 5d)
        });
        var relayUrlHost = new Border
        {
            Margin = new Thickness(22d, 0d, 0d, 0d),
            Child = _relayServerTextBox
        };
        relayPanel.Children.Add(relayUrlHost);
        relayPanel.Children.Add(new TextBlock
        {
            Text = RelayText(
                "No modo relay não é necessário abrir a TCP 27730 nem usar UPnP. A sala continua pertencendo a quem a criou; telemetria, chat e voz passam pelo servidor NavBR configurado.",
                "Relay mode does not require opening TCP 27730 or using UPnP. The room still belongs to its creator; telemetry, chat and voice pass through the configured NavBR server.",
                "El modo relay no requiere abrir TCP 27730 ni usar UPnP. La sala sigue perteneciendo a quien la creó; telemetría, chat y voz pasan por el servidor NavBR configurado.",
                "Im Relay-Modus müssen TCP 27730 und UPnP nicht geöffnet werden. Der Raum bleibt beim Ersteller; Telemetrie, Chat und Sprache laufen über den konfigurierten NavBR-Server.",
                "Le mode relais ne nécessite pas l’ouverture de TCP 27730 ni UPnP. La salle reste au créateur ; télémétrie, chat et voix transitent par le serveur NavBR configuré."),
            Foreground = WizardBrush(112, 139, 158),
            FontSize = 10d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(22d, 6d, 0d, 6d)
        });

        var summaryIndex = networkPage.Children
            .OfType<TextBlock>()
            .Select((text, index) => (text, index))
            .Where(item => item.text.FontSize >= 10.9d && item.text.Foreground is not null)
            .Select(item => item.index)
            .LastOrDefault();
        var insertIndex = Math.Clamp(summaryIndex, 1, networkPage.Children.Count);
        networkPage.Children.Insert(insertIndex, relayPanel);

        _relayEnabledCheckBox.Checked += RelayEnabledChanged;
        _relayEnabledCheckBox.Unchecked += RelayEnabledChanged;
        _relayServerTextBox.LostFocus += RelayServerTextBox_LostFocus;

        _relayWizardCreateButton = FindRelayDescendants<Button>(this)
            .FirstOrDefault(button =>
                !ReferenceEquals(button, CreateRoomButton) &&
                IsRelayCreateButtonLabel(button.Content?.ToString()));
        if (_relayWizardCreateButton is not null)
        {
            _relayWizardCreateButton.PreviewMouseLeftButtonDown += RelayCreateButton_PreviewMouseLeftButtonDown;
            _relayWizardCreateButton.PreviewKeyDown += RelayCreateButton_PreviewKeyDown;
        }

        ApplyRelayUiState(persist: false);
        Closed += RelayWindowClosed;
    }

    private void RelayEnabledChanged(object sender, RoutedEventArgs e) => ApplyRelayUiState(persist: true);

    private void RelayServerTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var value = (_relayServerTextBox?.Text ?? string.Empty).Trim();
        _settings = _settings with { RelayServerUrl = value };
        MultiplayerSettingsStore.Save(_settings);
    }

    private void ApplyRelayUiState(bool persist)
    {
        var enabled = _relayEnabledCheckBox?.IsChecked == true;
        if (_relayServerTextBox is not null)
        {
            _relayServerTextBox.IsEnabled = enabled;
        }

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
            RelayServerUrl = (_relayServerTextBox?.Text ?? _settings.RelayServerUrl).Trim(),
            EnableAutomaticUpnp = enabled ? false : _settings.EnableAutomaticUpnp
        };
        MultiplayerSettingsStore.Save(_settings);
    }

    private void RelayCreateButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_relayEnabledCheckBox?.IsChecked != true)
        {
            return;
        }

        e.Handled = true;
        _ = StartRelayRoomAsync();
    }

    private void RelayCreateButton_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_relayEnabledCheckBox?.IsChecked != true || (e.Key != Key.Enter && e.Key != Key.Space))
        {
            return;
        }

        e.Handled = true;
        _ = StartRelayRoomAsync();
    }

    private async Task StartRelayRoomAsync()
    {
        var relayUrl = (_relayServerTextBox?.Text ?? string.Empty).Trim();
        if (!TryNormalizeRelayUrl(relayUrl, out var normalizedRelayUrl))
        {
            StatusDetailText.Text = RelayText(
                "Informe um endereço HTTP/HTTPS válido para o servidor relay.",
                "Enter a valid HTTP/HTTPS address for the relay server.",
                "Introduce una dirección HTTP/HTTPS válida para el servidor relay.",
                "Geben Sie eine gültige HTTP/HTTPS-Adresse für den Relay-Server ein.",
                "Saisissez une adresse HTTP/HTTPS valide pour le serveur relais.");
            _relayServerTextBox?.Focus();
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

            InviteAddressText.Text = RelayText(
                $"Relay: {normalizedRelayUrl} • sala {_settings.RoomId}",
                $"Relay: {normalizedRelayUrl} • room {_settings.RoomId}",
                $"Relay: {normalizedRelayUrl} • sala {_settings.RoomId}",
                $"Relay: {normalizedRelayUrl} • Raum {_settings.RoomId}",
                $"Relais : {normalizedRelayUrl} • salle {_settings.RoomId}");
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

    private static bool IsRelayCreateButtonLabel(string? label) =>
        label is "Criar sala" or "Create room" or "Crear sala" or "Raum erstellen" or "Créer la salle";

    private void RelayWindowClosed(object? sender, EventArgs e)
    {
        if (_relayEnabledCheckBox is not null)
        {
            _relayEnabledCheckBox.Checked -= RelayEnabledChanged;
            _relayEnabledCheckBox.Unchecked -= RelayEnabledChanged;
        }
        if (_relayServerTextBox is not null)
        {
            _relayServerTextBox.LostFocus -= RelayServerTextBox_LostFocus;
        }
        if (_relayWizardCreateButton is not null)
        {
            _relayWizardCreateButton.PreviewMouseLeftButtonDown -= RelayCreateButton_PreviewMouseLeftButtonDown;
            _relayWizardCreateButton.PreviewKeyDown -= RelayCreateButton_PreviewKeyDown;
        }
        Closed -= RelayWindowClosed;
    }

    private static IEnumerable<T> FindRelayDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindRelayDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static T? FindRelayAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
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
