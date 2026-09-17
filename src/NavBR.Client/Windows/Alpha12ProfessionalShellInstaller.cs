using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Hardware;
using NavBR.Client.Localization;

namespace NavBR.Client.Windows;

internal static class Alpha12ProfessionalShellInstaller
{
    internal const string ToolsPanelName = "Alpha11ToolsPanel";
    internal const string OperationsPanelName = "Alpha12OperationsMenuPanel";
    internal const string SystemPanelName = "Alpha12SystemMenuPanel";

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
            return;
        }

        DetachFromParent(statusCard);
        DetachFromParent(telemetryCard);
        DetachFromParent(navigationCard);
        DetachFromParent(diagnosticsCard);
        DetachFromParent(window.MultiplayerButton);
        DetachFromParent(window.LanguageLabelText);
        DetachFromParent(window.LanguageComboBox);
        window.Content = null;

        window.Width = Math.Max(window.Width, 1380d);
        window.Height = Math.Max(window.Height, 860d);
        window.MinWidth = 1060d;
        window.MinHeight = 700d;

        NormalizeWorkspaceCard(statusCard);
        NormalizeWorkspaceCard(telemetryCard);
        NormalizeWorkspaceCard(navigationCard);
        NormalizeWorkspaceCard(diagnosticsCard);

        var headerTitle = new TextBlock
        {
            Text = "Visão geral",
            Foreground = Brushes.White,
            FontSize = 21d,
            FontWeight = FontWeights.SemiBold
        };
        var headerSubtitle = new TextBlock
        {
            Text = "Acompanhe sua operação sem perder tempo com detalhes técnicos.",
            Foreground = Brush(132, 150, 163),
            FontSize = 11d,
            Margin = new Thickness(0d, 3d, 0d, 0d)
        };

        var host = new Grid();
        var pages = BuildPages(window, host);
        pages.Home.Content = BuildHomePage(statusCard, telemetryCard);
        pages.Navigation.Content = BuildNavigationPage(navigationCard);
        pages.Multiplayer.Content = BuildMultiplayerPage(window);
        pages.Hardware.Content = BuildHardwarePage(window);
        pages.Diagnostics.Content = BuildDiagnosticsPage(diagnosticsCard);

        var root = new Grid { Background = Brush(5, 10, 15) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(276d) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var sidebar = BuildSidebar(window, pages, headerTitle, headerSubtitle);
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        var workspace = new Grid { Background = Brush(7, 12, 18) };
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var topbar = BuildTopbar(headerTitle, headerSubtitle);
        Grid.SetRow(topbar, 0);
        workspace.Children.Add(topbar);

        Grid.SetRow(host, 1);
        workspace.Children.Add(host);

        Grid.SetColumn(workspace, 1);
        root.Children.Add(workspace);
        window.Content = root;
    }

    private static ShellPages BuildPages(MainWindow window, Grid host)
    {
        var home = NewPage();
        var navigation = NewPage();
        var multiplayer = NewPage();
        var hardware = NewPage();
        var diagnostics = NewPage();

        host.Children.Add(home);
        host.Children.Add(navigation);
        host.Children.Add(multiplayer);
        host.Children.Add(hardware);
        host.Children.Add(diagnostics);

        navigation.Visibility = Visibility.Hidden;
        multiplayer.Visibility = Visibility.Hidden;
        hardware.Visibility = Visibility.Hidden;
        diagnostics.Visibility = Visibility.Hidden;

        return new ShellPages(home, navigation, multiplayer, hardware, diagnostics);
    }

    private static UIElement BuildHomePage(Border statusCard, Border telemetryCard)
    {
        var stack = NewPageStack();
        stack.Children.Add(BuildHero());

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16d) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        statusCard.Margin = new Thickness(0d);
        Grid.SetColumn(statusCard, 0);
        grid.Children.Add(statusCard);

        telemetryCard.Margin = new Thickness(0d);
        Grid.SetColumn(telemetryCard, 2);
        grid.Children.Add(telemetryCard);
        stack.Children.Add(grid);

        stack.Children.Add(BuildInformationCard(
            "Fluxo simples",
            "Abra o OMSI, carregue o mapa e o ônibus e use Navegação ou Multiplayer. Diagnósticos e ferramentas de manutenção ficam separados no modo avançado.",
            "OPERAÇÃO PRIMEIRO • TÉCNICO QUANDO PRECISAR"));
        return stack;
    }

    private static UIElement BuildNavigationPage(Border navigationCard)
    {
        var stack = NewPageStack();
        stack.Children.Add(BuildInformationCard(
            "GPS NavBR",
            "Mapa 2D/3D, rota ativa, progresso, próxima parada e jogadores online usam a mesma base de telemetria.",
            "2D / 3D • ROTA • PARADAS • MULTIPLAYER"));
        navigationCard.Margin = new Thickness(0d);
        stack.Children.Add(navigationCard);
        return stack;
    }

    private static UIElement BuildMultiplayerPage(MainWindow window)
    {
        var stack = NewPageStack();

        var actions = new Grid();
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16d) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var create = BuildInformationCard(
            "Criar sala",
            "Seu computador hospeda a sessão. A Central continua ativa em segundo plano mesmo quando a janela é recolhida.",
            "PEER-HOST • TCP 27730 • RELAY OPCIONAL");
        Grid.SetColumn(create, 0);
        actions.Children.Add(create);

        var join = BuildInformationCard(
            "Entrar em uma sala",
            "Confira mapa obrigatório e compatibilidade antes de entrar. Depois acompanhe jogadores, voz, CCO e ônibus 3D.",
            "MAPA OBRIGATÓRIO • COMPATIBILIDADE • 3D");
        Grid.SetColumn(join, 2);
        actions.Children.Add(join);
        stack.Children.Add(actions);

        var central = new Border
        {
            Margin = new Thickness(0d, 16d, 0d, 0d),
            Padding = new Thickness(20d),
            Background = Brush(11, 20, 28),
            BorderBrush = Brush(31, 52, 67),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(14d)
        };
        var centralGrid = new Grid();
        centralGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        centralGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        centralGrid.Children.Add(new TextBlock
        {
            Text = "Central Multiplayer — sala, jogadores, chat, voz, rede e operação em um único painel.",
            Foreground = Brush(190, 204, 214),
            FontSize = 12d,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 18d, 0d)
        });

        window.MultiplayerButton.Content = "Abrir Central Multiplayer";
        window.MultiplayerButton.MinWidth = 220d;
        window.MultiplayerButton.Padding = new Thickness(18d, 10d, 18d, 10d);
        window.MultiplayerButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(window.MultiplayerButton, 1);
        centralGrid.Children.Add(window.MultiplayerButton);
        central.Child = centralGrid;
        stack.Children.Add(central);
        return stack;
    }

    private static UIElement BuildHardwarePage(MainWindow window)
    {
        var stack = NewPageStack();
        stack.Children.Add(BuildInformationCard(
            "Hardware cockpit",
            "Conecte Arduino ou ESP32 e leve dados do OMSI para um painel físico sem misturar esta configuração com a operação diária.",
            "SERIAL • ARDUINO • ESP32"));
        stack.Children.Add(new HardwareCockpitView(window.GetCurrentTelemetryForAlpha11));
        return stack;
    }

    private static UIElement BuildDiagnosticsPage(Border diagnosticsCard)
    {
        var stack = NewPageStack();
        stack.Children.Add(BuildInformationCard(
            "Diagnóstico técnico",
            "Área de manutenção do plugin, bridge, perfis OMSI e ferramentas experimentais. Não interfere na navegação normal do aplicativo.",
            "MODO AVANÇADO"));
        diagnosticsCard.Margin = new Thickness(0d);
        stack.Children.Add(diagnosticsCard);
        return stack;
    }

    private static Border BuildSidebar(
        MainWindow window,
        ShellPages pages,
        TextBlock title,
        TextBlock subtitle)
    {
        var dock = new DockPanel { Margin = new Thickness(16d, 18d, 14d, 14d) };
        var body = new StackPanel();
        var pageButtons = new List<Button>();

        var footer = BuildFooter(window);
        DockPanel.SetDock(footer, Dock.Bottom);
        dock.Children.Add(footer);

        var scroller = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            PanningMode = PanningMode.VerticalOnly
        };
        dock.Children.Add(scroller);

        body.Children.Add(BuildBrand());
        body.Children.Add(Section("DIRIGIR"));
        var home = AddPageButton(body, "⌂  Início", "Visão geral", "Acompanhe sua operação sem perder tempo com detalhes técnicos.", pages.Home, pages, pageButtons, title, subtitle);
        AddPageButton(body, "⌖  Navegação", "Navegação", "GPS, mapa 2D/3D, rota, paradas e progresso da viagem.", pages.Navigation, pages, pageButtons, title, subtitle);
        AddPageButton(body, "●  Multiplayer", "Multiplayer", "Salas, jogadores, compatibilidade, chat, voz e ônibus online.", pages.Multiplayer, pages, pageButtons, title, subtitle);

        body.Children.Add(Separator());
        body.Children.Add(Section("OPERAÇÃO"));
        var operations = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 2d) };
        window.RegisterName(OperationsPanelName, operations);
        body.Children.Add(operations);

        body.Children.Add(Separator());
        body.Children.Add(Section("SISTEMA"));
        var system = new StackPanel();
        window.RegisterName(SystemPanelName, system);
        AddPageButton(system, "▣  Hardware", "Hardware cockpit", "Painéis físicos, Arduino, ESP32 e dispositivos seriais.", pages.Hardware, pages, pageButtons, title, subtitle);
        body.Children.Add(system);

        body.Children.Add(Separator());
        body.Children.Add(Section("AJUDA"));
        body.Children.Add(NavigationButton(GetManualButtonText(), () =>
        {
            new NavBRManualWindow { Owner = window }.ShowDialog();
        }));

        var advanced = new Expander
        {
            Header = new TextBlock
            {
                Text = "⋯  Ferramentas avançadas",
                Foreground = Brush(174, 192, 204),
                FontSize = 11.5d,
                FontWeight = FontWeights.SemiBold
            },
            IsExpanded = false,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0d, 8d, 0d, 0d)
        };
        var advancedContent = new StackPanel { Margin = new Thickness(0d, 8d, 0d, 0d) };
        advancedContent.Children.Add(NavigationButton("◫  Diagnóstico técnico", () =>
            ShowPage(pages.Diagnostics, pages, pageButtons, null, title, subtitle,
                "Diagnóstico técnico", "Plugin, bridge e ferramentas de manutenção.")));
        var tools = new StackPanel();
        window.RegisterName(ToolsPanelName, tools);
        advancedContent.Children.Add(tools);
        advanced.Content = advancedContent;
        body.Children.Add(advanced);

        footer.Tag = advanced;
        ShowPage(pages.Home, pages, pageButtons, home, title, subtitle,
            "Visão geral", "Acompanhe sua operação sem perder tempo com detalhes técnicos.");

        return new Border
        {
            Background = Brush(8, 15, 21),
            BorderBrush = Brush(25, 39, 49),
            BorderThickness = new Thickness(0d, 0d, 1d, 0d),
            Child = dock
        };
    }

    private static StackPanel BuildFooter(MainWindow window)
    {
        var stack = new StackPanel { Margin = new Thickness(0d, 10d, 0d, 0d) };
        stack.Children.Add(Separator());

        var advancedToggle = new CheckBox
        {
            Content = "Mostrar modo avançado",
            Foreground = Brush(172, 188, 199),
            FontSize = 10.5d,
            Margin = new Thickness(4d, 0d, 0d, 12d),
            Cursor = System.Windows.Input.Cursors.Hand
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
        window.LanguageComboBox.Width = double.NaN;
        window.LanguageComboBox.HorizontalAlignment = HorizontalAlignment.Stretch;
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

    private static Border BuildTopbar(TextBlock title, TextBlock subtitle)
    {
        var grid = new Grid { Margin = new Thickness(30d, 21d, 30d, 18d) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titles = new StackPanel();
        titles.Children.Add(title);
        titles.Children.Add(subtitle);
        grid.Children.Add(titles);

        var badges = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        badges.Children.Add(StatusBadge("OMSI", "AUTO"));
        badges.Children.Add(StatusBadge("NAVBR", "ALPHA.12 • DEV"));
        Grid.SetColumn(badges, 1);
        grid.Children.Add(badges);

        return new Border
        {
            Background = Brush(8, 15, 21),
            BorderBrush = Brush(25, 39, 49),
            BorderThickness = new Thickness(0d, 0d, 0d, 1d),
            Child = grid
        };
    }

    private static Border BuildHero()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "OMSI NavBR Multiplayer",
            Foreground = Brushes.White,
            FontSize = 26d,
            FontWeight = FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Navegação, operação e multiplayer organizados para você dirigir sem complicação.",
            Foreground = Brush(183, 198, 208),
            FontSize = 12.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 7d, 0d, 0d)
        });
        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 18d),
            Padding = new Thickness(22d),
            Background = new LinearGradientBrush(Color.FromRgb(16, 31, 42), Color.FromRgb(10, 18, 25), 0d),
            BorderBrush = Brush(35, 58, 73),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(15d),
            Child = stack
        };
    }

    private static Border BuildInformationCard(string title, string body, string status)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontSize = 14d, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(new TextBlock { Text = body, Foreground = Brush(153, 171, 184), FontSize = 11d, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0d, 7d, 0d, 12d) });
        stack.Children.Add(new TextBlock { Text = status, Foreground = Brush(75, 170, 255), FontSize = 9.5d, FontWeight = FontWeights.Bold });
        return new Border
        {
            Margin = new Thickness(0d, 16d, 0d, 0d),
            Padding = new Thickness(18d),
            Background = Brush(10, 19, 26),
            BorderBrush = Brush(29, 47, 59),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(13d),
            Child = stack
        };
    }

    private static Border StatusBadge(string title, string value)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, Foreground = Brush(120, 140, 154), FontSize = 8d, FontWeight = FontWeights.Bold });
        stack.Children.Add(new TextBlock { Text = value, Foreground = Brushes.White, FontSize = 9.5d, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0d, 1d, 0d, 0d) });
        return new Border
        {
            Margin = new Thickness(8d, 0d, 0d, 0d),
            Padding = new Thickness(11d, 6d, 11d, 6d),
            Background = Brush(12, 22, 29),
            BorderBrush = Brush(34, 51, 62),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(8d),
            Child = stack
        };
    }

    private static UIElement BuildBrand()
    {
        var stack = new StackPanel { Margin = new Thickness(4d, 2d, 4d, 23d) };
        stack.Children.Add(new TextBlock { Text = "NavBR", Foreground = Brushes.White, FontSize = 25d, FontWeight = FontWeights.Bold });
        stack.Children.Add(new TextBlock { Text = "OMSI MULTIPLAYER", Foreground = Brush(75, 170, 255), FontSize = 9d, FontWeight = FontWeights.Bold, Margin = new Thickness(0d, 2d, 0d, 0d) });
        stack.Children.Add(new TextBlock { Text = "Centro de operação do motorista", Foreground = Brush(102, 123, 138), FontSize = 9.5d, Margin = new Thickness(0d, 6d, 0d, 0d) });
        return stack;
    }

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        Margin = new Thickness(4d, 0d, 0d, 8d),
        Foreground = Brush(94, 117, 133),
        FontSize = 8.5d,
        FontWeight = FontWeights.Bold
    };

    private static Border Separator() => new()
    {
        Height = 1d,
        Margin = new Thickness(0d, 10d, 0d, 14d),
        Background = Brush(27, 42, 51)
    };

    private static Button AddPageButton(
        Panel panel,
        string text,
        string pageTitle,
        string pageSubtitle,
        FrameworkElement page,
        ShellPages pages,
        List<Button> buttons,
        TextBlock title,
        TextBlock subtitle)
    {
        Button? button = null;
        button = NavigationButton(text, () => ShowPage(page, pages, buttons, button, title, subtitle, pageTitle, pageSubtitle));
        buttons.Add(button);
        panel.Children.Add(button);
        return button;
    }

    private static void ShowPage(
        FrameworkElement page,
        ShellPages pages,
        IReadOnlyList<Button> buttons,
        Button? selected,
        TextBlock title,
        TextBlock subtitle,
        string pageTitle,
        string pageSubtitle)
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
        title.Text = pageTitle;
        subtitle.Text = pageSubtitle;
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
        button.Height = 44d;
        button.Margin = new Thickness(0d, 0d, 0d, 6d);
        button.Padding = new Thickness(selected ? 14d : 13d, 9d, 13d, 9d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Background = selected ? Brush(18, 37, 51) : Brushes.Transparent;
        button.Foreground = selected ? Brushes.White : Brush(201, 216, 226);
        button.BorderBrush = selected ? Brush(66, 169, 255) : Brushes.Transparent;
        button.BorderThickness = selected ? new Thickness(3d, 0d, 0d, 0d) : new Thickness(0d);
        button.FontSize = 12.5d;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static ScrollViewer NewPage() => new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        CanContentScroll = false,
        Background = Brushes.Transparent
    };

    private static StackPanel NewPageStack() => new() { Margin = new Thickness(30d, 24d, 30d, 30d) };

    private static void NormalizeWorkspaceCard(FrameworkElement card)
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

    private static void DetachFromParent(UIElement element)
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
