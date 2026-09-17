using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Driver;
using NavBR.Client.Hardware;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Windows;

internal static class Alpha12FigmaShellInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window) || window.Content is not UIElement)
        {
            return;
        }

        var statusCard = FindAncestor<Border>(window.StatusHeadingText);
        var telemetryCard = FindAncestor<Border>(window.TelemetryHeadingText);
        var navigationCard = FindAncestor<Border>(window.GpsHeadingText);
        var diagnosticsCard = FindAncestor<Border>(window.MilestoneHeadingText);
        if (statusCard is null || telemetryCard is null || navigationCard is null || diagnosticsCard is null)
        {
            Installed.Remove(window);
            return;
        }

        Detach(statusCard);
        Detach(telemetryCard);
        Detach(navigationCard);
        Detach(diagnosticsCard);
        Detach(window.MultiplayerButton);
        Detach(window.LanguageLabelText);
        Detach(window.LanguageComboBox);
        window.Content = null;

        window.Width = Math.Max(window.Width, 1500d);
        window.Height = Math.Max(window.Height, 880d);
        window.MinWidth = 1060d;
        window.MinHeight = 700d;

        NormalizeCard(navigationCard);
        NormalizeCard(diagnosticsCard);

        var pages = BuildPages();
        pages.Home.Content = BuildHome(window);
        pages.Navigation.Content = BuildNavigation(window, navigationCard);
        pages.Multiplayer.Content = BuildMultiplayer(window);
        pages.Hardware.Content = BuildHardware(window);
        pages.Diagnostics.Content = BuildDiagnostics(diagnosticsCard);

        var root = new Grid { Background = Brush(6, 16, 26) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(252d) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var sidebar = BuildSidebar(window, pages);
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        var workspace = new Grid { Background = Brush(6, 16, 26) };
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64d) });
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        workspace.Children.Add(BuildTopbar(window));

        var host = new Grid();
        foreach (var page in pages.All)
        {
            host.Children.Add(page);
        }
        pages.Navigation.Visibility = Visibility.Hidden;
        pages.Multiplayer.Visibility = Visibility.Hidden;
        pages.Hardware.Visibility = Visibility.Hidden;
        pages.Diagnostics.Visibility = Visibility.Hidden;
        Grid.SetRow(host, 1);
        workspace.Children.Add(host);

        Grid.SetColumn(workspace, 1);
        root.Children.Add(workspace);
        window.Content = root;

        window.Closed += (_, _) => Installed.Remove(window);
    }

    private static ShellPages BuildPages() => new(
        NewPage(),
        NewPage(),
        NewPage(),
        NewPage(),
        NewPage());

    private static UIElement BuildHome(MainWindow window)
    {
        var stack = PageStack();
        var profile = DriverProfileStore.Load();

        var heading = new Grid { Margin = new Thickness(0d, 0d, 0d, 28d) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headingText = new StackPanel();
        headingText.Children.Add(Text(string.Format(L("Olá, {0}", "Hello, {0}", "Hola, {0}", "Hallo, {0}", "Bonjour, {0}"), profile.DisplayName), 30d, White(), FontWeights.Bold));
        headingText.Children.Add(Text(L(
            "Visão operacional do OMSI e da sua sessão NavBR.",
            "Operational view of OMSI and your NavBR session.",
            "Vista operativa de OMSI y de tu sesión NavBR.",
            "Betriebsübersicht von OMSI und deiner NavBR-Sitzung.",
            "Vue opérationnelle d’OMSI et de votre session NavBR."), 13d, Muted(), FontWeights.Normal, new Thickness(0d, 6d, 0d, 0d)));
        heading.Children.Add(headingText);
        var openNavigation = PrimaryButton(L("Abrir navegação", "Open navigation", "Abrir navegación", "Navigation öffnen", "Ouvrir la navigation"));
        openNavigation.Width = 170d;
        openNavigation.Click += (_, _) => RaiseNavigation(window, "⌖");
        Grid.SetColumn(openNavigation, 1);
        heading.Children.Add(openNavigation);
        stack.Children.Add(heading);

        var vehicle = ValueText("—", 17d);
        var omsiState = SecondaryText(L("Aguardando OMSI", "Waiting for OMSI", "Esperando OMSI", "Warte auf OMSI", "En attente d’OMSI"));
        var map = SecondaryText($"{L("Mapa", "Map", "Mapa", "Karte", "Carte")} • —");
        var line = ValueText("—", 28d);
        var destination = SecondaryText("—");
        var nextStop = SecondaryText($"{L("Próxima parada", "Next stop", "Próxima parada", "Nächste Haltestelle", "Prochain arrêt")} • —");
        var multiplayerState = ValueText(L("Sem sessão", "No session", "Sin sesión", "Keine Sitzung", "Aucune session"), 17d);
        var multiplayerDetail = SecondaryText(L("Abra a Central Multiplayer", "Open the Multiplayer Center", "Abra la Central Multiplayer", "Multiplayer-Zentrale öffnen", "Ouvrez la centrale multijoueur"));
        var companyName = ValueText(profile.CompanyName ?? L("Sem empresa", "No company", "Sin empresa", "Kein Unternehmen", "Aucune entreprise"), 17d);
        var companyDetail = SecondaryText(L("Perfil do motorista", "Driver profile", "Perfil del conductor", "Fahrerprofil", "Profil conducteur"));

        var cards = new Grid { Margin = new Thickness(-8d, 0d, -8d, 0d) };
        for (var i = 0; i < 7; i++)
        {
            cards.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = i is 1 or 3 or 5 ? new GridLength(16d) : new GridLength(1d, GridUnitType.Star)
            });
        }
        AddGridCard(cards, 0, "OMSI", vehicle, omsiState, map);
        AddGridCard(cards, 2, "OPERAÇÃO ATUAL", line, destination, nextStop);
        AddGridCard(cards, 4, "MULTIPLAYER", multiplayerState, multiplayerDetail);
        AddGridCard(cards, 6, "EMPRESA", companyName, companyDetail);
        stack.Children.Add(cards);

        var operationRow = new Grid { Margin = new Thickness(0d, 20d, 0d, 0d) };
        operationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2d, GridUnitType.Star) });
        operationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16d) });
        operationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var operation = Card("OPERAÇÃO");
        var operationBody = (StackPanel)operation.Child;
        var currentStop = ValueText("—", 26d);
        currentStop.Margin = new Thickness(0d, 22d, 0d, 0d);
        operationBody.Children.Add(SecondaryText("Próxima parada"));
        operationBody.Children.Add(currentStop);
        var street = SecondaryText("Rua atual • —");
        street.Margin = new Thickness(0d, 9d, 0d, 0d);
        operationBody.Children.Add(street);
        var routeInfo = SecondaryText("Linha / destino indisponíveis");
        routeInfo.Margin = new Thickness(0d, 34d, 0d, 0d);
        operationBody.Children.Add(routeInfo);
        Grid.SetColumn(operation, 0);
        operationRow.Children.Add(operation);

        var metrics = new Grid();
        metrics.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        metrics.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16d) });
        metrics.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16d) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var speed = MetricCard("VELOCIDADE", "—");
        var schedule = MetricCard("HORÁRIO", "—");
        var status = Card("STATUS OPERACIONAL");
        ((StackPanel)status.Child).Children.Add(ValueText("Aguardando telemetria", 15d));
        var statusValue = ((StackPanel)status.Child).Children.OfType<TextBlock>().Last();
        Grid.SetColumn(speed.Card, 0);
        Grid.SetColumn(schedule.Card, 2);
        Grid.SetRow(status, 2);
        Grid.SetColumnSpan(status, 3);
        metrics.Children.Add(speed.Card);
        metrics.Children.Add(schedule.Card);
        metrics.Children.Add(status);
        Grid.SetColumn(metrics, 2);
        operationRow.Children.Add(metrics);
        stack.Children.Add(operationRow);

        var quick = Card("AÇÕES RÁPIDAS");
        quick.Margin = new Thickness(0d, 20d, 0d, 0d);
        var quickStack = (StackPanel)quick.Child;
        var actions = new WrapPanel { Margin = new Thickness(0d, 18d, 0d, 0d) };
        actions.Children.Add(ActionButton("Navegação", () => RaiseNavigation(window, "⌖")));
        actions.Children.Add(ActionButton("Multiplayer", () => RaiseNavigation(window, "●")));
        actions.Children.Add(ActionButton(
            L("Personagem / RP", "Character / RP", "Personaje / RP", "Charakter / RP", "Personnage / RP"),
            window.OpenRoleplayCharacterWindowForShell));
        actions.Children.Add(ActionButton("Abrir HUD", () => RaiseTaggedButton(window, "alpha12-hud-shortcut")));
        actions.Children.Add(ActionButton("Empresa", () => RaiseTaggedButton(window, "alpha12-company-fleet")));
        actions.Children.Add(ActionButton("CCO", () => RaiseTaggedButton(window, "alpha12-dispatcher")));
        quickStack.Children.Add(actions);
        stack.Children.Add(quick);

        StartHomeTelemetry(
            window,
            vehicle,
            omsiState,
            map,
            line,
            destination,
            nextStop,
            multiplayerState,
            multiplayerDetail,
            companyName,
            currentStop,
            street,
            routeInfo,
            speed.Value,
            schedule.Value,
            statusValue);
        return stack;
    }

    private static UIElement BuildNavigation(MainWindow window, Border navigationCard)
    {
        var stack = PageStack();
        stack.Children.Add(PageHeading("Navegação", "Rota ativa e orientação em tempo real."));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420d) });

        navigationCard.Margin = new Thickness(0d);
        navigationCard.Background = CardBrush();
        navigationCard.BorderBrush = BorderBrush();
        navigationCard.BorderThickness = new Thickness(1d);
        navigationCard.CornerRadius = new CornerRadius(14d);
        Grid.SetColumn(navigationCard, 0);
        grid.Children.Add(navigationCard);

        var route = Card("ROTA ATIVA");
        var body = (StackPanel)route.Child;
        body.Children.Add(SecondaryText("Estado da navegação"));
        var gps = BoundText(window.GpsStatusText, 16d, White(), FontWeights.SemiBold);
        gps.Margin = new Thickness(0d, 7d, 0d, 22d);
        gps.TextWrapping = TextWrapping.Wrap;
        body.Children.Add(gps);
        body.Children.Add(SecondaryText("Mapa atual"));
        var map = BoundText(window.MapValueText, 17d, White(), FontWeights.SemiBold);
        map.Margin = new Thickness(0d, 7d, 0d, 22d);
        body.Children.Add(map);
        body.Children.Add(SecondaryText("Velocidade"));
        var speed = BoundText(window.SpeedValueText, 30d, White(), FontWeights.Bold);
        speed.Margin = new Thickness(0d, 7d, 0d, 22d);
        body.Children.Add(speed);
        body.Children.Add(SecondaryText("Os dados de progresso e distância só aparecem quando calculados a partir da rota real do OMSI."));
        Grid.SetColumn(route, 2);
        grid.Children.Add(route);
        stack.Children.Add(grid);
        return stack;
    }

    private static UIElement BuildMultiplayer(MainWindow window)
    {
        var stack = PageStack();
        var heading = new Grid { Margin = new Thickness(0d, 0d, 0d, 20d) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        heading.Children.Add(PageHeading("Multiplayer", "Salas públicas, empresa, voz e sessão em segundo plano."));
        var bgStatus = Pill("SERVIDOR CONTINUA EM SEGUNDO PLANO", Accent());
        Grid.SetColumn(bgStatus, 1);
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        heading.Children.Add(bgStatus);
        stack.Children.Add(heading);

        var tabs = new Border
        {
            Height = 48d,
            Padding = new Thickness(8d, 6d, 8d, 6d),
            Background = ElevatedBrush(),
            BorderBrush = BorderBrush(),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d)
        };
        var tabStack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var text in new[] { "Salas", "Jogadores", "Chat", "Voz" })
        {
            var tab = new Border
            {
                Padding = new Thickness(22d, 8d, 22d, 8d),
                Margin = new Thickness(0d, 0d, 6d, 0d),
                Background = text == "Salas" ? Brush(16, 38, 56) : Brushes.Transparent,
                CornerRadius = new CornerRadius(8d),
                Child = Text(text, 12d, text == "Salas" ? White() : Muted(), FontWeights.SemiBold)
            };
            tabStack.Children.Add(tab);
        }
        tabs.Child = tabStack;
        stack.Children.Add(tabs);

        var body = new Grid { Margin = new Thickness(0d, 16d, 0d, 0d) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.7d, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16d) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var rooms = Card("SALAS");
        var roomsBody = (StackPanel)rooms.Child;
        roomsBody.Children.Add(SecondaryText("A Central Multiplayer continua sendo a fonte real das salas e compatibilidades."));
        var compatible = RoomPreview("Operação SP Noturna", "MAPA OBRIGATÓRIO", "SP Área 6 Sul", true);
        compatible.Margin = new Thickness(0d, 18d, 0d, 0d);
        roomsBody.Children.Add(compatible);
        var incompatible = RoomPreview("Sala incompatível", "MAPA NÃO ENCONTRADO", "Mapa necessário", false);
        incompatible.Margin = new Thickness(0d, 12d, 0d, 0d);
        roomsBody.Children.Add(incompatible);
        Grid.SetColumn(rooms, 0);
        body.Children.Add(rooms);

        var selected = Card("CENTRAL MULTIPLAYER");
        var selectedBody = (StackPanel)selected.Child;
        selectedBody.Children.Add(ValueText("Sessão e navegador de salas", 20d));
        selectedBody.Children.Add(SecondaryText("Mapa obrigatório, jogadores, chat, voz, compatibilidade e ônibus online ficam na Central."));
        window.MultiplayerButton.Content = "Abrir Central Multiplayer";
        window.MultiplayerButton.MinWidth = 220d;
        window.MultiplayerButton.Padding = new Thickness(18d, 10d, 18d, 10d);
        window.MultiplayerButton.Margin = new Thickness(0d, 24d, 0d, 0d);
        selectedBody.Children.Add(window.MultiplayerButton);
        selectedBody.Children.Add(SecondaryText("Fechar a Central não encerra host, voz nem telemetria."));
        Grid.SetColumn(selected, 2);
        body.Children.Add(selected);
        stack.Children.Add(body);
        return stack;
    }

    private static UIElement BuildHardware(MainWindow window)
    {
        var stack = PageStack();
        stack.Children.Add(PageHeading("Hardware", "Dispositivos, plugin OMSI e cockpit físico."));
        stack.Children.Add(new HardwareCockpitView(window.GetCurrentTelemetryForAlpha11));
        return stack;
    }

    private static UIElement BuildDiagnostics(Border diagnosticsCard)
    {
        var stack = PageStack();
        stack.Children.Add(PageHeading("Diagnóstico técnico", "Plugin, bridge, perfis OMSI e ferramentas avançadas."));
        diagnosticsCard.Margin = new Thickness(0d);
        stack.Children.Add(diagnosticsCard);
        return stack;
    }

    private static Border BuildSidebar(MainWindow window, ShellPages pages)
    {
        var dock = new DockPanel { Margin = new Thickness(16d, 18d, 14d, 14d) };
        var body = new StackPanel();
        var pageButtons = new List<Button>();

        var footer = BuildFooter(window);
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(footer);

        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false
        };
        dock.Children.Add(scroll);

        body.Children.Add(BuildBrand());
        body.Children.Add(Section("DIRIGIR"));
        var home = AddPageButton(body, "⌂  Início", pages.Home, pages, pageButtons);
        AddPageButton(body, "⌖  Navegação", pages.Navigation, pages, pageButtons);
        AddPageButton(body, "●  Multiplayer", pages.Multiplayer, pages, pageButtons);
        body.Children.Add(NavigationButton(
            L("♙  Personagem / RP", "♙  Character / RP", "♙  Personaje / RP", "♙  Charakter / RP", "♙  Personnage / RP"),
            window.OpenRoleplayCharacterWindowForShell));

        body.Children.Add(Separator());
        body.Children.Add(Section("OPERAÇÃO"));
        var operations = new StackPanel();
        window.RegisterName(Alpha12ProfessionalShellInstaller.OperationsPanelName, operations);
        body.Children.Add(operations);

        body.Children.Add(Separator());
        body.Children.Add(Section("SISTEMA"));
        var system = new StackPanel();
        window.RegisterName(Alpha12ProfessionalShellInstaller.SystemPanelName, system);
        AddPageButton(system, "▣  Hardware", pages.Hardware, pages, pageButtons);
        body.Children.Add(system);

        body.Children.Add(Separator());
        body.Children.Add(Section("AJUDA"));
        body.Children.Add(NavigationButton(GetManualButtonText(), () => new NavBRManualWindow { Owner = window }.ShowDialog()));

        var advanced = new Expander
        {
            Header = Text("⋯  Ferramentas avançadas", 11.5d, Muted(), FontWeights.SemiBold),
            IsExpanded = false,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0d, 8d, 0d, 0d)
        };
        var advancedBody = new StackPanel { Margin = new Thickness(0d, 8d, 0d, 0d) };
        advancedBody.Children.Add(AddStandalonePageButton("◫  Diagnóstico técnico", pages.Diagnostics, pages, pageButtons));
        var tools = new StackPanel();
        window.RegisterName(Alpha12ProfessionalShellInstaller.ToolsPanelName, tools);
        advancedBody.Children.Add(tools);
        advanced.Content = advancedBody;
        body.Children.Add(advanced);
        footer.Tag = advanced;

        ShowPage(pages.Home, pages, pageButtons, home);
        return new Border
        {
            Background = Brush(7, 18, 27),
            BorderBrush = BorderBrush(),
            BorderThickness = new Thickness(0d, 0d, 1d, 0d),
            Child = dock
        };
    }

    private static Border BuildTopbar(MainWindow window)
    {
        var grid = new Grid { Margin = new Thickness(28d, 0d, 28d, 0d) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var status = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        status.Children.Add(PillBound("OMSI", window.StatusText));
        var map = BoundText(window.MapValueText, 12d, Muted(), FontWeights.Medium);
        map.Margin = new Thickness(20d, 0d, 0d, 0d);
        map.VerticalAlignment = VerticalAlignment.Center;
        status.Children.Add(map);
        grid.Children.Add(status);

        var profile = DriverProfileStore.Load();
        var user = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        user.Children.Add(Text(profile.DisplayName, 13d, White(), FontWeights.SemiBold, new Thickness(0d, 0d, 14d, 0d)));
        var avatar = new Border
        {
            Width = 32d,
            Height = 32d,
            Background = ElevatedBrush(),
            BorderBrush = BorderBrush(),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(16d),
            Child = new TextBlock
            {
                Text = profile.DisplayName.Length > 0 ? profile.DisplayName[..1].ToUpperInvariant() : "N",
                Foreground = White(),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        user.Children.Add(avatar);
        Grid.SetColumn(user, 2);
        grid.Children.Add(user);

        return new Border
        {
            Height = 64d,
            Background = Brush(6, 16, 26),
            BorderBrush = BorderBrush(),
            BorderThickness = new Thickness(0d, 0d, 0d, 1d),
            Child = grid
        };
    }

    private static StackPanel BuildFooter(MainWindow window)
    {
        var stack = new StackPanel { Margin = new Thickness(0d, 8d, 0d, 0d) };
        stack.Children.Add(Separator());
        var advancedToggle = new CheckBox
        {
            Content = "Mostrar modo avançado",
            Foreground = Muted(),
            FontSize = 10.5d,
            Margin = new Thickness(4d, 0d, 0d, 12d)
        };
        advancedToggle.Checked += (_, _) =>
        {
            if (stack.Tag is Expander expander)
            {
                expander.Visibility = Visibility.Visible;
            }
        };
        advancedToggle.Unchecked += (_, _) =>
        {
            if (stack.Tag is Expander expander)
            {
                expander.IsExpanded = false;
                expander.Visibility = Visibility.Collapsed;
            }
        };
        stack.Children.Add(advancedToggle);

        window.LanguageLabelText.Margin = new Thickness(3d, 0d, 0d, 4d);
        stack.Children.Add(window.LanguageLabelText);
        window.LanguageComboBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        window.LanguageComboBox.Width = double.NaN;
        window.LanguageComboBox.Margin = new Thickness(0d, 0d, 0d, 8d);
        stack.Children.Add(window.LanguageComboBox);
        stack.Children.Add(NavigationButton("—  Minimizar para bandeja", () =>
        {
            if (Application.Current is App app)
            {
                app.TrayIcon.HideMainWindow();
            }
            else
            {
                window.WindowState = WindowState.Minimized;
            }
        }));
        return stack;
    }

    private static void StartHomeTelemetry(
        MainWindow window,
        TextBlock vehicle,
        TextBlock omsiState,
        TextBlock map,
        TextBlock line,
        TextBlock destination,
        TextBlock nextStop,
        TextBlock multiplayerState,
        TextBlock multiplayerDetail,
        TextBlock companyName,
        TextBlock currentStop,
        TextBlock street,
        TextBlock routeInfo,
        TextBlock speed,
        TextBlock schedule,
        TextBlock status)
    {
        void Refresh()
        {
            var telemetry = window.GetCurrentTelemetryForAlpha11();
            var profile = DriverProfileStore.Load();
            companyName.Text = profile.CompanyName ?? L("Sem empresa", "No company", "Sin empresa", "Kein Unternehmen", "Aucune entreprise");

            if (telemetry is null || !telemetry.IsInGame)
            {
                vehicle.Text = "—";
                omsiState.Text = string.IsNullOrWhiteSpace(window.StatusText.Text)
                    ? L("Aguardando OMSI", "Waiting for OMSI", "Esperando OMSI", "Warte auf OMSI", "En attente d’OMSI")
                    : window.StatusText.Text;
                map.Text = $"{L("Mapa", "Map", "Mapa", "Karte", "Carte")} • {Safe(window.MapValueText.Text)}";
                line.Text = "—";
                destination.Text = "—";
                nextStop.Text = $"{L("Próxima parada", "Next stop", "Próxima parada", "Nächste Haltestelle", "Prochain arrêt")} • —";
                currentStop.Text = "—";
                street.Text = $"{L("Rua atual", "Current street", "Calle actual", "Aktuelle Straße", "Rue actuelle")} • —";
                routeInfo.Text = L("Linha / destino indisponíveis", "Line / destination unavailable", "Línea / destino no disponibles", "Linie / Ziel nicht verfügbar", "Ligne / destination indisponibles");
                speed.Text = "—";
                schedule.Text = "—";
                status.Text = L("Aguardando telemetria", "Waiting for telemetry", "Esperando telemetría", "Warte auf Telemetrie", "En attente de télémétrie");
            }
            else
            {
                vehicle.Text = Safe(telemetry.VehicleName);
                omsiState.Text = L("Conectado ao jogo", "Connected to game", "Conectado al juego", "Mit dem Spiel verbunden", "Connecté au jeu");
                map.Text = $"{L("Mapa", "Map", "Mapa", "Karte", "Carte")} • {Safe(telemetry.MapName)}";
                line.Text = Safe(telemetry.Line);
                destination.Text = Safe(telemetry.DestinationName ?? telemetry.Route);
                nextStop.Text = $"{L("Próxima parada", "Next stop", "Próxima parada", "Nächste Haltestelle", "Prochain arrêt")} • {Safe(telemetry.NextStopName)}";
                currentStop.Text = Safe(telemetry.NextStopName);
                street.Text = $"{L("Rua atual", "Current street", "Calle actual", "Aktuelle Straße", "Rue actuelle")} • {Safe(telemetry.CurrentStreetName)}";
                routeInfo.Text = $"{L("Linha", "Line", "Línea", "Linie", "Ligne")} {Safe(telemetry.Line)} • {Safe(telemetry.DestinationName ?? telemetry.Route)}";
                speed.Text = $"{telemetry.SpeedKph:0} km/h";
                schedule.Text = FormatDelay(telemetry.DelaySeconds);
                status.Text = telemetry.Timestamp < DateTimeOffset.UtcNow.AddSeconds(-5)
                    ? L("Telemetria desatualizada", "Telemetry stale", "Telemetría desactualizada", "Telemetrie veraltet", "Télémétrie obsolète")
                    : L("Telemetria atualizada", "Telemetry current", "Telemetría actualizada", "Telemetrie aktuell", "Télémétrie à jour");
            }

            var multiplayer = Application.Current?.Windows.OfType<MultiplayerWindow>().FirstOrDefault();
            var active = multiplayer?.HasBackgroundSession == true;
            multiplayerState.Text = active
                ? L("Sessão ativa", "Active session", "Sesión activa", "Aktive Sitzung", "Session active")
                : L("Sem sessão", "No session", "Sin sesión", "Keine Sitzung", "Aucune session");
            multiplayerDetail.Text = active
                ? L("Host/conexão mantidos em segundo plano", "Host/connection kept in background", "Host/conexión mantenidos en segundo plano", "Host/Verbindung läuft im Hintergrund", "Hôte/connexion maintenus en arrière-plan")
                : L("Abra a Central Multiplayer", "Open the Multiplayer Center", "Abra la Central Multiplayer", "Multiplayer-Zentrale öffnen", "Ouvrez la centrale multijoueur");
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500d) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Refresh();
        window.Closed += (_, _) => timer.Stop();
    }

    private static string L(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };

    private static string FormatDelay(int? seconds)
    {
        if (!seconds.HasValue)
        {
            return "—";
        }
        var sign = seconds.Value > 0 ? "+" : seconds.Value < 0 ? "-" : string.Empty;
        var value = Math.Abs(seconds.Value);
        return $"{sign}{value / 60:00}:{value % 60:00}";
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static Border PageHeading(string title, string subtitle)
    {
        var stack = new StackPanel();
        stack.Children.Add(Text(title, 30d, White(), FontWeights.Bold));
        stack.Children.Add(Text(subtitle, 13d, Muted(), FontWeights.Normal, new Thickness(0d, 6d, 0d, 0d)));
        return new Border { Margin = new Thickness(0d, 0d, 0d, 24d), Child = stack };
    }

    private static Border Card(string title)
    {
        var stack = new StackPanel();
        stack.Children.Add(Text(title, 13d, White(), FontWeights.SemiBold));
        return new Border
        {
            Padding = new Thickness(18d),
            Background = CardBrush(),
            BorderBrush = BorderBrush(),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(14d),
            Child = stack
        };
    }

    private static void AddGridCard(Grid grid, int column, string title, params UIElement[] elements)
    {
        var card = Card(title);
        card.Margin = new Thickness(8d, 0d, 8d, 0d);
        var body = (StackPanel)card.Child;
        foreach (var element in elements)
        {
            if (element is FrameworkElement framework)
            {
                framework.Margin = new Thickness(0d, 9d, 0d, 0d);
            }
            body.Children.Add(element);
        }
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
    }

    private static (Border Card, TextBlock Value) MetricCard(string title, string initial)
    {
        var card = Card(title);
        var value = ValueText(initial, 34d);
        value.Margin = new Thickness(0d, 20d, 0d, 0d);
        ((StackPanel)card.Child).Children.Add(value);
        return (card, value);
    }

    private static Border RoomPreview(string title, string label, string map, bool compatible)
    {
        var body = new StackPanel();
        body.Children.Add(Text(title, 16d, White(), FontWeights.SemiBold));
        body.Children.Add(Text(label, 10d, compatible ? Success() : Error(), FontWeights.Bold, new Thickness(0d, 14d, 0d, 0d)));
        body.Children.Add(Text(map, 13d, White(), FontWeights.Medium, new Thickness(0d, 5d, 0d, 0d)));
        body.Children.Add(Text(compatible ? "Compatibilidade confirmada na Central" : "Entrada bloqueada enquanto incompatível", 11d, Muted(), FontWeights.Normal, new Thickness(0d, 10d, 0d, 0d)));
        return new Border
        {
            Padding = new Thickness(16d),
            Background = ElevatedBrush(),
            BorderBrush = compatible ? Brush(56, 201, 140) : Brush(239, 91, 100),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = body
        };
    }

    private static Border Pill(string text, Brush border)
    {
        return new Border
        {
            Padding = new Thickness(12d, 6d, 12d, 6d),
            BorderBrush = border,
            BorderThickness = new Thickness(1d),
            Background = ElevatedBrush(),
            CornerRadius = new CornerRadius(14d),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Text(text, 10d, White(), FontWeights.SemiBold)
        };
    }

    private static Border PillBound(string label, TextBlock source)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(Text(label, 10d, Accent(), FontWeights.Bold, new Thickness(0d, 0d, 8d, 0d)));
        stack.Children.Add(BoundText(source, 10d, White(), FontWeights.Medium));
        return new Border
        {
            Padding = new Thickness(12d, 6d, 12d, 6d),
            BorderBrush = BorderBrush(),
            BorderThickness = new Thickness(1d),
            Background = ElevatedBrush(),
            CornerRadius = new CornerRadius(14d),
            VerticalAlignment = VerticalAlignment.Center,
            Child = stack
        };
    }

    private static Button AddPageButton(Panel panel, string text, FrameworkElement page, ShellPages pages, List<Button> buttons)
    {
        Button? button = null;
        button = NavigationButton(text, () => ShowPage(page, pages, buttons, button));
        buttons.Add(button);
        panel.Children.Add(button);
        return button;
    }

    private static Button AddStandalonePageButton(string text, FrameworkElement page, ShellPages pages, List<Button> buttons)
    {
        Button? button = null;
        button = NavigationButton(text, () => ShowPage(page, pages, buttons, button));
        buttons.Add(button);
        return button;
    }

    private static void ShowPage(FrameworkElement page, ShellPages pages, IReadOnlyList<Button> buttons, Button? selected)
    {
        foreach (var candidate in pages.All)
        {
            candidate.Visibility = ReferenceEquals(candidate, page) ? Visibility.Visible : Visibility.Hidden;
        }
        foreach (var button in buttons)
        {
            StyleNavigationButton(button, false);
        }
        if (selected is not null)
        {
            StyleNavigationButton(selected, true);
        }
    }

    private static void RaiseNavigation(MainWindow window, string prefix)
    {
        var button = Enumerate<Button>(window).FirstOrDefault(candidate => candidate.Content is string text && text.StartsWith(prefix, StringComparison.Ordinal));
        button?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static void RaiseTaggedButton(MainWindow window, string tag)
    {
        var button = Enumerate<Button>(window).FirstOrDefault(candidate => Equals(candidate.Tag, tag));
        button?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = SecondaryButton(text);
        button.Margin = new Thickness(0d, 0d, 12d, 10d);
        button.Click += (_, _) => action();
        return button;
    }

    private static Button NavigationButton(string text, Action action)
    {
        var button = new Button { Content = text };
        StyleNavigationButton(button, false);
        button.Click += (_, _) => action();
        return button;
    }

    private static void StyleNavigationButton(Button button, bool selected)
    {
        button.Height = 40d;
        button.Margin = new Thickness(0d, 0d, 0d, 6d);
        button.Padding = new Thickness(16d, 9d, 12d, 9d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Background = selected ? Brush(16, 38, 56) : Brushes.Transparent;
        button.Foreground = selected ? White() : Muted();
        button.BorderBrush = selected ? Accent() : Brushes.Transparent;
        button.BorderThickness = selected ? new Thickness(3d, 0d, 0d, 0d) : new Thickness(0d);
        button.FontSize = 12.5d;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static Button PrimaryButton(string text) => StyledButton(text, Brush(61, 137, 196), White());
    private static Button SecondaryButton(string text) => StyledButton(text, ElevatedBrush(), White());

    private static Button StyledButton(string text, Brush background, Brush foreground)
    {
        return new Button
        {
            Content = text,
            MinHeight = 40d,
            Padding = new Thickness(16d, 9d, 16d, 9d),
            Background = background,
            Foreground = foreground,
            BorderBrush = BorderBrush(),
            BorderThickness = new Thickness(1d),
            FontSize = 12.5d,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private static UIElement BuildBrand()
    {
        var stack = new StackPanel { Margin = new Thickness(12d, 2d, 4d, 28d) };
        stack.Children.Add(Text("NAVBR", 22d, White(), FontWeights.Bold));
        stack.Children.Add(Text("OMSI MULTIPLAYER", 9d, Accent(), FontWeights.Bold, new Thickness(0d, 2d, 0d, 0d)));
        return stack;
    }

    private static TextBlock Section(string text) => Text(text, 9d, Brush(96, 113, 125), FontWeights.Bold, new Thickness(12d, 0d, 0d, 10d));

    private static Border Separator() => new()
    {
        Height = 1d,
        Margin = new Thickness(0d, 10d, 0d, 14d),
        Background = BorderBrush()
    };

    private static StackPanel PageStack() => new() { Margin = new Thickness(40d, 32d, 40d, 40d) };

    private static ScrollViewer NewPage() => new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Background = Brushes.Transparent,
        CanContentScroll = false
    };

    private static TextBlock ValueText(string text, double size) => Text(text, size, White(), FontWeights.SemiBold);
    private static TextBlock SecondaryText(string text) => Text(text, 12d, Muted(), FontWeights.Normal);

    private static TextBlock Text(string text, double size, Brush foreground, FontWeight weight, Thickness? margin = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = foreground,
            FontWeight = weight,
            Margin = margin ?? new Thickness(0d),
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static TextBlock BoundText(TextBlock source, double size, Brush foreground, FontWeight weight)
    {
        var target = Text(string.Empty, size, foreground, weight);
        target.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = source });
        return target;
    }

    private static void NormalizeCard(FrameworkElement card)
    {
        card.Margin = new Thickness(0d);
        card.HorizontalAlignment = HorizontalAlignment.Stretch;
        card.VerticalAlignment = VerticalAlignment.Top;
    }

    private static string GetManualButtonText() => LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
    {
        "de" => "?  Benutzerhandbuch",
        "fr" => "?  Manuel d’utilisation",
        "en" => "?  User manual",
        _ => "?  Manual de uso"
    };

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            if (current is T match)
            {
                return match;
            }
        }
        return null;
    }

    private static void Detach(UIElement element)
    {
        var parent = VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        switch (parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, element):
                decorator.Child = null;
                break;
            case ContentControl content when ReferenceEquals(content.Content, element):
                content.Content = null;
                break;
        }
    }

    private static IEnumerable<T> Enumerate<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            foreach (var child in Enumerate<T>(VisualTreeHelper.GetChild(root, i)))
            {
                yield return child;
            }
        }
    }

    private static SolidColorBrush CardBrush() => Brush(10, 19, 26);
    private static SolidColorBrush ElevatedBrush() => Brush(13, 26, 36);
    private static SolidColorBrush BorderBrush() => Brush(28, 42, 51);
    private static SolidColorBrush Accent() => Brush(113, 198, 255);
    private static SolidColorBrush White() => Brush(218, 230, 238);
    private static SolidColorBrush Muted() => Brush(151, 171, 185);
    private static SolidColorBrush Success() => Brush(56, 201, 140);
    private static SolidColorBrush Error() => Brush(239, 91, 100);
    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private sealed record ShellPages(
        ScrollViewer Home,
        ScrollViewer Navigation,
        ScrollViewer Multiplayer,
        ScrollViewer Hardware,
        ScrollViewer Diagnostics)
    {
        public IReadOnlyList<FrameworkElement> All { get; } = new FrameworkElement[] { Home, Navigation, Multiplayer, Hardware, Diagnostics };
    }
}
