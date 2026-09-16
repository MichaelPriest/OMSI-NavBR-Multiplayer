using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;

namespace NavBR.Client.Windows;

internal sealed class NavBRManualWindow : Window
{
    private static readonly Brush WindowBackground = new SolidColorBrush(Color.FromRgb(5, 9, 13));
    private static readonly Brush CardBackground = new SolidColorBrush(Color.FromRgb(10, 20, 28));
    private static readonly Brush Border = new SolidColorBrush(Color.FromRgb(31, 47, 60));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(166, 183, 198));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(244, 122, 24));

    public NavBRManualWindow()
    {
        var copy = GetCopy();
        Title = copy.WindowTitle;
        Width = 900d;
        Height = 760d;
        MinWidth = 700d;
        MinHeight = 560d;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = WindowBackground;
        Foreground = Brushes.White;
        Content = BuildContent(copy);
    }

    private UIElement BuildContent(ManualCopy copy)
    {
        var root = new Grid { Margin = new Thickness(24d) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(2d, 0d, 2d, 18d) };
        header.Children.Add(new TextBlock
        {
            Text = copy.Title,
            FontSize = 27d,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        });
        header.Children.Add(new TextBlock
        {
            Text = copy.Intro,
            Margin = new Thickness(0d, 7d, 0d, 0d),
            FontSize = 14d,
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 21d
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var sections = new StackPanel();
        foreach (var section in copy.Sections)
        {
            sections.Children.Add(BuildSection(section));
        }

        var scroll = new ScrollViewer
        {
            Content = sections,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0d, 0d, 8d, 0d)
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = new Grid { Margin = new Thickness(2d, 16d, 2d, 0d) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(new TextBlock
        {
            Text = copy.Credits,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Muted,
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap
        });

        var close = new Button
        {
            Content = copy.Close,
            MinWidth = 110d,
            Height = 38d,
            Padding = new Thickness(18d, 7d, 18d, 7d),
            Background = Accent,
            Foreground = Brushes.White,
            BorderBrush = Accent,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1);
        footer.Children.Add(close);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private static Border BuildSection(ManualSection section)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = section.Title,
            FontSize = 18d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        stack.Children.Add(new TextBlock
        {
            Text = section.Body,
            Margin = new Thickness(0d, 9d, 0d, 0d),
            FontSize = 13.5d,
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 21d
        });

        return new Border
        {
            Background = CardBackground,
            BorderBrush = Border,
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Padding = new Thickness(18d),
            Margin = new Thickness(0d, 0d, 0d, 12d),
            Child = stack
        };
    }

    private static ManualCopy GetCopy() =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "es" => Spanish(),
            "de" => German(),
            "fr" => French(),
            "pt" => Portuguese(),
            _ => English()
        };

    private static ManualCopy Portuguese() => new(
        "Manual de uso • OMSI NavBR Multiplayer",
        "Manual de uso para iniciantes",
        "Siga este guia na ordem. Você não precisa entender arquivos, memória do OMSI ou rede para usar as funções básicas.",
        "Fechar",
        "Desenvolvedor: MichaelPriest • Com apoio da IA ChatGPT",
        new[]
        {
            new ManualSection("1. Comece aqui", "1) Abra o NavBR.\n2) Abra o OMSI 2.\n3) Aguarde o NavBR mostrar que o OMSI foi detectado e a telemetria foi conectada.\n4) No OMSI, carregue o mapa, escolha seu ônibus e inicie a partida.\n5) Durante o jogo, o HUD/minimapa deve aparecer automaticamente. Ao abrir menus, opções ou janelas auxiliares do OMSI, o HUD deve se esconder."),
            new ManualSection("2. HUD, GPS e velocidade", "O HUD mostra as informações úteis para dirigir sem ocupar grande parte da tela.\n\n• Velocidade: vem diretamente do OMSI.\n• Linha/destino: acompanham a viagem ativa quando o OMSI disponibiliza esses dados.\n• Próxima parada: é destacada no minimapa.\n• Com rota ativa: aparecem somente as paradas daquela rota/viagem.\n• Sem rota ativa: podem aparecer todas as paradas válidas do mapa.\n\nSe a velocidade continuar em 0 com o ônibus andando, feche o NavBR, abra a versão mais recente e teste novamente."),
            new ManualSection("3. Entrar ou criar uma sala multiplayer", "Para criar uma sala: abra Multiplayer, escolha Criar sala neste PC, informe sala e apelido. Seu computador passa a hospedar a sessão pela porta TCP 27730.\n\nPara entrar: informe o endereço do computador que criou a sala, use o mesmo nome da sala e escolha seu apelido.\n\nSe outro jogador não conseguir entrar pela internet, pode ser necessário liberar a porta TCP 27730 no Firewall do Windows e no roteador do host. Na mesma rede local normalmente é mais simples."),
            new ManualSection("4. Chat e voz", "F9 abre o chat de texto durante o jogo.\nF10 é pressione-para-falar: mantenha a tecla pressionada enquanto fala.\n\nSe não houver som, confira o dispositivo de áudio do Windows e se o chat por voz está ativado na janela Multiplayer."),
            new ManualSection("5. Ônibus remoto 3D — EXPERIMENTAL", "Esse recurso é de teste e vem desligado por padrão. Ative “Ônibus remoto 3D (EXPERIMENTAL)” somente se quiser participar do teste.\n\nOs computadores precisam ter o mesmo mapa e o modelo do ônibus remoto instalado localmente no mesmo caminho dentro da pasta Vehicles. O NavBR não transfere ônibus pagos ou arquivos proprietários.\n\nSe o ônibus remoto não aparecer, confirme primeiro: mesmo mapa, plugin instalado, mesmo modelo de ônibus e opção experimental ligada. Se ocorrer crash ou congelamento, desligue o 3D e reinicie o OMSI."),
            new ManualSection("6. Diagnósticos automáticos", "A opção “Enviar diagnósticos automáticos do teste” é opcional e vem desligada por padrão. Ela ajuda o desenvolvimento a receber erros técnicos sem pedir que você copie arquivos de log.\n\nPode enviar versão do NavBR/OMSI, mapa, identificação técnica do veículo, estado do plugin/bridge e mensagens de erro. Não envia chat, áudio/voz, senha, token ou seus arquivos pessoais. Ao desligar a opção, a fila local pendente é apagada."),
            new ManualSection("7. Se algo não funcionar", "OMSI não detectado: confirme que Omsi.exe está aberto e use Detectar novamente.\n\nHUD não aparece: entre no gameplay; ele deve ficar oculto em menus e opções.\n\nMapa não aparece: confirme que o mapa possui roadmap compatível ou gere o roadmap pelas ferramentas do NavBR.\n\nParadas erradas: verifique se uma rota/viagem está realmente ativa no OMSI.\n\nMultiplayer não conecta: confira endereço do host, nome da sala, porta 27730 e firewall.\n\n3D não aparece: teste primeiro com dois jogadores, mesmo mapa e exatamente o mesmo ônibus instalado."),
            new ManualSection("8. Para o teste da comunidade", "Ao encontrar um erro, anote o que estava fazendo, mapa, ônibus e se o 3D experimental estava ligado. Se os diagnósticos automáticos estiverem ativados, o NavBR também enviará os dados técnicos permitidos para ajudar a localizar a falha.")
        });

    private static ManualCopy English() => new(
        "User manual • OMSI NavBR Multiplayer",
        "Beginner user manual",
        "Follow this guide in order. You do not need to understand OMSI memory, files or networking to use the basic features.",
        "Close",
        "Developer: MichaelPriest • With AI assistance from ChatGPT",
        new[]
        {
            new ManualSection("1. Start here", "1) Open NavBR.\n2) Open OMSI 2.\n3) Wait until NavBR reports that OMSI was detected and telemetry is connected.\n4) In OMSI, load a map, choose your bus and start driving.\n5) The HUD/minimap should appear automatically during gameplay and hide while OMSI menus or auxiliary windows are open."),
            new ManualSection("2. HUD, GPS and speed", "The HUD keeps the most useful driving information compact. Speed is read from OMSI. Line, destination and next stop follow the active trip when available. With an active route, only stops belonging to that route/trip are shown. Without an active route, all valid map stops may be shown."),
            new ManualSection("3. Multiplayer", "To host: open Multiplayer, create a room on this PC, enter a room name and nickname. Your PC hosts the session on TCP port 27730. To join: enter the host computer address, the same room name and your nickname. Internet hosting may require opening TCP 27730 in Windows Firewall and the router."),
            new ManualSection("4. Chat and voice", "F9 opens text chat in game. F10 is push-to-talk: hold it while speaking. If voice does not work, check the Windows audio device and that voice chat is enabled."),
            new ManualSection("5. Remote 3D bus — EXPERIMENTAL", "This test feature is off by default. Both computers must have the same map and the remote bus model installed locally at the same relative Vehicles path. NavBR does not transfer paid or proprietary buses. If the simulator crashes or freezes, turn 3D off and restart OMSI."),
            new ManualSection("6. Automatic diagnostics", "Automatic test diagnostics are optional and off by default. They may send NavBR/OMSI version, map, technical vehicle identity, plugin/bridge state and error messages. They do not send chat, voice/audio, passwords, tokens or personal files. Turning the option off clears the pending local queue."),
            new ManualSection("7. Troubleshooting", "OMSI not detected: make sure Omsi.exe is running and click Detect again. HUD missing: enter gameplay; it should hide in OMSI menus. Multiplayer cannot connect: verify host address, room name, TCP 27730 and firewall. 3D missing: test with two players using the same map and exactly the same locally installed bus."),
            new ManualSection("8. Community testing", "When reporting a problem, note what you were doing, the map, bus and whether experimental 3D was enabled. If automatic diagnostics are enabled, NavBR can also send the permitted technical data to help reproduce the issue.")
        });

    private static ManualCopy Spanish() => new(
        "Manual de uso • OMSI NavBR Multiplayer",
        "Manual para principiantes",
        "Sigue esta guía en orden. No necesitas entender memoria, archivos o redes de OMSI para usar las funciones básicas.",
        "Cerrar",
        "Desarrollador: MichaelPriest • Con apoyo de la IA ChatGPT",
        new[]
        {
            new ManualSection("1. Primeros pasos", "1) Abre NavBR.\n2) Abre OMSI 2.\n3) Espera hasta que NavBR detecte OMSI y conecte la telemetría.\n4) Carga el mapa, elige el autobús y empieza la partida.\n5) El HUD/minimapa debe aparecer durante el juego y ocultarse en menús y ventanas auxiliares."),
            new ManualSection("2. HUD, GPS y velocidad", "La velocidad se lee directamente de OMSI. La línea, destino y próxima parada siguen el viaje activo cuando están disponibles. Con una ruta activa solo aparecen las paradas de esa ruta; sin ruta activa pueden aparecer todas las paradas válidas del mapa."),
            new ManualSection("3. Multiplayer", "Para crear una sala, abre Multiplayer y selecciona Crear sala en este PC. El host usa el puerto TCP 27730. Para entrar, usa la dirección del host, el mismo nombre de sala y tu apodo. Por Internet puede ser necesario liberar TCP 27730 en firewall y router."),
            new ManualSection("4. Chat y voz", "F9 abre el chat de texto. F10 es pulsar-para-hablar: mantenlo presionado mientras hablas."),
            new ManualSection("5. Autobús remoto 3D — EXPERIMENTAL", "Está desactivado por defecto. Ambos jugadores necesitan el mismo mapa y el mismo modelo de autobús instalado localmente en la misma ruta de Vehicles. NavBR no transfiere contenido pago o propietario."),
            new ManualSection("6. Diagnósticos automáticos", "Son opcionales y están desactivados por defecto. Pueden enviar versiones, mapa, identificación técnica del vehículo, estado del plugin/bridge y errores. No envían chat, voz, contraseñas, tokens ni archivos personales."),
            new ManualSection("7. Si algo falla", "OMSI no detectado: confirma que Omsi.exe está abierto. HUD ausente: entra al juego. Multiplayer sin conexión: revisa host, sala, TCP 27730 y firewall. 3D ausente: prueba con dos jugadores, mismo mapa y mismo autobús."),
            new ManualSection("8. Prueba comunitaria", "Al informar un fallo, anota mapa, autobús, qué estabas haciendo y si el 3D experimental estaba activado.")
        });

    private static ManualCopy German() => new(
        "Benutzerhandbuch • OMSI NavBR Multiplayer",
        "Einfaches Handbuch für Einsteiger",
        "Folge den Schritten der Reihe nach. Für die Grundfunktionen sind keine Kenntnisse über OMSI-Speicher, Dateien oder Netzwerke nötig.",
        "Schließen",
        "Entwickler: MichaelPriest • Mit KI-Unterstützung durch ChatGPT",
        new[]
        {
            new ManualSection("1. Erste Schritte", "1) NavBR starten.\n2) OMSI 2 starten.\n3) Warten, bis OMSI erkannt und die Telemetrie verbunden ist.\n4) Karte und Bus in OMSI laden und die Fahrt starten.\n5) HUD/Minikarte erscheinen im Spiel automatisch und werden in OMSI-Menüs ausgeblendet."),
            new ManualSection("2. HUD, GPS und Geschwindigkeit", "Die Geschwindigkeit wird direkt aus OMSI gelesen. Linie, Ziel und nächste Haltestelle folgen der aktiven Fahrt. Bei aktiver Route werden nur die Haltestellen dieser Route angezeigt; ohne aktive Route können alle gültigen Haltestellen der Karte erscheinen."),
            new ManualSection("3. Multiplayer", "Zum Hosten im Multiplayer-Fenster einen Raum auf diesem PC erstellen. Der Host verwendet TCP-Port 27730. Zum Beitreten Host-Adresse, denselben Raumnamen und einen Spitznamen eingeben. Über das Internet müssen Firewall/Router eventuell TCP 27730 freigeben."),
            new ManualSection("4. Chat und Sprache", "F9 öffnet den Textchat. F10 ist Push-to-Talk: beim Sprechen gedrückt halten."),
            new ManualSection("5. Entfernter 3D-Bus — EXPERIMENTELL", "Standardmäßig deaktiviert. Beide PCs benötigen dieselbe Karte und dasselbe Busmodell im gleichen relativen Vehicles-Pfad. NavBR überträgt keine kostenpflichtigen oder proprietären Busdateien."),
            new ManualSection("6. Automatische Diagnose", "Optional und standardmäßig deaktiviert. Gesendet werden können Versionen, Karte, technische Fahrzeugkennung, Plugin/Bridge-Status und Fehler. Chat, Sprache, Passwörter, Tokens und persönliche Dateien werden nicht gesendet."),
            new ManualSection("7. Wenn etwas nicht funktioniert", "OMSI nicht erkannt: prüfen, ob Omsi.exe läuft. HUD fehlt: ins Gameplay wechseln. Multiplayer verbindet nicht: Host-Adresse, Raum, TCP 27730 und Firewall prüfen. 3D fehlt: mit zwei Spielern, gleicher Karte und exakt gleichem Bus testen."),
            new ManualSection("8. Community-Test", "Bei Fehlern Karte, Bus, Aktion und den Status des experimentellen 3D-Modus notieren.")
        });

    private static ManualCopy French() => new(
        "Manuel d’utilisation • OMSI NavBR Multiplayer",
        "Manuel simple pour débutants",
        "Suivez ce guide dans l’ordre. Aucune connaissance de la mémoire d’OMSI, des fichiers ou du réseau n’est nécessaire pour les fonctions de base.",
        "Fermer",
        "Développeur : MichaelPriest • Avec l’aide de l’IA ChatGPT",
        new[]
        {
            new ManualSection("1. Bien démarrer", "1) Ouvrez NavBR.\n2) Ouvrez OMSI 2.\n3) Attendez que NavBR détecte OMSI et connecte la télémétrie.\n4) Chargez la carte, choisissez le bus et démarrez la partie.\n5) Le HUD/minicarte apparaît pendant le jeu et se masque dans les menus ou fenêtres auxiliaires d’OMSI."),
            new ManualSection("2. HUD, GPS et vitesse", "La vitesse est lue directement depuis OMSI. Ligne, destination et prochain arrêt suivent le trajet actif. Avec une route active, seuls les arrêts de cette route sont affichés ; sans route active, tous les arrêts valides de la carte peuvent apparaître."),
            new ManualSection("3. Multijoueur", "Pour héberger, créez une salle sur ce PC dans la fenêtre Multijoueur. L’hôte utilise le port TCP 27730. Pour rejoindre, indiquez l’adresse de l’hôte, le même nom de salle et votre pseudo. Sur Internet, il peut être nécessaire d’ouvrir TCP 27730 dans le pare-feu et le routeur."),
            new ManualSection("4. Chat et voix", "F9 ouvre le chat texte. F10 est le push-to-talk : maintenez la touche pendant que vous parlez."),
            new ManualSection("5. Bus distant 3D — EXPÉRIMENTAL", "Désactivé par défaut. Les deux PC doivent avoir la même carte et le même modèle de bus installé localement dans le même chemin relatif Vehicles. NavBR ne transfère pas les bus payants ou propriétaires."),
            new ManualSection("6. Diagnostics automatiques", "Ils sont facultatifs et désactivés par défaut. Ils peuvent envoyer versions, carte, identifiant technique du véhicule, état plugin/bridge et erreurs. Ils n’envoient pas chat, voix, mots de passe, jetons ou fichiers personnels."),
            new ManualSection("7. En cas de problème", "OMSI non détecté : vérifiez que Omsi.exe est ouvert. HUD absent : entrez dans le gameplay. Multijoueur impossible : vérifiez l’hôte, la salle, TCP 27730 et le pare-feu. 3D absent : testez avec deux joueurs, la même carte et exactement le même bus."),
            new ManualSection("8. Test communautaire", "Lors d’un problème, notez la carte, le bus, ce que vous faisiez et si le mode 3D expérimental était activé.")
        });

    private sealed record ManualCopy(
        string WindowTitle,
        string Title,
        string Intro,
        string Close,
        string Credits,
        IReadOnlyList<ManualSection> Sections);

    private sealed record ManualSection(string Title, string Body);
}
