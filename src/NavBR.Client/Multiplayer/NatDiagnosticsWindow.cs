using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;

namespace NavBR.Client.Multiplayer;

internal sealed class NatDiagnosticsWindow : Window
{
    private readonly NatDiagnosticsService _service = new();
    private readonly TextBlock _summary = Value(16d);
    private readonly TextBlock _localAddresses = Value();
    private readonly TextBlock _listener = Value();
    private readonly TextBlock _firewall = Value();
    private readonly TextBlock _upnp = Value();
    private readonly TextBlock _wan = Value();
    private readonly TextBlock _nat = Value();
    private readonly TextBlock _externalTest = Value();
    private readonly TextBlock _technical = Value(10.5d);
    private readonly Button _refresh = new();
    private bool _busy;

    public NatDiagnosticsWindow(Window owner)
    {
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 860d;
        Height = 720d;
        MinWidth = 760d;
        MinHeight = 620d;
        Background = Brush(6, 11, 16);
        Title = Text("Title");
        Content = BuildContent();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private UIElement BuildContent()
    {
        var root = new StackPanel { Margin = new Thickness(28d) };
        root.Children.Add(new TextBlock
        {
            Text = Text("Title"),
            Foreground = Brushes.White,
            FontSize = 25d,
            FontWeight = FontWeights.Bold
        });
        root.Children.Add(new TextBlock
        {
            Text = Text("Subtitle"),
            Foreground = Brush(142, 161, 174),
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 6d, 0d, 16d)
        });

        _summary.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(Card(_summary, new Thickness(0d, 0d, 0d, 14d)));

        var metrics = new WrapPanel();
        metrics.Children.Add(Metric("LocalAddresses", _localAddresses));
        metrics.Children.Add(Metric("Listener", _listener));
        metrics.Children.Add(Metric("Firewall", _firewall));
        metrics.Children.Add(Metric("Upnp", _upnp));
        metrics.Children.Add(Metric("Wan", _wan));
        metrics.Children.Add(Metric("Nat", _nat));
        metrics.Children.Add(Metric("ExternalTest", _externalTest));
        root.Children.Add(metrics);

        _technical.Foreground = Brush(145, 163, 176);
        _technical.TextWrapping = TextWrapping.Wrap;
        root.Children.Add(Card(_technical, new Thickness(0d, 4d, 0d, 14d)));

        _refresh.Content = Text("Refresh");
        _refresh.Height = 40d;
        _refresh.MinWidth = 180d;
        _refresh.HorizontalAlignment = HorizontalAlignment.Left;
        _refresh.Padding = new Thickness(16d, 8d, 16d, 8d);
        _refresh.Background = Brush(14, 27, 35);
        _refresh.Foreground = Brush(218, 230, 238);
        _refresh.BorderBrush = Brush(44, 65, 78);
        _refresh.BorderThickness = new Thickness(1d);
        _refresh.Cursor = System.Windows.Input.Cursors.Hand;
        _refresh.Click += async (_, _) => await RefreshAsync();
        root.Children.Add(_refresh);

        return new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private async Task RefreshAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _refresh.IsEnabled = false;
        _summary.Text = Text("Checking");
        try
        {
            var snapshot = await _service.InspectAsync();
            _localAddresses.Text = snapshot.LocalIpv4Addresses.Count == 0
                ? Text("Unavailable")
                : string.Join(Environment.NewLine, snapshot.LocalIpv4Addresses);
            _listener.Text = snapshot.LocalPortListening ? Text("Listening") : Text("NotListening");
            _listener.Foreground = snapshot.LocalPortListening ? Good() : Neutral();
            _firewall.Text = snapshot.FirewallRulePresent ? Text("Ready") : Text("NeedsAttention");
            _firewall.Foreground = snapshot.FirewallRulePresent ? Good() : Warning();
            _upnp.Text = snapshot.UpnpGatewayFound
                ? snapshot.AutomaticUpnpEnabled ? Text("GatewayAndEnabled") : Text("GatewayDisabled")
                : Text("GatewayNotFound");
            _upnp.Foreground = snapshot.UpnpGatewayFound ? Good() : Neutral();
            _wan.Text = string.IsNullOrWhiteSpace(snapshot.GatewayExternalAddress)
                ? Text("Unavailable")
                : snapshot.GatewayExternalAddress;
            _nat.Text = NatLabel(snapshot.EnvironmentKind);
            _nat.Foreground = NatBrush(snapshot.EnvironmentKind);
            _externalTest.Text = Text("NotExternallyVerified");
            _externalTest.Foreground = Warning();
            _technical.Text = LocalizeTechnical(snapshot);
            _summary.Text = Summary(snapshot);
            _summary.Foreground = snapshot.EnvironmentKind switch
            {
                NatEnvironmentKind.PublicWan when snapshot.FirewallRulePresent => Good(),
                NatEnvironmentKind.CarrierGradeNat or NatEnvironmentKind.PrivateWan => Warning(),
                _ => Brushes.White
            };
        }
        catch (Exception ex)
        {
            _summary.Text = string.Format(Text("Failed"), ex.Message);
            _summary.Foreground = Brush(255, 112, 112);
        }
        finally
        {
            _busy = false;
            _refresh.IsEnabled = true;
        }
    }

