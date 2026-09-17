using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Operations;

internal sealed class SessionHealthWindow : Window
{
    private readonly Func<VehicleTelemetry?> _telemetryProvider;
    private readonly Func<OmsiPluginBridgeConnectionInfo> _pluginInfoProvider;
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _summary = new();
    private Border? _summaryCard;
    private readonly TextBlock _omsiState = new();
    private readonly TextBlock _multiplayerState = new();
    private readonly TextBlock _pluginState = new();
    private readonly TextBlock _remoteState = new();
    private readonly TextBlock _freshnessState = new();
    private readonly TextBlock _latencyState = new();
    private readonly TextBlock _jitterState = new();
    private readonly TextBlock _lossState = new();
    private readonly TextBlock _rateState = new();

    public SessionHealthWindow(
        Window owner,
        Func<VehicleTelemetry?> telemetryProvider,
        Func<OmsiPluginBridgeConnectionInfo> pluginInfoProvider)
    {
        Owner = owner;
        _telemetryProvider = telemetryProvider;
        _pluginInfoProvider = pluginInfoProvider;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 860d;
        Height = 680d;
        MinWidth = 740d;
        MinHeight = 600d;
        Background = Brush(6, 11, 16);
        Content = BuildContent();
        ApplyLocalization();
        Refresh();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750d) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private UIElement BuildContent()
    {
        var root = new Grid { Margin = new Thickness(28d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var heading = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 18d) };
        heading.Children.Add(new TextBlock
        {
            Tag = "health-title",
            Foreground = Brushes.White,
            FontSize = 25d,
            FontWeight = FontWeights.Bold
        });
        heading.Children.Add(new TextBlock
        {
            Tag = "health-subtitle",
            Foreground = Brush(142, 161, 174),
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 6d, 0d, 0d)
        });
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        _summaryCard = new Border
        {
            Padding = new Thickness(16d),
            Background = Brush(11, 20, 26),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = _summary
        };
        _summary.Foreground = Brushes.White;
        _summary.FontSize = 14d;
        _summary.FontWeight = FontWeights.SemiBold;
        Grid.SetRow(_summaryCard, 1);
        root.Children.Add(_summaryCard);

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0d, 18d, 0d, 0d)
        };
        var body = new StackPanel();
        var metrics = new UniformGrid
        {
            Columns = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        metrics.Children.Add(BuildMetricCard("Omsi", _omsiState));
        metrics.Children.Add(BuildMetricCard("Multiplayer", _multiplayerState));
        metrics.Children.Add(BuildMetricCard("Plugin", _pluginState));
        metrics.Children.Add(BuildMetricCard("Remote", _remoteState));
        metrics.Children.Add(BuildMetricCard("Freshness", _freshnessState));
        metrics.Children.Add(BuildMetricCard("Latency", _latencyState));
        metrics.Children.Add(BuildMetricCard("Jitter", _jitterState));
        metrics.Children.Add(BuildMetricCard("Loss", _lossState));
        metrics.Children.Add(BuildMetricCard("TelemetryRate", _rateState));
        body.Children.Add(metrics);

        body.Children.Add(new Border
        {
            Margin = new Thickness(0d, 8d, 0d, 0d),
            Padding = new Thickness(16d),
            Background = Brush(10, 19, 25),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = new TextBlock
            {
                Tag = "health-note",
                Foreground = Brush(141, 160, 173),
                FontSize = 10.5d,
                TextWrapping = TextWrapping.Wrap
            }
        });

        scroller.Content = body;
        Grid.SetRow(scroller, 2);
        root.Children.Add(scroller);
        return root;
    }

    private static Border BuildMetricCard(string key, TextBlock value)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Tag = "health-label:" + key,
            Foreground = Brush(127, 148, 162),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });
        value.Foreground = Brushes.White;
        value.FontSize = 16d;
        value.FontWeight = FontWeights.SemiBold;
        value.TextWrapping = TextWrapping.Wrap;
        value.Margin = new Thickness(0d, 6d, 0d, 0d);
        stack.Children.Add(value);

        return new Border
        {
            MinHeight = 104d,
            Margin = new Thickness(0d, 0d, 10d, 10d),
            Padding = new Thickness(14d),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brush(10, 19, 25),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Child = stack
        };
    }

    private void Refresh()
    {
        var telemetry = _telemetryProvider();
        var plugin = _pluginInfoProvider();
        var multiplayer = DispatcherSessionFeed.Snapshot();
        var network = SessionNetworkQualityFeed.Snapshot();
        var now = DateTimeOffset.UtcNow;

        var omsiActive = telemetry?.IsInGame == true;
        _omsiState.Text = omsiActive ? Text("Active") : Text("Waiting");
        _multiplayerState.Text = multiplayer.Connected ? Text("Connected") : Text("Disconnected");
        _pluginState.Text = plugin.IsConnected
            ? string.IsNullOrWhiteSpace(plugin.PluginComponentVersion)
                ? Text("Connected")
                : $"{Text("Connected")} • {plugin.PluginComponentVersion}"
            : Text("OptionalOffline");
        _remoteState.Text = multiplayer.Connected
            ? string.Format(Text("DriversCount"), multiplayer.RemoteDrivers.Count)
            : "—";

        if (!multiplayer.Connected || multiplayer.RemoteDrivers.Count == 0)
        {
            _freshnessState.Text = "—";
        }
        else
        {
            var newest = multiplayer.RemoteDrivers.Max(driver => driver.ReceivedAtUtc);
            var age = Math.Max(0d, (now - newest).TotalSeconds);
            _freshnessState.Text = age < 1d
                ? Text("Now")
                : string.Format(Text("SecondsAgo"), Math.Round(age));
        }

        if (!multiplayer.Connected || network.Samples < 2 || network.RoundTripMs is null)
        {
            _latencyState.Text = "—";
            _jitterState.Text = "—";
            _lossState.Text = "—";
            _rateState.Text = "—";
        }
        else
        {
            _latencyState.Text = $"{network.RoundTripMs.Value:F0} ms";
            _jitterState.Text = network.JitterMs is null ? "—" : $"{network.JitterMs.Value:F0} ms";
            _lossState.Text = $"{network.LossPercent:F1}%";
            _rateState.Text = network.Level switch
            {
                SessionNetworkQualityLevel.Poor => "~1.5 Hz",
                SessionNetworkQualityLevel.Degraded => "~2.5 Hz",
                _ => "~4 Hz"
            };
        }

        var networkHealthy = network.Level is SessionNetworkQualityLevel.Good or SessionNetworkQualityLevel.Unknown;
        _latencyState.Foreground = networkHealthy ? Brushes.White : Brush(255, 187, 91);
        _jitterState.Foreground = networkHealthy ? Brushes.White : Brush(255, 187, 91);
        _lossState.Foreground = network.Level == SessionNetworkQualityLevel.Poor
            ? Brush(255, 112, 112)
            : network.Level == SessionNetworkQualityLevel.Degraded
                ? Brush(255, 187, 91)
                : Brushes.White;

        _summary.Text = BuildSummary(omsiActive, multiplayer.Connected, multiplayer.RemoteDrivers.Count, network.Level);
        ApplySummaryVisual(omsiActive, multiplayer.Connected, network.Level);
    }

    private void ApplySummaryVisual(
        bool omsiActive,
        bool multiplayerConnected,
        SessionNetworkQualityLevel networkLevel)
    {
        if (_summaryCard is null)
        {
            return;
        }

        if (!omsiActive)
        {
            _summary.Foreground = Brush(151, 171, 185);
            _summaryCard.Background = Brush(13, 26, 36);
            _summaryCard.BorderBrush = Brush(28, 42, 51);
            return;
        }

        if (multiplayerConnected && networkLevel == SessionNetworkQualityLevel.Poor)
        {
            _summary.Foreground = Brush(255, 194, 198);
            _summaryCard.Background = Brush(50, 20, 25);
            _summaryCard.BorderBrush = Brush(239, 91, 100);
            return;
        }

        if (multiplayerConnected && networkLevel == SessionNetworkQualityLevel.Degraded)
        {
            _summary.Foreground = Brush(255, 221, 160);
            _summaryCard.Background = Brush(45, 34, 18);
            _summaryCard.BorderBrush = Brush(242, 184, 75);
            return;
        }

        _summary.Foreground = Brush(186, 244, 218);
        _summaryCard.Background = Brush(10, 38, 29);
        _summaryCard.BorderBrush = Brush(56, 201, 140);
    }

    private static string BuildSummary(
        bool omsiActive,
        bool multiplayerConnected,
        int remoteDrivers,
        SessionNetworkQualityLevel networkLevel)
    {
        if (!omsiActive)
        {
            return Text("SummaryWaitingOmsi");
        }
        if (!multiplayerConnected)
        {
            return Text("SummaryLocalReady");
        }
        if (networkLevel == SessionNetworkQualityLevel.Poor)
        {
            return Text("SummaryNetworkPoor");
        }
        if (networkLevel == SessionNetworkQualityLevel.Degraded)
        {
            return Text("SummaryNetworkDegraded");
        }
        if (remoteDrivers == 0)
        {
            return Text("SummaryConnectedWaiting");
        }
        return string.Format(Text("SummaryHealthy"), remoteDrivers);
    }

    private void ApplyLocalization()
    {
        Title = Text("Title");
        foreach (var text in Enumerate<TextBlock>(this))
        {
            if (text.Tag is not string tag)
            {
                continue;
            }
            if (tag == "health-title") text.Text = Text("Title");
            else if (tag == "health-subtitle") text.Text = Text("Subtitle");
            else if (tag == "health-note") text.Text = Text("Note");
            else if (tag.StartsWith("health-label:", StringComparison.Ordinal))
                text.Text = Text(tag["health-label:".Length..]);
        }
    }

    internal static string MenuText() => LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
    {
        "pt" => "◎  Saúde da sessão",
        "es" => "◎  Salud de la sesión",
        "de" => "◎  Sitzungsstatus",
        "fr" => "◎  Santé de session",
        _ => "◎  Session health"
    };

    private static string Text(string key)
    {
        var table = LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => Pt,
            "es" => Es,
            "de" => De,
            "fr" => Fr,
            _ => En
        };
        return table.TryGetValue(key, out var value) ? value : key;
    }

    private static readonly IReadOnlyDictionary<string, string> En = T(
        ("Title", "Session health"), ("Subtitle", "A simple view of OMSI, multiplayer, network quality and the optional plugin bridge."),
        ("Omsi", "OMSI"), ("Multiplayer", "Multiplayer"), ("Plugin", "Plugin bridge"), ("Remote", "Remote drivers"), ("Freshness", "Remote telemetry"),
        ("Latency", "LATENCY"), ("Jitter", "JITTER"), ("Loss", "EST. LOSS"), ("TelemetryRate", "TELEMETRY RATE"),
        ("Active", "Active"), ("Waiting", "Waiting for OMSI"), ("Connected", "Connected"), ("Disconnected", "Disconnected"), ("OptionalOffline", "Optional • offline"),
        ("DriversCount", "{0} driver(s)"), ("Now", "Receiving now"), ("SecondsAgo", "{0} s ago"),
        ("SummaryWaitingOmsi", "Open a trip in OMSI to start the operational checks."),
        ("SummaryLocalReady", "OMSI is active. Local navigation is ready; multiplayer is currently offline."),
        ("SummaryConnectedWaiting", "OMSI and multiplayer are active. Waiting for telemetry from another driver."),
        ("SummaryHealthy", "Session is receiving telemetry from {0} remote driver(s)."),
        ("SummaryNetworkDegraded", "The multiplayer link is degraded. NavBR reduced telemetry frequency to improve stability."),
        ("SummaryNetworkPoor", "The multiplayer link is poor. NavBR reduced telemetry traffic while the connection recovers."),
        ("Note", "Network values come from real lightweight probes to the same NavBR peer-host. Loss is estimated from the recent probe window; telemetry frequency adapts automatically without opening a second SignalR connection."));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("Title", "Saúde da sessão"), ("Subtitle", "Uma visão simples do OMSI, multiplayer, qualidade de rede e do bridge/plugin opcional."),
        ("Omsi", "OMSI"), ("Multiplayer", "Multiplayer"), ("Plugin", "Bridge/plugin"), ("Remote", "Motoristas remotos"), ("Freshness", "Telemetria remota"),
        ("Latency", "LATÊNCIA"), ("Jitter", "JITTER"), ("Loss", "PERDA EST."), ("TelemetryRate", "TAXA DE TELEMETRIA"),
        ("Active", "Ativo"), ("Waiting", "Aguardando OMSI"), ("Connected", "Conectado"), ("Disconnected", "Desconectado"), ("OptionalOffline", "Opcional • offline"),
        ("DriversCount", "{0} motorista(s)"), ("Now", "Recebendo agora"), ("SecondsAgo", "há {0} s"),
        ("SummaryWaitingOmsi", "Abra uma viagem no OMSI para iniciar as verificações operacionais."),
        ("SummaryLocalReady", "OMSI ativo. A navegação local está pronta; o multiplayer está desconectado."),
        ("SummaryConnectedWaiting", "OMSI e multiplayer ativos. Aguardando telemetria de outro motorista."),
        ("SummaryHealthy", "Sessão recebendo telemetria de {0} motorista(s) remoto(s)."),
        ("SummaryNetworkDegraded", "A conexão multiplayer está degradada. O NavBR reduziu a frequência de telemetria para melhorar a estabilidade."),
        ("SummaryNetworkPoor", "A conexão multiplayer está ruim. O NavBR reduziu o tráfego de telemetria enquanto a rede se recupera."),
        ("Note", "Os valores de rede vêm de sondagens leves reais para o mesmo peer-host do NavBR. A perda é estimada pela janela recente de sondagens; a frequência de telemetria se adapta sem abrir uma segunda conexão SignalR."));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("Title", "Salud de la sesión"), ("Subtitle", "Una vista simple de OMSI, multijugador, calidad de red y el bridge/plugin opcional."),
        ("Omsi", "OMSI"), ("Multiplayer", "Multijugador"), ("Plugin", "Bridge/plugin"), ("Remote", "Conductores remotos"), ("Freshness", "Telemetría remota"),
        ("Latency", "LATENCIA"), ("Jitter", "JITTER"), ("Loss", "PÉRDIDA EST."), ("TelemetryRate", "TASA DE TELEMETRÍA"),
        ("Active", "Activo"), ("Waiting", "Esperando OMSI"), ("Connected", "Conectado"), ("Disconnected", "Desconectado"), ("OptionalOffline", "Opcional • offline"),
        ("DriversCount", "{0} conductor(es)"), ("Now", "Recibiendo ahora"), ("SecondsAgo", "hace {0} s"),
        ("SummaryWaitingOmsi", "Abre un viaje en OMSI para iniciar las comprobaciones operativas."),
        ("SummaryLocalReady", "OMSI está activo. La navegación local está lista; el multijugador está desconectado."),
        ("SummaryConnectedWaiting", "OMSI y multijugador activos. Esperando telemetría de otro conductor."),
        ("SummaryHealthy", "La sesión recibe telemetría de {0} conductor(es) remoto(s)."),
        ("SummaryNetworkDegraded", "La conexión multijugador está degradada. NavBR redujo la frecuencia de telemetría para mejorar la estabilidad."),
        ("SummaryNetworkPoor", "La conexión multijugador es deficiente. NavBR redujo el tráfico de telemetría mientras la red se recupera."),
        ("Note", "Los valores de red provienen de sondeos ligeros reales al mismo peer-host de NavBR. La pérdida se estima con la ventana reciente; la frecuencia de telemetría se adapta sin abrir una segunda conexión SignalR."));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("Title", "Sitzungsstatus"), ("Subtitle", "Eine einfache Übersicht über OMSI, Mehrspieler, Netzwerkqualität und die optionale Plugin-Bridge."),
        ("Omsi", "OMSI"), ("Multiplayer", "Mehrspieler"), ("Plugin", "Plugin-Bridge"), ("Remote", "Remote-Fahrer"), ("Freshness", "Remote-Telemetrie"),
        ("Latency", "LATENZ"), ("Jitter", "JITTER"), ("Loss", "GESCH. VERLUST"), ("TelemetryRate", "TELEMETRIE-RATE"),
        ("Active", "Aktiv"), ("Waiting", "Warte auf OMSI"), ("Connected", "Verbunden"), ("Disconnected", "Getrennt"), ("OptionalOffline", "Optional • offline"),
        ("DriversCount", "{0} Fahrer"), ("Now", "Empfang läuft"), ("SecondsAgo", "vor {0} s"),
        ("SummaryWaitingOmsi", "Eine Fahrt in OMSI öffnen, um die Betriebsprüfungen zu starten."),
        ("SummaryLocalReady", "OMSI ist aktiv. Lokale Navigation ist bereit; Mehrspieler ist offline."),
        ("SummaryConnectedWaiting", "OMSI und Mehrspieler sind aktiv. Warte auf Telemetrie eines anderen Fahrers."),
        ("SummaryHealthy", "Sitzung empfängt Telemetrie von {0} Remote-Fahrer(n)."),
        ("SummaryNetworkDegraded", "Die Mehrspieler-Verbindung ist beeinträchtigt. NavBR hat die Telemetrie-Frequenz zur Stabilisierung reduziert."),
        ("SummaryNetworkPoor", "Die Mehrspieler-Verbindung ist schlecht. NavBR reduziert den Telemetrieverkehr, bis sich das Netzwerk erholt."),
        ("Note", "Die Netzwerkwerte stammen aus echten leichten Abfragen an denselben NavBR-Peer-Host. Verlust wird aus dem jüngsten Messfenster geschätzt; die Telemetrie-Frequenz passt sich ohne zweite SignalR-Verbindung an."));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("Title", "Santé de session"), ("Subtitle", "Une vue simple d’OMSI, du multijoueur, de la qualité réseau et du bridge/plugin optionnel."),
        ("Omsi", "OMSI"), ("Multiplayer", "Multijoueur"), ("Plugin", "Bridge/plugin"), ("Remote", "Conducteurs distants"), ("Freshness", "Télémétrie distante"),
        ("Latency", "LATENCE"), ("Jitter", "JITTER"), ("Loss", "PERTE EST."), ("TelemetryRate", "TAUX TÉLÉMÉTRIE"),
        ("Active", "Actif"), ("Waiting", "En attente d’OMSI"), ("Connected", "Connecté"), ("Disconnected", "Déconnecté"), ("OptionalOffline", "Optionnel • hors ligne"),
        ("DriversCount", "{0} conducteur(s)"), ("Now", "Réception en cours"), ("SecondsAgo", "il y a {0} s"),
        ("SummaryWaitingOmsi", "Ouvrez un service dans OMSI pour démarrer les vérifications opérationnelles."),
        ("SummaryLocalReady", "OMSI est actif. La navigation locale est prête; le multijoueur est déconnecté."),
        ("SummaryConnectedWaiting", "OMSI et multijoueur actifs. En attente de la télémétrie d’un autre conducteur."),
        ("SummaryHealthy", "La session reçoit la télémétrie de {0} conducteur(s) distant(s)."),
        ("SummaryNetworkDegraded", "La connexion multijoueur est dégradée. NavBR a réduit la fréquence de télémétrie pour améliorer la stabilité."),
        ("SummaryNetworkPoor", "La connexion multijoueur est mauvaise. NavBR réduit le trafic de télémétrie pendant la récupération du réseau."),
        ("Note", "Les valeurs réseau proviennent de sondes légères réelles vers le même peer-host NavBR. La perte est estimée sur la fenêtre récente; la fréquence de télémétrie s’adapte sans ouvrir une seconde connexion SignalR."));

    private static IReadOnlyDictionary<string, string> T(params (string Key, string Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<T> Enumerate<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            foreach (var child in Enumerate<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
        }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
