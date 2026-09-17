using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private Border? _sessionSyncCard;
    private TextBlock? _sessionSyncStateText;
    private TextBlock? _sessionSyncDetailText;
    private DispatcherTimer? _sessionSyncTimer;
    private long _sessionSyncSequence;
    private bool _sessionSyncBusy;

    [ModuleInitializer]
    internal static void InitializeSessionSyncBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(SessionSyncWindowLoaded));
    }

    private static void SessionSyncWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallSessionSyncPreview),
            DispatcherPriority.ApplicationIdle);
    }

    private void InstallSessionSyncPreview()
    {
        if (_sessionSyncCard is not null || StatusDetailText.Parent is not StackPanel sessionPanel)
        {
            return;
        }

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = SessionSyncText(
                "SINCRONIZAÇÃO DA OPERAÇÃO",
                "OPERATION SYNC",
                "SINCRONIZACIÓN DE OPERACIÓN",
                "BETRIEBSSYNCHRONISIERUNG",
                "SYNCHRONISATION DE L’OPÉRATION"),
            Foreground = SessionSyncBrush(126, 158, 179),
            FontSize = 8.5d,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 0);
        header.Children.Add(title);

        _sessionSyncStateText = new TextBlock
        {
            Text = "PREVIEW",
            Foreground = SessionSyncBrush(111, 174, 218),
            FontSize = 8.5d,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var stateHost = new Border
        {
            BorderBrush = SessionSyncBrush(47, 94, 124),
            BorderThickness = new Thickness(1d),
            Background = SessionSyncBrush(9, 31, 45),
            CornerRadius = new CornerRadius(8d),
            Padding = new Thickness(7d, 3d, 7d, 3d),
            Child = _sessionSyncStateText
        };
        Grid.SetColumn(stateHost, 1);
        header.Children.Add(stateHost);

        _sessionSyncDetailText = new TextBlock
        {
            Foreground = SessionSyncBrush(177, 202, 218),
            FontSize = 9.3d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 6d, 0d, 0d)
        };

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(_sessionSyncDetailText);

        _sessionSyncCard = new Border
        {
            Background = SessionSyncBrush(7, 23, 34),
            BorderBrush = SessionSyncBrush(28, 56, 74),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Padding = new Thickness(9d, 7d, 9d, 7d),
            Margin = new Thickness(0d, 6d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = body,
            Visibility = Visibility.Collapsed,
            ToolTip = SessionSyncText(
                "Preview somente leitura. Esta etapa sincroniza o estado operacional da sala, mas não altera hora, data ou clima do OMSI.",
                "Read-only preview. This stage synchronizes room operational state but does not change OMSI time, date or weather.",
                "Vista previa de solo lectura. Esta etapa sincroniza el estado operativo de la sala, pero no cambia hora, fecha o clima de OMSI.",
                "Nur-Lese-Vorschau. Diese Stufe synchronisiert den Betriebszustand des Raums, ändert aber weder OMSI-Zeit, Datum noch Wetter.",
                "Aperçu en lecture seule. Cette étape synchronise l’état opérationnel de la salle sans modifier l’heure, la date ou la météo d’OMSI.")
        };
        sessionPanel.Children.Add(_sessionSyncCard);

        _sessionSyncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1200d)
        };
        _sessionSyncTimer.Tick += SessionSyncTimer_Tick;
        _sessionSyncTimer.Start();
        Closed += SessionSyncWindowClosed;
        RenderSessionSyncPreview();
    }

    private async void SessionSyncTimer_Tick(object? sender, EventArgs e)
    {
        if (_sessionSyncBusy)
        {
            return;
        }

        _sessionSyncBusy = true;
        try
        {
            if (!_client.IsConnected)
            {
                RenderSessionSyncPreview();
                return;
            }

            if (_client.IsTrafficAuthority)
            {
                var telemetry = _telemetrySource();
                if (telemetry?.IsInGame == true)
                {
                    _sessionSyncSequence++;
                    await _client.PublishSessionOperationalStateAsync(
                        telemetry,
                        _sessionSyncSequence);
                }
            }
            else
            {
                var current = _client.CurrentSessionOperationalState;
                if (current is null ||
                    DateTimeOffset.UtcNow - current.ServerTimestampUtc > TimeSpan.FromSeconds(5))
                {
                    await _client.RefreshSessionOperationalStateAsync();
                }
            }
        }
        catch
        {
            // Session sync is an in-development preview. Core multiplayer must
            // remain usable even if the optional sync channel is unavailable.
        }
        finally
        {
            _sessionSyncBusy = false;
            RenderSessionSyncPreview();
        }
    }

    private void RenderSessionSyncPreview()
    {
        if (_sessionSyncCard is null ||
            _sessionSyncStateText is null ||
            _sessionSyncDetailText is null)
        {
            return;
        }

        if (!_client.IsConnected)
        {
            _sessionSyncCard.Visibility = Visibility.Collapsed;
            return;
        }

        _sessionSyncCard.Visibility = Visibility.Visible;
        var state = _client.CurrentSessionOperationalState;
        if (state is null ||
            (!string.IsNullOrWhiteSpace(_client.TrafficAuthorityPlayerId) &&
             !string.Equals(
                 state.AuthorityPlayerId,
                 _client.TrafficAuthorityPlayerId,
                 StringComparison.OrdinalIgnoreCase)))
        {
            _sessionSyncStateText.Text = SessionSyncText(
                "AGUARDANDO",
                "WAITING",
                "ESPERANDO",
                "WARTET",
                "EN ATTENTE");
            _sessionSyncStateText.Foreground = SessionSyncBrush(237, 184, 75);
            _sessionSyncDetailText.Text = SessionSyncText(
                "Aguardando estado operacional da autoridade atual da sala.",
                "Waiting for operational state from the current room authority.",
                "Esperando el estado operativo de la autoridad actual de la sala.",
                "Warten auf den Betriebszustand der aktuellen Raumautorität.",
                "En attente de l’état opérationnel de l’autorité actuelle de la salle.");
            return;
        }

        _sessionSyncStateText.Text = _client.IsTrafficAuthority
            ? SessionSyncText("PUBLICANDO", "PUBLISHING", "PUBLICANDO", "SENDET", "PUBLICATION")
            : SessionSyncText("SINCRONIZADO", "SYNCED", "SINCRONIZADO", "SYNCHRON", "SYNCHRONISÉ");
        _sessionSyncStateText.Foreground = SessionSyncBrush(82, 215, 145);

        var authority = ResolveAuthorityDisplayName(state.AuthorityPlayerId);
        var line = SessionSyncValue(state.Line);
        var route = SessionSyncValue(state.Route);
        var destination = SessionSyncValue(state.DestinationName);
        var nextStop = SessionSyncValue(state.NextStopName);
        var map = SessionSyncValue(state.MapName);
        var ageSeconds = Math.Max(0d, (DateTimeOffset.UtcNow - state.ServerTimestampUtc).TotalSeconds);

        _sessionSyncDetailText.Text = SessionSyncText(
            $"Autoridade: {authority} • Linha {line} / rota {route} • Destino: {destination} • Próxima: {nextStop} • Mapa: {map} • {ageSeconds:0}s",
            $"Authority: {authority} • Line {line} / route {route} • Destination: {destination} • Next: {nextStop} • Map: {map} • {ageSeconds:0}s",
            $"Autoridad: {authority} • Línea {line} / ruta {route} • Destino: {destination} • Próxima: {nextStop} • Mapa: {map} • {ageSeconds:0}s",
            $"Autorität: {authority} • Linie {line} / Route {route} • Ziel: {destination} • Nächste: {nextStop} • Karte: {map} • {ageSeconds:0}s",
            $"Autorité : {authority} • Ligne {line} / trajet {route} • Destination : {destination} • Prochain : {nextStop} • Carte : {map} • {ageSeconds:0}s");
    }

    private void SessionSyncWindowClosed(object? sender, EventArgs e)
    {
        if (_sessionSyncTimer is not null)
        {
            _sessionSyncTimer.Stop();
            _sessionSyncTimer.Tick -= SessionSyncTimer_Tick;
            _sessionSyncTimer = null;
        }
        Closed -= SessionSyncWindowClosed;
    }

    private static string SessionSyncValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static SolidColorBrush SessionSyncBrush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    private static string SessionSyncText(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
