using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Operations;

internal sealed class SessionHealthWindow : Window
{
    private readonly Func<VehicleTelemetry?> _telemetryProvider;
    private readonly Func<OmsiPluginBridgeConnectionInfo> _pluginInfoProvider;
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _summary = new();
    private readonly TextBlock _omsiState = new();
    private readonly TextBlock _multiplayerState = new();
    private readonly TextBlock _pluginState = new();
    private readonly TextBlock _remoteState = new();
    private readonly TextBlock _freshnessState = new();

    public SessionHealthWindow(
        Window owner,
        Func<VehicleTelemetry?> telemetryProvider,
        Func<OmsiPluginBridgeConnectionInfo> pluginInfoProvider)
    {
        Owner = owner;
        _telemetryProvider = telemetryProvider;
        _pluginInfoProvider = pluginInfoProvider;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 780d;
        Height = 610d;
        MinWidth = 720d;
        MinHeight = 560d;
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

        var summaryCard = new Border
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
        Grid.SetRow(summaryCard, 1);
        root.Children.Add(summaryCard);

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0d, 18d, 0d, 0d)
        };
        var body = new StackPanel();
        var metrics = new WrapPanel();
        metrics.Children.Add(BuildMetricCard("Omsi", _omsiState));
        metrics.Children.Add(BuildMetricCard("Multiplayer", _multiplayerState));
        metrics.Children.Add(BuildMetricCard("Plugin", _pluginState));
        metrics.Children.Add(BuildMetricCard("Remote", _remoteState));
        metrics.Children.Add(BuildMetricCard("Freshness", _freshnessState));
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
            Width = 220d,
            MinHeight = 104d,
            Margin = new Thickness(0d, 0d, 10d, 10d),
            Padding = new Thickness(14d),
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

        _summary.Text = BuildSummary(omsiActive, multiplayer.Connected, multiplayer.RemoteDrivers.Count);
    }

    private static string BuildSummary(bool omsiActive, bool multiplayerConnected, int remoteDrivers)
    {
        if (!omsiActive)
        {
            return Text("SummaryWaitingOmsi");
        }
        if (!multiplayerConnected)
        {
            return Text("SummaryLocalReady");
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
        ("Title", "Session health"), ("Subtitle", "A simple view of OMSI, multiplayer and the optional plugin bridge."),
        ("Omsi", "OMSI"), ("Multiplayer", "Multiplayer"), ("Plugin", "Plugin bridge"), ("Remote", "Remote drivers"), ("Freshness", "Remote telemetry"),
        ("Active", "Active"), ("Waiting", "Waiting for OMSI"), ("Connected", "Connected"), ("Disconnected", "Disconnected"), ("OptionalOffline", "Optional • offline"),
        ("DriversCount", "{0} driver(s)"), ("Now", "Receiving now"), ("SecondsAgo", "{0} s ago"),
        ("SummaryWaitingOmsi", "Open a trip in OMSI to start the operational checks."),
        ("SummaryLocalReady", "OMSI is active. Local navigation is ready; multiplayer is currently offline."),
        ("SummaryConnectedWaiting", "OMSI and multiplayer are active. Waiting for telemetry from another driver."),
        ("SummaryHealthy", "Session is receiving telemetry from {0} remote driver(s)."),
        ("Note", "The OMSI plugin is optional for the normal GPS/HUD and direct multiplayer flow. Ping, jitter and packet-loss indicators will only appear after NavBR has a real active measurement; this screen does not invent network values."));
    private static readonly IReadOnlyDictionary<string, string> Pt = T(
        ("Title", "Saúde da sessão"), ("Subtitle", "Uma visão simples do OMSI, multiplayer e do bridge/plugin opcional."),
        ("Omsi", "OMSI"), ("Multiplayer", "Multiplayer"), ("Plugin", "Bridge/plugin"), ("Remote", "Motoristas remotos"), ("Freshness", "Telemetria remota"),
        ("Active", "Ativo"), ("Waiting", "Aguardando OMSI"), ("Connected", "Conectado"), ("Disconnected", "Desconectado"), ("OptionalOffline", "Opcional • offline"),
        ("DriversCount", "{0} motorista(s)"), ("Now", "Recebendo agora"), ("SecondsAgo", "há {0} s"),
        ("SummaryWaitingOmsi", "Abra uma viagem no OMSI para iniciar as verificações operacionais."),
        ("SummaryLocalReady", "OMSI ativo. A navegação local está pronta; o multiplayer está desconectado."),
        ("SummaryConnectedWaiting", "OMSI e multiplayer ativos. Aguardando telemetria de outro motorista."),
        ("SummaryHealthy", "Sessão recebendo telemetria de {0} motorista(s) remoto(s)."),
        ("Note", "O plugin do OMSI é opcional para o fluxo normal de GPS/HUD e multiplayer direto. Ping, jitter e perda de pacotes só serão mostrados quando o NavBR tiver medição real ativa; esta tela não inventa valores de rede."));
    private static readonly IReadOnlyDictionary<string, string> Es = T(
        ("Title", "Salud de la sesión"), ("Subtitle", "Una vista simple de OMSI, multijugador y el bridge/plugin opcional."),
        ("Omsi", "OMSI"), ("Multiplayer", "Multijugador"), ("Plugin", "Bridge/plugin"), ("Remote", "Conductores remotos"), ("Freshness", "Telemetría remota"),
        ("Active", "Activo"), ("Waiting", "Esperando OMSI"), ("Connected", "Conectado"), ("Disconnected", "Desconectado"), ("OptionalOffline", "Opcional • offline"),
        ("DriversCount", "{0} conductor(es)"), ("Now", "Recibiendo ahora"), ("SecondsAgo", "hace {0} s"),
        ("SummaryWaitingOmsi", "Abre un viaje en OMSI para iniciar las comprobaciones operativas."),
        ("SummaryLocalReady", "OMSI está activo. La navegación local está lista; el multijugador está desconectado."),
        ("SummaryConnectedWaiting", "OMSI y multijugador activos. Esperando telemetría de otro conductor."),
        ("SummaryHealthy", "La sesión recibe telemetría de {0} conductor(es) remoto(s)."),
        ("Note", "El plugin de OMSI es opcional para GPS/HUD y multijugador directo. Ping, jitter y pérdida solo aparecerán cuando exista una medición real activa; esta pantalla no inventa valores de red."));
    private static readonly IReadOnlyDictionary<string, string> De = T(
        ("Title", "Sitzungsstatus"), ("Subtitle", "Eine einfache Übersicht über OMSI, Mehrspieler und die optionale Plugin-Bridge."),
        ("Omsi", "OMSI"), ("Multiplayer", "Mehrspieler"), ("Plugin", "Plugin-Bridge"), ("Remote", "Remote-Fahrer"), ("Freshness", "Remote-Telemetrie"),
        ("Active", "Aktiv"), ("Waiting", "Warte auf OMSI"), ("Connected", "Verbunden"), ("Disconnected", "Getrennt"), ("OptionalOffline", "Optional • offline"),
        ("DriversCount", "{0} Fahrer"), ("Now", "Empfang läuft"), ("SecondsAgo", "vor {0} s"),
        ("SummaryWaitingOmsi", "Eine Fahrt in OMSI öffnen, um die Betriebsprüfungen zu starten."),
        ("SummaryLocalReady", "OMSI ist aktiv. Lokale Navigation ist bereit; Mehrspieler ist offline."),
        ("SummaryConnectedWaiting", "OMSI und Mehrspieler sind aktiv. Warte auf Telemetrie eines anderen Fahrers."),
        ("SummaryHealthy", "Sitzung empfängt Telemetrie von {0} Remote-Fahrer(n)."),
        ("Note", "Das OMSI-Plugin ist für GPS/HUD und direkten Mehrspieler optional. Ping, Jitter und Paketverlust werden erst mit einer echten aktiven Messung angezeigt; diese Ansicht erfindet keine Netzwerkwerte."));
    private static readonly IReadOnlyDictionary<string, string> Fr = T(
        ("Title", "Santé de session"), ("Subtitle", "Une vue simple d’OMSI, du multijoueur et du bridge/plugin optionnel."),
        ("Omsi", "OMSI"), ("Multiplayer", "Multijoueur"), ("Plugin", "Bridge/plugin"), ("Remote", "Conducteurs distants"), ("Freshness", "Télémétrie distante"),
        ("Active", "Actif"), ("Waiting", "En attente d’OMSI"), ("Connected", "Connecté"), ("Disconnected", "Déconnecté"), ("OptionalOffline", "Optionnel • hors ligne"),
        ("DriversCount", "{0} conducteur(s)"), ("Now", "Réception en cours"), ("SecondsAgo", "il y a {0} s"),
        ("SummaryWaitingOmsi", "Ouvrez un service dans OMSI pour démarrer les vérifications opérationnelles."),
        ("SummaryLocalReady", "OMSI est actif. La navigation locale est prête; le multijoueur est déconnecté."),
        ("SummaryConnectedWaiting", "OMSI et multijoueur actifs. En attente de la télémétrie d’un autre conducteur."),
        ("SummaryHealthy", "La session reçoit la télémétrie de {0} conducteur(s) distant(s)."),
        ("Note", "Le plugin OMSI est optionnel pour le GPS/HUD et le multijoueur direct. Ping, jitter et perte de paquets n’apparaîtront qu’avec une mesure réelle active; cet écran n’invente aucune valeur réseau."));

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
