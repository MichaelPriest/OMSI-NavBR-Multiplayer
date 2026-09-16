using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

internal sealed class ExternalPortProbeWindow : Window
{
    private readonly NatDiagnosticsService _localDiagnostics = new();
    private readonly ExternalPortProbeClient _probeClient = new();
    private readonly TextBlock _state = new();
    private readonly TextBlock _details = new();
    private readonly Button _probeButton = new();
    private readonly Button _refreshButton = new();
    private bool _busy;
    private bool _localHostListening;

    public ExternalPortProbeWindow(Window owner)
    {
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Width = 650d;
        Height = 470d;
        MinWidth = 580d;
        MinHeight = 420d;
        Background = Brush(6, 11, 16);
        Title = Text("Title");
        Content = BuildContent();
        Loaded += async (_, _) => await RefreshPrerequisitesAsync();
        Closed += (_, _) => _probeClient.Dispose();
    }

    internal static string ButtonText() => LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
    {
        "pt" => "Teste externo (experimental)",
        "es" => "Prueba externa (experimental)",
        "de" => "Externer Test (experimentell)",
        "fr" => "Test externe (expérimental)",
        _ => "External test (experimental)"
    };

    private UIElement BuildContent()
    {
        var root = new StackPanel { Margin = new Thickness(28d) };
        root.Children.Add(new TextBlock
        {
            Text = Text("Title"),
            Foreground = Brushes.White,
            FontSize = 24d,
            FontWeight = FontWeights.Bold
        });
        root.Children.Add(new TextBlock
        {
            Text = Text("Subtitle"),
            Foreground = Brush(142, 161, 174),
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 6d, 0d, 18d)
        });

        _state.Foreground = Brushes.White;
        _state.FontSize = 17d;
        _state.FontWeight = FontWeights.SemiBold;
        _state.TextWrapping = TextWrapping.Wrap;
        _details.Foreground = Brush(155, 173, 185);
        _details.FontSize = 10.8d;
        _details.TextWrapping = TextWrapping.Wrap;
        _details.Margin = new Thickness(0d, 8d, 0d, 0d);

        var card = new Border
        {
            Padding = new Thickness(18d),
            CornerRadius = new CornerRadius(12d),
            Background = Brush(10, 19, 25),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            Child = new StackPanel
            {
                Children =
                {
                    _state,
                    _details
                }
            }
        };
        root.Children.Add(card);

        var actions = new WrapPanel { Margin = new Thickness(0d, 18d, 0d, 0d) };
        ConfigureButton(_probeButton, primary: true);
        _probeButton.Content = Text("Probe");
        _probeButton.Click += async (_, _) => await ProbeAsync();
        actions.Children.Add(_probeButton);

        ConfigureButton(_refreshButton, primary: false);
        _refreshButton.Content = Text("Refresh");
        _refreshButton.Margin = new Thickness(10d, 0d, 0d, 0d);
        _refreshButton.Click += async (_, _) => await RefreshPrerequisitesAsync();
        actions.Children.Add(_refreshButton);
        root.Children.Add(actions);