    private static string Summary(NatDiagnosticsSnapshot snapshot)
    {
        return snapshot.EnvironmentKind switch
        {
            NatEnvironmentKind.PublicWan => snapshot.LocalPortListening
                ? Text("SummaryPublicListening")
                : Text("SummaryPublicIdle"),
            NatEnvironmentKind.CarrierGradeNat => Text("SummaryCgnat"),
            NatEnvironmentKind.PrivateWan => Text("SummaryDoubleNat"),
            NatEnvironmentKind.ReservedWan => Text("SummaryReserved"),
            _ => snapshot.UpnpGatewayFound ? Text("SummaryUnknown") : Text("SummaryNoGateway")
        };
    }

    private static string LocalizeTechnical(NatDiagnosticsSnapshot snapshot)
    {
        var gateway = string.IsNullOrWhiteSpace(snapshot.GatewayLocalAddress) ? "—" : snapshot.GatewayLocalAddress;
        return Text("TechnicalPrefix") + Environment.NewLine +
               string.Format(Text("GatewayLocal"), gateway) + Environment.NewLine +
               string.Format(Text("PortState"), NatDiagnosticsService.HostPort, snapshot.LocalPortListening ? Text("Listening") : Text("NotListening")) + Environment.NewLine +
               Text("ExternalLimit");
    }

    private static string NatLabel(NatEnvironmentKind kind) => kind switch
    {
        NatEnvironmentKind.PublicWan => Text("NatPublic"),
        NatEnvironmentKind.CarrierGradeNat => Text("NatCgnat"),
        NatEnvironmentKind.PrivateWan => Text("NatPrivate"),
        NatEnvironmentKind.ReservedWan => Text("NatReserved"),
        _ => Text("NatUnknown")
    };

    private static Brush NatBrush(NatEnvironmentKind kind) => kind switch
    {
        NatEnvironmentKind.PublicWan => Good(),
        NatEnvironmentKind.CarrierGradeNat or NatEnvironmentKind.PrivateWan => Warning(),
        _ => Neutral()
    };

    private static Border Metric(string key, TextBlock value)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = Text(key),
            Foreground = Brush(116, 137, 151),
            FontSize = 8.8d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(value);
        return new Border
        {
            Width = 245d,
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

    private static TextBlock Value(double fontSize = 13d) => new()
    {
        Foreground = Brushes.White,
        FontSize = fontSize,
        FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0d, 6d, 0d, 0d)
    };

    private static Border Card(UIElement child, Thickness margin) => new()
    {
        Margin = margin,
        Padding = new Thickness(16d),
        Background = Brush(10, 19, 25),
        BorderBrush = Brush(31, 47, 57),
        BorderThickness = new Thickness(1d),
        CornerRadius = new CornerRadius(12d),
        Child = child
    };

    internal static string ButtonText() => LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
    {
        "pt" => "Diagnóstico NAT/Internet",
        "es" => "Diagnóstico NAT/Internet",
        "de" => "NAT/Internet-Diagnose",
        "fr" => "Diagnostic NAT/Internet",
        _ => "NAT/Internet diagnostics"
    };

