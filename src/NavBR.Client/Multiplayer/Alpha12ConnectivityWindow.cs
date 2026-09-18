using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Client.Operations;

namespace NavBR.Client.Multiplayer;

internal sealed class Alpha12ConnectivityWindow : Window
{
    private const int HostPort = 27730;
    private readonly TextBlock _summary = new();
    private readonly TextBlock _networkValue = new();
    private readonly TextBlock _firewallValue = new();
    private readonly TextBlock _portValue = new();
    private readonly TextBlock _roomValue = new();
    private readonly TextBlock _internetValue = new();
    private readonly Button _firewallButton = new();
    private bool _checking;

    public Alpha12ConnectivityWindow(Window owner)
    {
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 820d;
        Height = 650d;
        MinWidth = 720d;
        MinHeight = 570d;
        Background = Brush(6, 11, 16);
        Content = BuildContent();
        ApplyLocalization();
        Loaded += async (_, _) => await RefreshAsync();
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
            Text = Text("Title"),
            Foreground = Brushes.White,
            FontSize = 25d,
            FontWeight = FontWeights.Bold,
            Tag = "title"
        });
        heading.Children.Add(new TextBlock
        {
            Text = Text("Subtitle"),
            Foreground = Brush(142, 161, 174),
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 6d, 0d, 0d),
            Tag = "subtitle"
        });
        root.Children.Add(heading);

        _summary.Foreground = Brushes.White;
        _summary.FontSize = 14d;
        _summary.FontWeight = FontWeights.SemiBold;
        _summary.TextWrapping = TextWrapping.Wrap;
        var summaryCard = Card(_summary);
        Grid.SetRow(summaryCard, 1);
        root.Children.Add(summaryCard);

        var body = new StackPanel();
        var metrics = new WrapPanel();
        metrics.Children.Add(MetricCard("LocalNetwork", _networkValue));
        metrics.Children.Add(MetricCard("Firewall", _firewallValue));
        metrics.Children.Add(MetricCard("Port", _portValue));
        metrics.Children.Add(MetricCard("Room", _roomValue));
        metrics.Children.Add(MetricCard("Internet", _internetValue));
        body.Children.Add(metrics);

        StyleButton(_firewallButton);
        _firewallButton.Click += async (_, _) => await EnsureFirewallAsync();
        body.Children.Add(_firewallButton);

        body.Children.Add(Card(new TextBlock
        {
            Text = Text("InternetNote"),
            Foreground = Brush(141, 160, 173),
            FontSize = 10.5d,
            TextWrapping = TextWrapping.Wrap
        }, new Thickness(0d, 14d, 0d, 0d)));

        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0d, 18d, 0d, 0d),
            Content = body
        };
        Grid.SetRow(scroller, 2);
        root.Children.Add(scroller);
        return root;
    }

    private async Task RefreshAsync()
    {
        if (_checking)
        {
            return;
        }

        _checking = true;
        _firewallButton.IsEnabled = false;
        try
        {
            var addresses = GetPrivateIpv4Addresses();
            var firewallReady = await WindowsFirewallService.IsInboundRulePresentAsync(HostPort);
            var session = DispatcherSessionFeed.Snapshot();

            _networkValue.Text = addresses.Count == 0
                ? Text("NoNetwork")
                : string.Join(Environment.NewLine, addresses);
            _firewallValue.Text = firewallReady ? Text("Ready") : Text("NeedsAttention");
            _firewallValue.Foreground = firewallReady ? Brush(101, 224, 154) : Brush(255, 187, 91);
            _portValue.Text = $"TCP {HostPort}";
            _roomValue.Text = session.Connected
                ? string.IsNullOrWhiteSpace(session.RoomId) ? Text("Connected") : session.RoomId
                : Text("Offline");
            _internetValue.Text = Text("NotVerified");
            _summary.Text = BuildSummary(addresses.Count > 0, firewallReady, session.Connected);
            _firewallButton.Content = firewallReady ? Text("FirewallReadyButton") : Text("AllowFirewall");
            _firewallButton.IsEnabled = !firewallReady;
        }
        finally
        {
            _checking = false;
        }
    }

    private async Task EnsureFirewallAsync()
    {
        if (_checking)
        {
            return;
        }

        _checking = true;
        _firewallButton.IsEnabled = false;
        _firewallValue.Text = Text("Requesting");
        FirewallRuleApplyResult? result = null;
        try
        {
            result = await WindowsFirewallService.EnsureInboundRuleDetailedAsync(HostPort);
            _firewallValue.Text = result.Success
                ? Text("Ready")
                : result.Cancelled
                    ? Text("Cancelled")
                    : Text("Failed");
            _firewallValue.Foreground = result.Success
                ? Brush(101, 224, 154)
                : Brush(255, 112, 112);

            if (!result.Success)
            {
                _summary.Text = result.Cancelled
                    ? Text("FirewallCancelled")
                    : $"{Text("FirewallFailedDetail")} {result.ErrorMessage}".Trim();
            }
        }
        finally
        {
            _checking = false;
            if (result?.Success == true)
            {
                await RefreshAsync();
            }
            else
            {
                _firewallButton.IsEnabled = true;
            }
        }
    }

    private static IReadOnlyList<string> GetPrivateIpv4Addresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                                  network.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                  network.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                .Select(address => address.Address)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork && IsPrivateIpv4(address))
                .Select(address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 &&
               (bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168));
    }

    private static string BuildSummary(bool localNetwork, bool firewallReady, bool connected)
    {
        if (!localNetwork)
        {
            return Text("SummaryNoNetwork");
        }
        if (!firewallReady)
        {
            return Text("SummaryFirewall");
        }
        return connected ? Text("SummaryConnected") : Text("SummaryLanReady");
    }

    private void ApplyLocalization()
    {
        Title = Text("Title");
        foreach (var textBlock in Enumerate<TextBlock>(this))
        {
            if (textBlock.Tag as string == "title")
            {
                textBlock.Text = Text("Title");
            }
            else if (textBlock.Tag as string == "subtitle")
            {
                textBlock.Text = Text("Subtitle");
            }
            else if (textBlock.Tag is string tag && tag.StartsWith("metric:", StringComparison.Ordinal))
            {
                textBlock.Text = Text(tag["metric:".Length..]);
            }
        }
    }

    private static Border MetricCard(string key, TextBlock value)
    {
        var label = new TextBlock
        {
            Text = Text(key),
            Tag = "metric:" + key,
            Foreground = Brush(127, 148, 162),
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        };
        value.Foreground = Brushes.White;
        value.FontSize = 14d;
        value.FontWeight = FontWeights.SemiBold;
        value.TextWrapping = TextWrapping.Wrap;
        value.Margin = new Thickness(0d, 6d, 0d, 0d);

        var stack = new StackPanel();
        stack.Children.Add(label);
        stack.Children.Add(value);
        return new Border
        {
            Width = 230d,
            MinHeight = 105d,
            Margin = new Thickness(0d, 0d, 10d, 10d),
            Padding = new Thickness(14d),
            Background = Brush(10, 19, 25),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Child = stack
        };
    }

    private static Border Card(UIElement child, Thickness? margin = null) => new()
    {
        Margin = margin ?? new Thickness(0d),
        Padding = new Thickness(16d),
        Background = Brush(10, 19, 25),
        BorderBrush = Brush(31, 47, 57),
        BorderThickness = new Thickness(1d),
        CornerRadius = new CornerRadius(12d),
        Child = child
    };

    private static void StyleButton(Button button)
    {
        button.Height = 40d;
        button.MinWidth = 220d;
        button.Padding = new Thickness(16d, 8d, 16d, 8d);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0d, 4d, 0d, 0d);
        button.Background = Brush(14, 27, 35);
        button.Foreground = Brush(218, 230, 238);
        button.BorderBrush = Brush(44, 65, 78);
        button.BorderThickness = new Thickness(1d);
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static IEnumerable<T> Enumerate<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Enumerate<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    internal static string ButtonText() => LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
    {
        "pt" => "Ver conectividade",
        "es" => "Ver conectividad",
        "de" => "Verbindung prüfen",
        "fr" => "Voir la connectivité",
        _ => "View connectivity"
    };

    private static string Text(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        return (language, key) switch
        {
            ("pt", "Title") => "Conectividade multiplayer",
            ("pt", "Subtitle") => "Verificação simples da rede local e dos requisitos para hospedar uma sala NavBR.",
            ("pt", "LocalNetwork") => "REDE LOCAL",
            ("pt", "Firewall") => "FIREWALL DO WINDOWS",
            ("pt", "Port") => "PORTA DO NAVBR",
            ("pt", "Room") => "SALA ATUAL",
            ("pt", "Internet") => "ACESSO PELA INTERNET",
            ("pt", "NoNetwork") => "Nenhum IPv4 privado detectado",
            ("pt", "Ready") => "Pronto",
            ("pt", "NeedsAttention") => "Precisa permitir",
            ("pt", "Connected") => "Conectado",
            ("pt", "Offline") => "Sem sessão",
            ("pt", "NotVerified") => "Ainda não verificado",
            ("pt", "Requesting") => "Solicitando permissão…",
            ("pt", "Failed") => "Não confirmado",
            ("pt", "Cancelled") => "Cancelado",
            ("pt", "FirewallCancelled") => "A solicitação de administrador foi cancelada. O Firewall não foi alterado.",
            ("pt", "FirewallFailedDetail") => "O Windows não confirmou a criação da regra TCP 27730.",
            ("pt", "AllowFirewall") => "Permitir TCP 27730 no Firewall",
            ("pt", "FirewallReadyButton") => "Firewall pronto",
            ("pt", "SummaryNoNetwork") => "O NavBR não encontrou uma rede local IPv4 privada ativa neste momento.",
            ("pt", "SummaryFirewall") => "Rede local detectada. Falta permitir a porta do NavBR no Windows Firewall.",
            ("pt", "SummaryConnected") => "A sessão está conectada e os requisitos locais de hospedagem estão prontos.",
            ("pt", "SummaryLanReady") => "Este PC está pronto para testes na rede local. Para Internet, ainda será necessário confirmar roteador/NAT.",
            ("pt", "InternetNote") => "Esta tela não afirma que sua porta está acessível pela Internet. Encaminhamento no roteador, CGNAT e NAT podem impedir conexões externas. A Alpha.12 terá diagnóstico externo e UPnP/NAT traversal em etapas posteriores.",

            ("es", "Title") => "Conectividad multijugador",
            ("es", "Subtitle") => "Comprobación simple de red local y requisitos para alojar una sala NavBR.",
            ("es", "LocalNetwork") => "RED LOCAL",
            ("es", "Firewall") => "FIREWALL DE WINDOWS",
            ("es", "Port") => "PUERTO NAVBR",
            ("es", "Room") => "SALA ACTUAL",
            ("es", "Internet") => "ACCESO A INTERNET",
            ("es", "NoNetwork") => "Ningún IPv4 privado detectado",
            ("es", "Ready") => "Listo",
            ("es", "NeedsAttention") => "Requiere permiso",
            ("es", "Connected") => "Conectado",
            ("es", "Offline") => "Sin sesión",
            ("es", "NotVerified") => "Aún no verificado",
            ("es", "Requesting") => "Solicitando permiso…",
            ("es", "Failed") => "No confirmado",
            ("es", "Cancelled") => "Cancelado",
            ("es", "FirewallCancelled") => "Se canceló la solicitud de administrador. El Firewall no fue modificado.",
            ("es", "FirewallFailedDetail") => "Windows no confirmó la creación de la regla TCP 27730.",
            ("es", "AllowFirewall") => "Permitir TCP 27730 en Firewall",
            ("es", "FirewallReadyButton") => "Firewall listo",
            ("es", "SummaryNoNetwork") => "NavBR no encontró una red IPv4 privada activa.",
            ("es", "SummaryFirewall") => "Red local detectada. Falta permitir el puerto de NavBR en Windows Firewall.",
            ("es", "SummaryConnected") => "La sesión está conectada y los requisitos locales están listos.",
            ("es", "SummaryLanReady") => "Este PC está listo para pruebas en red local; Internet aún requiere verificar router/NAT.",
            ("es", "InternetNote") => "Esta pantalla no afirma que el puerto sea accesible desde Internet. Router, CGNAT y NAT pueden impedir conexiones externas; el diagnóstico externo y UPnP/NAT llegarán por etapas.",

            ("de", "Title") => "Mehrspieler-Verbindung",
            ("de", "Subtitle") => "Einfache Prüfung des lokalen Netzwerks und der Voraussetzungen zum Hosten eines NavBR-Raums.",
            ("de", "LocalNetwork") => "LOKALES NETZ",
            ("de", "Firewall") => "WINDOWS-FIREWALL",
            ("de", "Port") => "NAVBR-PORT",
            ("de", "Room") => "AKTUELLER RAUM",
            ("de", "Internet") => "INTERNETZUGRIFF",
            ("de", "NoNetwork") => "Keine private IPv4-Adresse erkannt",
            ("de", "Ready") => "Bereit",
            ("de", "NeedsAttention") => "Freigabe nötig",
            ("de", "Connected") => "Verbunden",
            ("de", "Offline") => "Keine Sitzung",
            ("de", "NotVerified") => "Noch nicht geprüft",
            ("de", "Requesting") => "Berechtigung wird angefordert…",
            ("de", "Failed") => "Nicht bestätigt",
            ("de", "Cancelled") => "Abgebrochen",
            ("de", "FirewallCancelled") => "Die Administratoranforderung wurde abgebrochen. Die Firewall wurde nicht geändert.",
            ("de", "FirewallFailedDetail") => "Windows hat die TCP-27730-Regel nicht bestätigt.",
            ("de", "AllowFirewall") => "TCP 27730 in Firewall erlauben",
            ("de", "FirewallReadyButton") => "Firewall bereit",
            ("de", "SummaryNoNetwork") => "NavBR hat derzeit kein aktives privates IPv4-Netzwerk gefunden.",
            ("de", "SummaryFirewall") => "Lokales Netzwerk erkannt. Der NavBR-Port muss noch in der Windows-Firewall erlaubt werden.",
            ("de", "SummaryConnected") => "Die Sitzung ist verbunden und die lokalen Hosting-Voraussetzungen sind bereit.",
            ("de", "SummaryLanReady") => "Dieser PC ist für Tests im lokalen Netz bereit; Internetzugriff muss noch über Router/NAT geprüft werden.",
            ("de", "InternetNote") => "Diese Ansicht behauptet nicht, dass der Port aus dem Internet erreichbar ist. Router, CGNAT und NAT können externe Verbindungen blockieren; externe Diagnose und UPnP/NAT folgen später.",

            ("fr", "Title") => "Connectivité multijoueur",
            ("fr", "Subtitle") => "Vérification simple du réseau local et des prérequis pour héberger une salle NavBR.",
            ("fr", "LocalNetwork") => "RÉSEAU LOCAL",
            ("fr", "Firewall") => "PARE-FEU WINDOWS",
            ("fr", "Port") => "PORT NAVBR",
            ("fr", "Room") => "SALLE ACTUELLE",
            ("fr", "Internet") => "ACCÈS INTERNET",
            ("fr", "NoNetwork") => "Aucune IPv4 privée détectée",
            ("fr", "Ready") => "Prêt",
            ("fr", "NeedsAttention") => "Autorisation requise",
            ("fr", "Connected") => "Connecté",
            ("fr", "Offline") => "Aucune session",
            ("fr", "NotVerified") => "Pas encore vérifié",
            ("fr", "Requesting") => "Demande d’autorisation…",
            ("fr", "Failed") => "Non confirmé",
            ("fr", "Cancelled") => "Annulé",
            ("fr", "FirewallCancelled") => "La demande administrateur a été annulée. Le pare-feu n’a pas été modifié.",
            ("fr", "FirewallFailedDetail") => "Windows n’a pas confirmé la création de la règle TCP 27730.",
            ("fr", "AllowFirewall") => "Autoriser TCP 27730 dans le pare-feu",
            ("fr", "FirewallReadyButton") => "Pare-feu prêt",
            ("fr", "SummaryNoNetwork") => "NavBR n’a détecté aucun réseau IPv4 privé actif.",
            ("fr", "SummaryFirewall") => "Réseau local détecté. Le port NavBR doit encore être autorisé dans le pare-feu Windows.",
            ("fr", "SummaryConnected") => "La session est connectée et les prérequis locaux sont prêts.",
            ("fr", "SummaryLanReady") => "Ce PC est prêt pour les tests en réseau local; Internet nécessite encore une vérification routeur/NAT.",
            ("fr", "InternetNote") => "Cette vue n’affirme pas que le port est accessible depuis Internet. Routeur, CGNAT et NAT peuvent bloquer les connexions externes; diagnostic externe et UPnP/NAT seront ajoutés par étapes.",

            (_, "Title") => "Multiplayer connectivity",
            (_, "Subtitle") => "Simple local-network and hosting-requirement checks for a NavBR room.",
            (_, "LocalNetwork") => "LOCAL NETWORK",
            (_, "Firewall") => "WINDOWS FIREWALL",
            (_, "Port") => "NAVBR PORT",
            (_, "Room") => "CURRENT ROOM",
            (_, "Internet") => "INTERNET ACCESS",
            (_, "NoNetwork") => "No private IPv4 detected",
            (_, "Ready") => "Ready",
            (_, "NeedsAttention") => "Permission needed",
            (_, "Connected") => "Connected",
            (_, "Offline") => "No session",
            (_, "NotVerified") => "Not verified yet",
            (_, "Requesting") => "Requesting permission…",
            (_, "Failed") => "Not confirmed",
            (_, "Cancelled") => "Cancelled",
            (_, "FirewallCancelled") => "Administrator permission was cancelled. The firewall was not changed.",
            (_, "FirewallFailedDetail") => "Windows did not confirm creation of the TCP 27730 rule.",
            (_, "AllowFirewall") => "Allow TCP 27730 in Firewall",
            (_, "FirewallReadyButton") => "Firewall ready",
            (_, "SummaryNoNetwork") => "NavBR did not find an active private IPv4 network right now.",
            (_, "SummaryFirewall") => "Local network detected. The NavBR port still needs to be allowed in Windows Firewall.",
            (_, "SummaryConnected") => "The session is connected and local hosting requirements are ready.",
            (_, "SummaryLanReady") => "This PC is ready for local-network tests; Internet access still requires router/NAT verification.",
            (_, "InternetNote") => "This screen does not claim that your port is reachable from the Internet. Router forwarding, CGNAT and NAT can block external connections. External diagnostics and UPnP/NAT traversal will be added in later Alpha.12 stages.",
            _ => key
        };
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