        root.Children.Add(new TextBlock
        {
            Text = Text("Privacy"),
            Foreground = Brush(110, 132, 146),
            FontSize = 9.8d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 18d, 0d, 0d)
        });

        return root;
    }

    private async Task RefreshPrerequisitesAsync()
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var local = await _localDiagnostics.InspectAsync();
            _localHostListening = local.LocalPortListening;

            if (!_probeClient.IsConfigured)
            {
                _state.Text = Text("NotConfigured");
                _state.Foreground = Warning();
                _details.Text = string.Format(
                    Text("NotConfiguredBody"),
                    ExternalPortProbeClient.ProbeUrlEnvironmentVariable);
                return;
            }

            if (!_localHostListening)
            {
                _state.Text = Text("HostNotListening");
                _state.Foreground = Warning();
                _details.Text = Text("HostNotListeningBody");
                return;
            }

            _state.Text = Text("Ready");
            _state.Foreground = Good();
            _details.Text = string.Format(
                Text("ReadyBody"),
                _probeClient.ServiceOrigin ?? "—",
                NatDiagnosticsService.HostPort);
        }
        catch (Exception ex)
        {
            _state.Text = Text("PrerequisiteFailed");
            _state.Foreground = Bad();
            _details.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ProbeAsync()
    {
        if (_busy || !_probeClient.IsConfigured || !_localHostListening)
        {
            return;
        }

        SetBusy(true);
        _state.Text = Text("Testing");
        _state.Foreground = Brushes.White;
        _details.Text = Text("TestingBody");

        try
        {
            var result = await _probeClient.ProbeAsync();
            RenderResult(result);
        }
        catch (Exception ex)
        {
            _state.Text = Text("ProbeFailed");
            _state.Foreground = Bad();
            _details.Text = string.Format(Text("ProbeFailedBody"), ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RenderResult(ExternalPortProbeResult? result)
    {
        if (result is null)
        {
            _state.Text = Text("NotConfigured");
            _state.Foreground = Warning();
            _details.Text = string.Format(
                Text("NotConfiguredBody"),
                ExternalPortProbeClient.ProbeUrlEnvironmentVariable);
            return;
        }

        if (result.Reachable)
        {
            _state.Text = Text("Reachable");
            _state.Foreground = Good();
            _details.Text = string.Format(
                Text("ReachableBody"),
                result.Port,
                result.DurationMilliseconds);
            return;
        }

        _state.Foreground = Warning();
        switch (result.Status)
        {
            case "source_not_public":
                _state.Text = Text("SourceNotPublic");
                _details.Text = Text("SourceNotPublicBody");
                break;
            case "timeout":
                _state.Text = Text("Timeout");
                _details.Text = string.Format(Text("UnreachableBody"), result.Port);
                break;
            default:
                _state.Text = Text("Unreachable");
                _details.Text = string.Format(Text("UnreachableBody"), result.Port);
                break;
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _refreshButton.IsEnabled = !busy;
        _probeButton.IsEnabled = !busy && _probeClient.IsConfigured && _localHostListening;
    }

    private static void ConfigureButton(Button button, bool primary)
    {
        button.Height = 40d;
        button.MinWidth = 190d;
        button.Padding = new Thickness(16d, 8d, 16d, 8d);
        button.Foreground = primary ? Brush(22, 15, 7) : Brush(218, 230, 238);
        button.Background = primary ? Brush(255, 164, 75) : Brush(14, 27, 35);
        button.BorderBrush = primary ? Brush(255, 190, 125) : Brush(44, 65, 78);
        button.BorderThickness = new Thickness(1d);
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static string Text(string key)
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        return (language, key) switch
        {
            ("pt", "Title") => "Teste externo da porta",
            ("pt", "Subtitle") => "Validação experimental feita por um servidor NavBR realmente externo. O serviço tenta conectar de volta ao seu IP de origem na porta TCP 27730.",
            ("pt", "Probe") => "Testar TCP 27730 de fora",
            ("pt", "Refresh") => "Verificar pré-requisitos",
            ("pt", "NotConfigured") => "Serviço externo não configurado",
            ("pt", "NotConfiguredBody") => "Defina {0} com a URL base de uma instância pública NavBR habilitada para probe. A Alpha.12 ainda não define um serviço oficial padrão.",
            ("pt", "HostNotListening") => "Crie uma sala primeiro",
            ("pt", "HostNotListeningBody") => "Este PC ainda não está escutando TCP 27730. Inicie uma sala peer-host antes de executar o callback externo.",
            ("pt", "Ready") => "Pronto para teste externo",
            ("pt", "ReadyBody") => "Serviço: {0}\nDestino permitido pelo servidor: somente o IP de origem desta conexão, TCP {1}.",
            ("pt", "Testing") => "Testando de fora…",
            ("pt", "TestingBody") => "O servidor externo está tentando abrir uma conexão TCP até seu host.",
            ("pt", "Reachable") => "TCP 27730 acessível pela Internet",
            ("pt", "ReachableBody") => "O callback conseguiu alcançar TCP {0}. Tempo do probe: {1} ms.",
            ("pt", "Unreachable") => "TCP 27730 não foi alcançada",
            ("pt", "Timeout") => "O teste externo expirou",
            ("pt", "UnreachableBody") => "O servidor não conseguiu alcançar TCP {0}. Verifique firewall, UPnP/redirecionamento, double NAT ou CGNAT.",
            ("pt", "SourceNotPublic") => "O servidor não recebeu um IP público utilizável",
            ("pt", "SourceNotPublicBody") => "A origem observada pelo serviço é privada/reservada. Isso pode indicar implantação atrás de proxy sem configuração adequada ou ambiente de teste local.",
            ("pt", "PrerequisiteFailed") => "Falha ao verificar pré-requisitos",
            ("pt", "ProbeFailed") => "Falha ao chamar o serviço externo",
            ("pt", "ProbeFailedBody") => "O callback não pôde ser executado: {0}",
            ("pt", "Privacy") => "Privacidade e segurança: o cliente não envia um IP-alvo nem uma porta arbitrária. O servidor usa apenas o endereço de origem que ele próprio observa e a porta fixa 27730; o endpoint fica desativado por padrão.",

            ("es", "Title") => "Prueba externa del puerto",
            ("es", "Subtitle") => "Validación experimental realizada por un servidor NavBR realmente externo. Intenta conectar al IP de origen por TCP 27730.",
            ("es", "Probe") => "Probar TCP 27730 desde fuera",
            ("es", "Refresh") => "Verificar requisitos",
            ("es", "NotConfigured") => "Servicio externo no configurado",
            ("es", "NotConfiguredBody") => "Define {0} con la URL base de una instancia pública NavBR habilitada para probe. Alpha.12 aún no define un servicio oficial por defecto.",
            ("es", "HostNotListening") => "Crea una sala primero",
            ("es", "HostNotListeningBody") => "Este PC aún no escucha TCP 27730. Inicia una sala peer-host antes del callback externo.",
            ("es", "Ready") => "Listo para la prueba externa",
            ("es", "ReadyBody") => "Servicio: {0}\nDestino permitido: solo la IP de origen observada, TCP {1}.",
            ("es", "Testing") => "Probando desde fuera…",
            ("es", "TestingBody") => "El servidor externo intenta abrir una conexión TCP hacia tu host.",
            ("es", "Reachable") => "TCP 27730 accesible desde Internet",
            ("es", "ReachableBody") => "El callback alcanzó TCP {0}. Tiempo: {1} ms.",
            ("es", "Unreachable") => "TCP 27730 no fue accesible",
            ("es", "Timeout") => "La prueba externa agotó el tiempo",
            ("es", "UnreachableBody") => "El servidor no pudo alcanzar TCP {0}. Revisa firewall, UPnP/redirección, doble NAT o CGNAT.",
            ("es", "SourceNotPublic") => "El servidor no observó una IP pública utilizable",
            ("es", "SourceNotPublicBody") => "El origen observado es privado/reservado; puede indicar un proxy no configurado o un entorno local.",
            ("es", "PrerequisiteFailed") => "Error al verificar requisitos",
            ("es", "ProbeFailed") => "Error al llamar al servicio externo",
            ("es", "ProbeFailedBody") => "No se pudo ejecutar el callback: {0}",
            ("es", "Privacy") => "Seguridad: el cliente no envía IP ni puerto objetivo arbitrarios. El servidor usa solo la IP de origen observada y el puerto fijo 27730; el endpoint está desactivado por defecto.",

            ("de", "Title") => "Externer Porttest",
            ("de", "Subtitle") => "Experimentelle Prüfung durch einen wirklich externen NavBR-Server. Er verbindet zurück zur beobachteten Quell-IP auf TCP 27730.",
            ("de", "Probe") => "TCP 27730 von außen testen",
            ("de", "Refresh") => "Voraussetzungen prüfen",
            ("de", "NotConfigured") => "Externer Dienst nicht konfiguriert",
            ("de", "NotConfiguredBody") => "Setze {0} auf die Basis-URL einer öffentlichen NavBR-Instanz mit aktiviertem Probe. Alpha.12 hat noch keinen offiziellen Standarddienst.",
            ("de", "HostNotListening") => "Zuerst einen Raum erstellen",
            ("de", "HostNotListeningBody") => "Dieser PC lauscht noch nicht auf TCP 27730. Starte zuerst einen Peer-Host-Raum.",
            ("de", "Ready") => "Bereit für externen Test",
            ("de", "ReadyBody") => "Dienst: {0}\nErlaubtes Ziel: nur beobachtete Quell-IP, TCP {1}.",
            ("de", "Testing") => "Test von außen läuft…",
            ("de", "TestingBody") => "Der externe Server versucht eine TCP-Verbindung zu deinem Host.",
            ("de", "Reachable") => "TCP 27730 aus dem Internet erreichbar",
            ("de", "ReachableBody") => "Callback erreichte TCP {0}. Probe-Zeit: {1} ms.",
            ("de", "Unreachable") => "TCP 27730 nicht erreichbar",
            ("de", "Timeout") => "Externer Test Zeitüberschreitung",
            ("de", "UnreachableBody") => "TCP {0} war nicht erreichbar. Firewall, UPnP/Weiterleitung, Doppel-NAT oder CGNAT prüfen.",
            ("de", "SourceNotPublic") => "Keine nutzbare öffentliche Quell-IP beobachtet",
            ("de", "SourceNotPublicBody") => "Die beobachtete Quelle ist privat/reserviert; möglich sind ein nicht konfigurierter Proxy oder lokale Tests.",
            ("de", "PrerequisiteFailed") => "Voraussetzungen konnten nicht geprüft werden",
            ("de", "ProbeFailed") => "Externer Dienst nicht erreichbar",
            ("de", "ProbeFailedBody") => "Callback konnte nicht ausgeführt werden: {0}",
            ("de", "Privacy") => "Sicherheit: Der Client sendet kein beliebiges Ziel-IP/Port-Paar. Der Server nutzt nur die beobachtete Quell-IP und festen Port 27730; der Endpoint ist standardmäßig deaktiviert.",

            ("fr", "Title") => "Test externe du port",
            ("fr", "Subtitle") => "Validation expérimentale par un serveur NavBR réellement externe. Il tente une connexion vers l’IP source observée sur TCP 27730.",
            ("fr", "Probe") => "Tester TCP 27730 depuis l’extérieur",
            ("fr", "Refresh") => "Vérifier les prérequis",
            ("fr", "NotConfigured") => "Service externe non configuré",
            ("fr", "NotConfiguredBody") => "Définissez {0} avec l’URL de base d’une instance NavBR publique activée pour le probe. Alpha.12 n’a pas encore de service officiel par défaut.",
            ("fr", "HostNotListening") => "Créez d’abord une salle",
            ("fr", "HostNotListeningBody") => "Ce PC n’écoute pas encore TCP 27730. Démarrez une salle peer-host avant le callback externe.",
            ("fr", "Ready") => "Prêt pour le test externe",
            ("fr", "ReadyBody") => "Service : {0}\nDestination autorisée : uniquement l’IP source observée, TCP {1}.",
            ("fr", "Testing") => "Test depuis l’extérieur…",
            ("fr", "TestingBody") => "Le serveur externe tente d’ouvrir une connexion TCP vers votre hôte.",
            ("fr", "Reachable") => "TCP 27730 accessible depuis Internet",
            ("fr", "ReachableBody") => "Le callback a atteint TCP {0}. Durée : {1} ms.",
            ("fr", "Unreachable") => "TCP 27730 inaccessible",
            ("fr", "Timeout") => "Le test externe a expiré",
            ("fr", "UnreachableBody") => "Le serveur n’a pas atteint TCP {0}. Vérifiez pare-feu, UPnP/redirection, double NAT ou CGNAT.",
            ("fr", "SourceNotPublic") => "Aucune IP source publique utilisable observée",
            ("fr", "SourceNotPublicBody") => "La source observée est privée/réservée; cela peut indiquer un proxy non configuré ou un test local.",
            ("fr", "PrerequisiteFailed") => "Échec de la vérification des prérequis",
            ("fr", "ProbeFailed") => "Échec du service externe",
            ("fr", "ProbeFailedBody") => "Le callback n’a pas pu être exécuté : {0}",
            ("fr", "Privacy") => "Sécurité : le client n’envoie ni IP cible ni port arbitraire. Le serveur utilise uniquement l’IP source observée et le port fixe 27730; l’endpoint est désactivé par défaut.",

            (_, "Title") => "External port test",
            (_, "Subtitle") => "Experimental validation by a truly external NavBR server. It connects back to the observed source IP on TCP 27730.",
            (_, "Probe") => "Test TCP 27730 from outside",
            (_, "Refresh") => "Check prerequisites",
            (_, "NotConfigured") => "External service not configured",
            (_, "NotConfiguredBody") => "Set {0} to the base URL of a public NavBR instance with probing enabled. Alpha.12 does not define an official default service yet.",
            (_, "HostNotListening") => "Create a room first",
            (_, "HostNotListeningBody") => "This PC is not listening on TCP 27730 yet. Start a peer-host room before running the external callback.",
            (_, "Ready") => "Ready for external test",
            (_, "ReadyBody") => "Service: {0}\nAllowed destination: only the observed source IP, TCP {1}.",
            (_, "Testing") => "Testing from outside…",
            (_, "TestingBody") => "The external server is trying to open a TCP connection back to your host.",
            (_, "Reachable") => "TCP 27730 is reachable from the Internet",
            (_, "ReachableBody") => "The callback reached TCP {0}. Probe time: {1} ms.",
            (_, "Unreachable") => "TCP 27730 was not reachable",
            (_, "Timeout") => "The external test timed out",
            (_, "UnreachableBody") => "The server could not reach TCP {0}. Check firewall, UPnP/port forwarding, double NAT, or CGNAT.",
            (_, "SourceNotPublic") => "The server did not observe a usable public source IP",
            (_, "SourceNotPublicBody") => "The observed source is private/reserved; this may indicate an unconfigured reverse proxy or local test environment.",
            (_, "PrerequisiteFailed") => "Could not check prerequisites",
            (_, "ProbeFailed") => "Could not call external service",
            (_, "ProbeFailedBody") => "The callback could not run: {0}",
            (_, "Privacy") => "Security: the client does not send an arbitrary target IP or port. The server uses only the source address it observes and fixed port 27730; the endpoint is disabled by default.",
            _ => key
        };
    }

    private static SolidColorBrush Good() => Brush(101, 224, 154);
    private static SolidColorBrush Warning() => Brush(255, 187, 91);
    private static SolidColorBrush Bad() => Brush(255, 112, 112);
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
}