    private static string Text(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        return (language, key) switch
        {
            ("pt", "Title") => "Diagnóstico NAT e Internet",
            ("pt", "Subtitle") => "Leitura segura do ambiente de rede. Esta tela identifica sinais de CGNAT/double NAT sem afirmar que a porta está aberta externamente.",
            ("pt", "LocalAddresses") => "IPv4 LOCAL",
            ("pt", "Listener") => "TCP 27730 LOCAL",
            ("pt", "Firewall") => "FIREWALL",
            ("pt", "Upnp") => "ROTEADOR / UPnP",
            ("pt", "Wan") => "IPv4 WAN DO GATEWAY",
            ("pt", "Nat") => "AMBIENTE NAT",
            ("pt", "ExternalTest") => "TESTE EXTERNO DA PORTA",
            ("pt", "Unavailable") => "Não disponível",
            ("pt", "Listening") => "Escutando",
            ("pt", "NotListening") => "Sem host ativo",
            ("pt", "Ready") => "Pronto",
            ("pt", "NeedsAttention") => "Precisa permitir",
            ("pt", "GatewayAndEnabled") => "Gateway encontrado • automático ativado",
            ("pt", "GatewayDisabled") => "Gateway encontrado • automático desativado",
            ("pt", "GatewayNotFound") => "Gateway UPnP não encontrado",
            ("pt", "NotExternallyVerified") => "Ainda não verificado de fora",
            ("pt", "NatPublic") => "IPv4 público no gateway",
            ("pt", "NatCgnat") => "CGNAT provável (100.64/10)",
            ("pt", "NatPrivate") => "Double NAT / WAN privada provável",
            ("pt", "NatReserved") => "WAN não pública/reservada",
            ("pt", "NatUnknown") => "Não determinado",
            ("pt", "SummaryPublicListening") => "O gateway reporta IPv4 público e o NavBR está escutando localmente. Ainda falta uma verificação feita por um servidor externo para confirmar o acesso pela Internet.",
            ("pt", "SummaryPublicIdle") => "O gateway reporta IPv4 público. Crie uma sala para iniciar TCP 27730; depois ainda será necessário o teste externo de alcance.",
            ("pt", "SummaryCgnat") => "Há forte sinal de CGNAT. Encaminhamento local/UPnP normalmente não atravessa o NAT da operadora; um relay/fallback ou suporte do provedor pode ser necessário.",
            ("pt", "SummaryDoubleNat") => "O gateway recebeu um IPv4 privado na WAN. Há provável double NAT ou outro roteador acima dele.",
            ("pt", "SummaryReserved") => "O endereço WAN informado não é um IPv4 público utilizável. Acesso direto externo não pode ser presumido.",
            ("pt", "SummaryUnknown") => "O roteador foi encontrado, mas não forneceu um IPv4 WAN utilizável para classificar o NAT.",
            ("pt", "SummaryNoGateway") => "Nenhum gateway UPnP foi encontrado. Isso não significa que a Internet está sem funcionar; apenas impede este diagnóstico automático do roteador.",
            ("pt", "Checking") => "Analisando rede e roteador…",
            ("pt", "Refresh") => "Verificar novamente",
            ("pt", "Failed") => "Não foi possível concluir o diagnóstico: {0}",
            ("pt", "TechnicalPrefix") => "Detalhes técnicos",
            ("pt", "GatewayLocal") => "Endereço local usado para alcançar o gateway: {0}",
            ("pt", "PortState") => "TCP {0} neste PC: {1}",
            ("pt", "ExternalLimit") => "Limite atual: o NavBR ainda não possui um servidor público de callback para tentar conectar de fora até sua porta. Por isso esta tela não apresenta falso positivo de 'porta aberta'.",

            ("es", "Title") => "Diagnóstico NAT e Internet",
            ("es", "Subtitle") => "Lectura segura del entorno de red. Detecta señales de CGNAT/doble NAT sin afirmar que el puerto esté abierto externamente.",
            ("es", "LocalAddresses") => "IPv4 LOCAL",
            ("es", "Listener") => "TCP 27730 LOCAL",
            ("es", "Firewall") => "FIREWALL",
            ("es", "Upnp") => "ROUTER / UPnP",
            ("es", "Wan") => "IPv4 WAN DEL GATEWAY",
            ("es", "Nat") => "ENTORNO NAT",
            ("es", "ExternalTest") => "PRUEBA EXTERNA DEL PUERTO",
            ("es", "Unavailable") => "No disponible",
            ("es", "Listening") => "Escuchando",
            ("es", "NotListening") => "Sin host activo",
            ("es", "Ready") => "Listo",
            ("es", "NeedsAttention") => "Requiere permiso",
            ("es", "GatewayAndEnabled") => "Gateway encontrado • automático activado",
            ("es", "GatewayDisabled") => "Gateway encontrado • automático desactivado",
            ("es", "GatewayNotFound") => "Gateway UPnP no encontrado",
            ("es", "NotExternallyVerified") => "Aún no verificado desde fuera",
            ("es", "NatPublic") => "IPv4 público en el gateway",
            ("es", "NatCgnat") => "CGNAT probable (100.64/10)",
            ("es", "NatPrivate") => "Doble NAT / WAN privada probable",
            ("es", "NatReserved") => "WAN no pública/reservada",
            ("es", "NatUnknown") => "No determinado",
            ("es", "SummaryPublicListening") => "El gateway informa IPv4 público y NavBR escucha localmente. Aún falta una comprobación desde un servidor externo.",
            ("es", "SummaryPublicIdle") => "El gateway informa IPv4 público. Crea una sala para iniciar TCP 27730; después faltará la prueba externa.",
            ("es", "SummaryCgnat") => "Hay una señal fuerte de CGNAT. UPnP local normalmente no atraviesa el NAT del operador; puede ser necesario relay/fallback o soporte del ISP.",
            ("es", "SummaryDoubleNat") => "El gateway recibió IPv4 privado en la WAN. Es probable que exista doble NAT u otro router superior.",
            ("es", "SummaryReserved") => "La WAN informada no es un IPv4 público utilizable.",
            ("es", "SummaryUnknown") => "El router fue encontrado, pero no entregó un IPv4 WAN utilizable.",
            ("es", "SummaryNoGateway") => "No se encontró gateway UPnP. Esto no implica que Internet esté caído.",
            ("es", "Checking") => "Analizando red y router…",
            ("es", "Refresh") => "Comprobar de nuevo",
            ("es", "Failed") => "No se pudo completar el diagnóstico: {0}",
            ("es", "TechnicalPrefix") => "Detalles técnicos",
            ("es", "GatewayLocal") => "Dirección local usada para llegar al gateway: {0}",
            ("es", "PortState") => "TCP {0} en este PC: {1}",
            ("es", "ExternalLimit") => "Límite actual: NavBR aún no dispone de un servidor público de callback para intentar conectarse desde fuera. Por eso no muestra un falso positivo de puerto abierto.",

            ("de", "Title") => "NAT- und Internet-Diagnose",
            ("de", "Subtitle") => "Sichere Netzwerkanalyse. Erkennt Hinweise auf CGNAT/Doppel-NAT, ohne externe Port-Erreichbarkeit vorzutäuschen.",
            ("de", "LocalAddresses") => "LOKALE IPv4",
            ("de", "Listener") => "LOKALES TCP 27730",
            ("de", "Firewall") => "FIREWALL",
            ("de", "Upnp") => "ROUTER / UPnP",
            ("de", "Wan") => "WAN-IPv4 DES GATEWAYS",
            ("de", "Nat") => "NAT-UMGEBUNG",
            ("de", "ExternalTest") => "EXTERNER PORTTEST",
            ("de", "Unavailable") => "Nicht verfügbar",
            ("de", "Listening") => "Lauscht",
            ("de", "NotListening") => "Kein Host aktiv",
            ("de", "Ready") => "Bereit",
            ("de", "NeedsAttention") => "Freigabe nötig",
            ("de", "GatewayAndEnabled") => "Gateway gefunden • Automatik aktiv",
            ("de", "GatewayDisabled") => "Gateway gefunden • Automatik aus",
            ("de", "GatewayNotFound") => "UPnP-Gateway nicht gefunden",
            ("de", "NotExternallyVerified") => "Von außen noch nicht geprüft",
            ("de", "NatPublic") => "Öffentliche IPv4 am Gateway",
            ("de", "NatCgnat") => "CGNAT wahrscheinlich (100.64/10)",
            ("de", "NatPrivate") => "Doppel-NAT / private WAN wahrscheinlich",
            ("de", "NatReserved") => "Nicht öffentliche/reservierte WAN",
            ("de", "NatUnknown") => "Nicht bestimmt",
            ("de", "SummaryPublicListening") => "Das Gateway meldet eine öffentliche IPv4 und NavBR lauscht lokal. Für die Bestätigung fehlt noch ein externer Callback-Test.",
            ("de", "SummaryPublicIdle") => "Das Gateway meldet eine öffentliche IPv4. Erstelle einen Raum, um TCP 27730 zu starten; danach fehlt noch der externe Test.",
            ("de", "SummaryCgnat") => "Starker Hinweis auf CGNAT. Lokales UPnP durchquert den Provider-NAT üblicherweise nicht; Relay/Fallback oder ISP-Unterstützung kann nötig sein.",
            ("de", "SummaryDoubleNat") => "Das Gateway hat eine private WAN-IPv4. Doppel-NAT oder ein vorgeschalteter Router ist wahrscheinlich.",
            ("de", "SummaryReserved") => "Die gemeldete WAN-Adresse ist keine nutzbare öffentliche IPv4.",
            ("de", "SummaryUnknown") => "Der Router wurde gefunden, lieferte aber keine nutzbare WAN-IPv4.",
            ("de", "SummaryNoGateway") => "Kein UPnP-Gateway gefunden. Das bedeutet nicht, dass die Internetverbindung ausgefallen ist.",
            ("de", "Checking") => "Netzwerk und Router werden analysiert…",
            ("de", "Refresh") => "Erneut prüfen",
            ("de", "Failed") => "Diagnose konnte nicht abgeschlossen werden: {0}",
            ("de", "TechnicalPrefix") => "Technische Details",
            ("de", "GatewayLocal") => "Lokale Adresse zum Gateway: {0}",
            ("de", "PortState") => "TCP {0} auf diesem PC: {1}",
            ("de", "ExternalLimit") => "Aktuelle Grenze: NavBR hat noch keinen öffentlichen Callback-Server, der von außen zu deinem Port verbindet. Deshalb wird kein falsches 'Port offen' angezeigt.",

            ("fr", "Title") => "Diagnostic NAT et Internet",
            ("fr", "Subtitle") => "Analyse réseau sûre. Détecte les signes de CGNAT/double NAT sans prétendre que le port est accessible de l’extérieur.",
            ("fr", "LocalAddresses") => "IPv4 LOCALE",
            ("fr", "Listener") => "TCP 27730 LOCAL",
            ("fr", "Firewall") => "PARE-FEU",
            ("fr", "Upnp") => "ROUTEUR / UPnP",
            ("fr", "Wan") => "IPv4 WAN DE LA PASSERELLE",
            ("fr", "Nat") => "ENVIRONNEMENT NAT",
            ("fr", "ExternalTest") => "TEST EXTERNE DU PORT",
            ("fr", "Unavailable") => "Indisponible",
            ("fr", "Listening") => "En écoute",
            ("fr", "NotListening") => "Aucun hôte actif",
            ("fr", "Ready") => "Prêt",
            ("fr", "NeedsAttention") => "Autorisation requise",
            ("fr", "GatewayAndEnabled") => "Passerelle trouvée • automatique activé",
            ("fr", "GatewayDisabled") => "Passerelle trouvée • automatique désactivé",
            ("fr", "GatewayNotFound") => "Passerelle UPnP introuvable",
            ("fr", "NotExternallyVerified") => "Pas encore vérifié depuis l’extérieur",
            ("fr", "NatPublic") => "IPv4 publique sur la passerelle",
            ("fr", "NatCgnat") => "CGNAT probable (100.64/10)",
            ("fr", "NatPrivate") => "Double NAT / WAN privée probable",
            ("fr", "NatReserved") => "WAN non publique/réservée",
            ("fr", "NatUnknown") => "Non déterminé",
            ("fr", "SummaryPublicListening") => "La passerelle signale une IPv4 publique et NavBR écoute localement. Un test par serveur externe reste nécessaire.",
            ("fr", "SummaryPublicIdle") => "La passerelle signale une IPv4 publique. Créez une salle pour démarrer TCP 27730, puis effectuez un test externe.",
            ("fr", "SummaryCgnat") => "Fort indice de CGNAT. L’UPnP local ne traverse généralement pas le NAT de l’opérateur; un relay/fallback ou l’aide du FAI peut être nécessaire.",
            ("fr", "SummaryDoubleNat") => "La passerelle a reçu une IPv4 WAN privée. Un double NAT ou un routeur amont est probable.",
            ("fr", "SummaryReserved") => "L’adresse WAN signalée n’est pas une IPv4 publique utilisable.",
            ("fr", "SummaryUnknown") => "Le routeur a été trouvé mais n’a pas fourni d’IPv4 WAN exploitable.",
            ("fr", "SummaryNoGateway") => "Aucune passerelle UPnP trouvée. Cela ne signifie pas que la connexion Internet est en panne.",
            ("fr", "Checking") => "Analyse du réseau et du routeur…",
            ("fr", "Refresh") => "Vérifier à nouveau",
            ("fr", "Failed") => "Diagnostic impossible : {0}",
            ("fr", "TechnicalPrefix") => "Détails techniques",
            ("fr", "GatewayLocal") => "Adresse locale utilisée vers la passerelle : {0}",
            ("fr", "PortState") => "TCP {0} sur ce PC : {1}",
            ("fr", "ExternalLimit") => "Limite actuelle : NavBR ne possède pas encore de serveur public de callback capable de se connecter depuis l’extérieur. Aucun faux positif 'port ouvert' n’est donc affiché.",

            (_, "Title") => "NAT and Internet diagnostics",
            (_, "Subtitle") => "Safe network inspection. Detects CGNAT/double-NAT signals without claiming that the port is externally reachable.",
            (_, "LocalAddresses") => "LOCAL IPv4",
            (_, "Listener") => "LOCAL TCP 27730",
            (_, "Firewall") => "FIREWALL",
            (_, "Upnp") => "ROUTER / UPnP",
            (_, "Wan") => "GATEWAY WAN IPv4",
            (_, "Nat") => "NAT ENVIRONMENT",
            (_, "ExternalTest") => "EXTERNAL PORT TEST",
            (_, "Unavailable") => "Unavailable",
            (_, "Listening") => "Listening",
            (_, "NotListening") => "No active host",
            (_, "Ready") => "Ready",
            (_, "NeedsAttention") => "Needs permission",
            (_, "GatewayAndEnabled") => "Gateway found • automatic enabled",
            (_, "GatewayDisabled") => "Gateway found • automatic disabled",
            (_, "GatewayNotFound") => "UPnP gateway not found",
            (_, "NotExternallyVerified") => "Not yet verified from outside",
            (_, "NatPublic") => "Public IPv4 on gateway",
            (_, "NatCgnat") => "Likely CGNAT (100.64/10)",
            (_, "NatPrivate") => "Likely double NAT / private WAN",
            (_, "NatReserved") => "Non-public/reserved WAN",
            (_, "NatUnknown") => "Undetermined",
            (_, "SummaryPublicListening") => "The gateway reports a public IPv4 and NavBR is listening locally. An outside callback test is still required to confirm Internet reachability.",
            (_, "SummaryPublicIdle") => "The gateway reports a public IPv4. Create a room to start TCP 27730; an external reachability test is still required afterward.",
            (_, "SummaryCgnat") => "There is a strong CGNAT signal. Local UPnP normally cannot cross the carrier NAT; a relay/fallback or ISP support may be required.",
            (_, "SummaryDoubleNat") => "The gateway received a private WAN IPv4. Double NAT or another upstream router is likely.",
            (_, "SummaryReserved") => "The reported WAN address is not a usable public IPv4.",
            (_, "SummaryUnknown") => "The router was found but did not provide a usable WAN IPv4.",
            (_, "SummaryNoGateway") => "No UPnP gateway was found. This does not mean that the Internet connection is down.",
            (_, "Checking") => "Inspecting network and router…",
            (_, "Refresh") => "Check again",
            (_, "Failed") => "Could not complete diagnostics: {0}",
            (_, "TechnicalPrefix") => "Technical details",
            (_, "GatewayLocal") => "Local address used to reach the gateway: {0}",
            (_, "PortState") => "TCP {0} on this PC: {1}",
            (_, "ExternalLimit") => "Current limit: NavBR does not yet operate a public callback server that can connect back to your port from outside. The UI therefore does not show a false 'port open' result.",
            _ => key
        };
    }

    private static SolidColorBrush Good() => Brush(101, 224, 154);
    private static SolidColorBrush Warning() => Brush(255, 187, 91);
    private static SolidColorBrush Neutral() => Brush(168, 180, 188);
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
