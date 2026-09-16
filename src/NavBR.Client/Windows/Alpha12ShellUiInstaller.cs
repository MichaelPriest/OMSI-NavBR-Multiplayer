using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Hardware;
using NavBR.Client.Localization;
using NavBR.Shared.Features;

namespace NavBR.Client.Windows;

internal static class Alpha12ShellUiInstaller
{
    // Kept for compatibility with the existing OMSI tools installer while the
    // Alpha.12 shell is being migrated module by module.
    private const string ToolsPanelName = "Alpha11ToolsPanel";
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
        window.Content = null;

        window.Width = Math.Max(window.Width, 1320d);
        window.Height = Math.Max(window.Height, 840d);
        window.MinWidth = 1000d;
        window.MinHeight = 680d;

        PrepareWorkspaceCard(statusCard, new Thickness(0d, 0d, 0d, 14d));
        PrepareWorkspaceCard(telemetryCard, new Thickness(0d));
        PrepareWorkspaceCard(navigationCard, new Thickness(0d));
        PrepareWorkspaceCard(diagnosticsCard, new Thickness(0d));

        var pages = BuildPages(window, statusCard, telemetryCard, navigationCard, diagnosticsCard);

        var root = new Grid
        {
            Background = Brush(6, 10, 14)
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(248d) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var sidebar = BuildSidebar(window, pages);
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        var workspace = new Grid
        {
            Background = Brush(6, 10, 14)
        };
        workspace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        var header = BuildWorkspaceHeader();
        Grid.SetRow(header, 0);
        workspace.Children.Add(header);

        Grid.SetRow(pages.Host, 1);
        workspace.Children.Add(pages.Host);

        Grid.SetColumn(workspace, 1);
        root.Children.Add(workspace);
        window.Content = root;
    }

    private static ShellPages BuildPages(
        MainWindow window,
        Border statusCard,
        Border telemetryCard,
        Border navigationCard,
        Border diagnosticsCard)
    {
        var homeStack = new StackPanel { Margin = new Thickness(28d, 22d, 28d, 28d) };
        homeStack.Children.Add(BuildWelcomeCard());
        homeStack.Children.Add(BuildSectionTitle(
            "Seu ônibus agora",
            "O NavBR deixa os detalhes técnicos em segundo plano e mostra primeiro o que você precisa para dirigir."));
        homeStack.Children.Add(statusCard);
        homeStack.Children.Add(telemetryCard);
        homeStack.Children.Add(BuildQuickHelpCard());

        var navigationStack = new StackPanel { Margin = new Thickness(28d, 22d, 28d, 28d) };
        navigationStack.Children.Add(BuildSectionTitle(
            "Navegação",
            "Mapa, rota, paradas e acompanhamento da viagem em um único lugar."));
        navigationStack.Children.Add(navigationCard);

        var multiplayerStack = new StackPanel { Margin = new Thickness(28d, 22d, 28d, 28d) };
        multiplayerStack.Children.Add(BuildSectionTitle(
            "Multiplayer",
            "Jogue com outras pessoas sem precisar entender a parte técnica da rede."));
        multiplayerStack.Children.Add(BuildMultiplayerLanding(window));

        var hardwareStack = new StackPanel { Margin = new Thickness(28d, 22d, 28d, 28d) };
        hardwareStack.Children.Add(BuildSectionTitle(
            "Hardware cockpit",
            "Conecte Arduino ou ESP32 e leve informações do OMSI para um painel físico."));
        hardwareStack.Children.Add(new HardwareCockpitView(window.GetCurrentTelemetryForAlpha11));

        var featuresStack = new StackPanel { Margin = new Thickness(28d, 22d, 28d, 28d) };
        featuresStack.Children.Add(BuildSectionTitle(
            "Alpha.12",
            "Todas as frentes da próxima versão em uma visão simples. Recursos experimentais continuam protegidos por opt-in."));
        featuresStack.Children.Add(BuildFeatureOverview());

        var diagnosticsStack = new StackPanel { Margin = new Thickness(28d, 22d, 28d, 28d) };
        diagnosticsStack.Children.Add(BuildSectionTitle(
            "Diagnóstico e ferramentas",
            "Área técnica para manutenção, plugin, perfis OMSI, Roadmap Studio e testes experimentais."));
        diagnosticsStack.Children.Add(diagnosticsCard);

        var homePage = NewPage(homeStack);
        var navigationPage = NewPage(navigationStack);
        var multiplayerPage = NewPage(multiplayerStack);
        var hardwarePage = NewPage(hardwareStack);
        var featuresPage = NewPage(featuresStack);
        var diagnosticsPage = NewPage(diagnosticsStack);

        var host = new Grid();
        host.Children.Add(homePage);
        host.Children.Add(navigationPage);
        host.Children.Add(multiplayerPage);
        host.Children.Add(hardwarePage);
        host.Children.Add(featuresPage);
        host.Children.Add(diagnosticsPage);

        navigationPage.Visibility = Visibility.Hidden;
        multiplayerPage.Visibility = Visibility.Hidden;
        hardwarePage.Visibility = Visibility.Hidden;
        featuresPage.Visibility = Visibility.Hidden;
        diagnosticsPage.Visibility = Visibility.Hidden;

        return new ShellPages(
            host,
            homePage,
            navigationPage,
            multiplayerPage,
            hardwarePage,
            featuresPage,
            diagnosticsPage);
    }

    private static FrameworkElement BuildMultiplayerLanding(MainWindow window)
    {
        var stack = new StackPanel();

        var flow = new Grid { Margin = new Thickness(0d, 0d, 0d, 18d) };
        flow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        flow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14d) });
        flow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var hostCard = BuildSimpleActionCard(
            "CRIAR UMA SALA",
            "Você vira o host da própria sala. O NavBR inicia o servidor no seu PC e mostra as informações para convidar seus amigos.",
            "PEER-HOST • TCP 27730",
            Brush(255, 164, 75));
        Grid.SetColumn(hostCard, 0);
        flow.Children.Add(hostCard);

        var joinCard = BuildSimpleActionCard(
            "ENTRAR EM UMA SALA",
            "Recebeu um convite? Abra a Central Multiplayer e cole os dados do host. O NavBR cuida da conexão, presença, chat e voz.",
            "CONVITE • CHAT • VOZ",
            Brush(103, 188, 255));
        Grid.SetColumn(joinCard, 2);
        flow.Children.Add(joinCard);
        stack.Children.Add(flow);

        var publicRooms = BuildSimpleActionCard(
            "SALAS PÚBLICAS",
            "O navegador de salas públicas faz parte da Alpha.12. Enquanto ele é desenvolvido, o modo peer-host atual continua sendo o caminho principal.",
            "ALPHA.12 • EM DESENVOLVIMENTO",
            Brush(159, 139, 255));
        publicRooms.Margin = new Thickness(0d, 0d, 0d, 18d);
        stack.Children.Add(publicRooms);

        var cta = new Border
        {
            Padding = new Thickness(18d),
            Background = Brush(12, 21, 27),
            BorderBrush = Brush(38, 57, 69),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(14d)
        };
        var ctaGrid = new Grid();
        ctaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        ctaGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var explanation = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        explanation.Children.Add(new TextBlock
        {
            Text = "Central Multiplayer",
            Foreground = Brushes.White,
            FontSize = 15d,
            FontWeight = FontWeights.SemiBold
        });
        explanation.Children.Add(new TextBlock
        {
            Text = "Aqui ficam criação/entrada de sala, jogadores, chat, voz e opções de conexão.",
            Foreground = Brush(142, 160, 173),
            FontSize = 10.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 4d, 16d, 0d)
        });
        Grid.SetColumn(explanation, 0);
        ctaGrid.Children.Add(explanation);

        DetachFromParent(window.MultiplayerButton);
        window.MultiplayerButton.Content = "Abrir Central Multiplayer";
        window.MultiplayerButton.MinWidth = 230d;
        window.MultiplayerButton.HorizontalAlignment = HorizontalAlignment.Right;
        window.MultiplayerButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        NormalizePrimaryButton(window.MultiplayerButton);
        Grid.SetColumn(window.MultiplayerButton, 1);
        ctaGrid.Children.Add(window.MultiplayerButton);

        cta.Child = ctaGrid;
        stack.Children.Add(cta);

        var note = new TextBlock
        {
            Text = "Dica: para uma primeira experiência, teste entre dois PCs na mesma rede antes de configurar acesso pela Internet.",
            Foreground = Brush(117, 137, 151),
            FontSize = 10d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(3d, 12d, 3d, 0d)
        };
        stack.Children.Add(note);

        return stack;
    }

    private static Border BuildSimpleActionCard(
        string title,
        string body,
        string status,
        Brush accent)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontSize = 13d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = body,
            Foreground = Brush(152, 170, 182),
            FontSize = 10.8d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 8d, 0d, 13d)
        });
        stack.Children.Add(new TextBlock
        {
            Text = status,
            Foreground = accent,
            FontSize = 9d,
            FontWeight = FontWeights.Bold
        });

        return new Border
        {
            MinHeight = 142d,
            Padding = new Thickness(17d),
            Background = Brush(10, 18, 24),
            BorderBrush = Brush(31, 47, 57),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(14d),
            Child = stack
        };
    }

    private static Border BuildWorkspaceHeader()
    {
        var grid = new Grid { Margin = new Thickness(28d, 20d, 28d, 0d) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new StackPanel();
        title.Children.Add(new TextBlock
        {
            Text = "NavBR",
            Foreground = Brushes.White,
            FontSize = 20d,
            FontWeight = FontWeights.Bold
        });
        title.Children.Add(new TextBlock
        {
            Text = "Dirija. Navegue. Conecte.",
            Foreground = Brush(127, 145, 159),
            FontSize = 11d,
            Margin = new Thickness(0d, 3d, 0d, 0d)
        });
        Grid.SetColumn(title, 0);
        grid.Children.Add(title);

        var version = new Border
        {
            Padding = new Thickness(11d, 6d, 11d, 6d),
            Background = Brush(31, 25, 18),
            BorderBrush = Brush(112, 66, 30),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(999d),
            Child = new TextBlock
            {
                Text = "ALPHA.12 • DEV",
                Foreground = Brush(255, 164, 75),
                FontSize = 10d,
                FontWeight = FontWeights.Bold
            }
        };
        Grid.SetColumn(version, 1);
        grid.Children.Add(version);

        return new Border
        {
            Padding = new Thickness(0d, 0d, 0d, 18d),
            BorderBrush = Brush(24, 35, 43),
            BorderThickness = new Thickness(0d, 0d, 0d, 1d),
            Child = grid
        };
    }

    private static Border BuildWelcomeCard()
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Tudo pronto para sua próxima viagem",
            Foreground = Brushes.White,
            FontSize = 26d,
            FontWeight = FontWeights.Bold
        });
        content.Children.Add(new TextBlock
        {
            Text = "Abra o OMSI, escolha seu ônibus e deixe o NavBR cuidar de navegação, multiplayer e integrações.",
            Foreground = Brush(181, 196, 207),
            FontSize = 13d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 8d, 0d, 16d)
        });

        var chips = new WrapPanel();
        chips.Children.Add(NewChip("OMSI", "AUTO"));
        chips.Children.Add(NewChip("GPS", "PRONTO"));
        chips.Children.Add(NewChip("MULTIPLAYER", "PEER HOST"));
        chips.Children.Add(NewChip("HARDWARE", "SERIAL"));
        content.Children.Add(chips);

        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 24d),
            Padding = new Thickness(22d),
            CornerRadius = new CornerRadius(18d),
            Background = new LinearGradientBrush(
                Color.FromRgb(16, 28, 36),
                Color.FromRgb(10, 17, 23),
                0d),
            BorderBrush = Brush(38, 57, 69),
            BorderThickness = new Thickness(1d),
            Child = content
        };
    }

    private static Border NewChip(string title, string status)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brush(145, 163, 176),
            FontSize = 8.5d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = status,
            Foreground = Brush(255, 164, 75),
            FontSize = 10d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 2d, 0d, 0d)
        });

        return new Border
        {
            MinWidth = 108d,
            Margin = new Thickness(0d, 0d, 8d, 8d),
            Padding = new Thickness(10d, 7d, 10d, 7d),
            Background = Brush(9, 17, 23),
            BorderBrush = Brush(38, 53, 63),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Child = stack
        };
    }

    private static Border BuildQuickHelpCard()
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "Primeiros passos",
            Foreground = Brushes.White,
            FontSize = 14d,
            FontWeight = FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "1. Abra o OMSI  •  2. Carregue mapa e ônibus  •  3. Confira se o status fica conectado  •  4. Use Navegação ou Multiplayer",
            Foreground = Brush(164, 181, 194),
            FontSize = 11.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 7d, 0d, 0d)
        });

        return new Border
        {
            Margin = new Thickness(0d, 18d, 0d, 0d),
            Padding = new Thickness(16d),
            CornerRadius = new CornerRadius(12d),
            Background = Brush(10, 18, 24),
            BorderBrush = Brush(30, 45, 55),
            BorderThickness = new Thickness(1d),
            Child = stack
        };
    }

    private static FrameworkElement BuildSectionTitle(string title, string subtitle)
    {
        var stack = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 18d) };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brushes.White,
            FontSize = 22d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = Brush(137, 155, 169),
            FontSize = 11.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 5d, 0d, 0d)
        });
        return stack;
    }

    private static FrameworkElement BuildFeatureOverview()
    {
        var stack = new StackPanel();
        foreach (var group in NavBRFeatureCatalog.All.GroupBy(feature => feature.State))
        {
            stack.Children.Add(new TextBlock
            {
                Text = FeatureStateTitle(group.Key),
                Foreground = Brush(179, 195, 207),
                FontSize = 10d,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(2d, 12d, 0d, 8d)
            });

            var wrap = new WrapPanel();
            foreach (var feature in group)
            {
                wrap.Children.Add(BuildFeatureCard(feature));
            }
            stack.Children.Add(wrap);
        }
        return stack;
    }

    private static Border BuildFeatureCard(NavBRFeatureDescriptor feature)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = feature.Name,
            Foreground = Brushes.White,
            FontSize = 12d,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = FeatureStateLabel(feature.State) + (feature.RequiresExplicitOptIn ? " • OPT-IN" : string.Empty),
            Foreground = FeatureStateBrush(feature.State),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 6d, 0d, 0d)
        });

        return new Border
        {
            Width = 230d,
            MinHeight = 72d,
            Margin = new Thickness(0d, 0d, 10d, 10d),
            Padding = new Thickness(13d),
            Background = Brush(10, 18, 24),
            BorderBrush = Brush(31, 46, 56),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(11d),
            Child = stack
        };
    }

    private static string FeatureStateTitle(NavBRFeatureState state) => state switch
    {
        NavBRFeatureState.Ready => "PRONTO",
        NavBRFeatureState.Experimental => "EM TESTE",
        NavBRFeatureState.InDevelopment => "EM DESENVOLVIMENTO",
        _ => "PLANEJADO NA ALPHA.12"
    };

    private static string FeatureStateLabel(NavBRFeatureState state) => state switch
    {
        NavBRFeatureState.Ready => "PRONTO",
        NavBRFeatureState.Experimental => "EXPERIMENTAL",
        NavBRFeatureState.InDevelopment => "EM DESENVOLVIMENTO",
        _ => "PLANEJADO"
    };

    private static Brush FeatureStateBrush(NavBRFeatureState state) => state switch
    {
        NavBRFeatureState.Ready => Brush(110, 216, 153),
        NavBRFeatureState.Experimental => Brush(240, 194, 105),
        NavBRFeatureState.InDevelopment => Brush(255, 164, 75),
        _ => Brush(134, 155, 171)
    };

    private static ScrollViewer NewPage(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        CanContentScroll = false,
        Background = Brushes.Transparent
    };

    private static Border BuildSidebar(MainWindow window, ShellPages pages)
    {
        var panel = new DockPanel { Margin = new Thickness(14d) };
        var body = new StackPanel();
        body.LayoutUpdated += (_, _) => NormalizeSidebarButtons(body);

        var advancedExpander = new Expander
        {
            Header = NewExpanderHeader("Ferramentas avançadas"),
            IsExpanded = false,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0d, 5d, 0d, 0d),
            Foreground = Brush(183, 199, 213)
        };

        var footer = BuildFooter(window, advancedExpander);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);

        var bodyScroller = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            PanningMode = System.Windows.Controls.PanningMode.VerticalOnly,
            Focusable = false
        };
        panel.Children.Add(bodyScroller);

        body.Children.Add(BuildBrand());
        body.Children.Add(BuildSectionLabel("USO DIÁRIO"));

        var pageButtons = new List<Button>();
        var homeButton = NewPageButton("⌂  Início", pages.Home, pages, pageButtons);
        body.Children.Add(homeButton);
        body.Children.Add(NewPageButton("⌖  Navegação", pages.Navigation, pages, pageButtons));
        body.Children.Add(NewPageButton("●  Multiplayer", pages.Multiplayer, pages, pageButtons));
        body.Children.Add(NewPageButton("▣  Hardware", pages.Hardware, pages, pageButtons));

        body.Children.Add(NewSeparator());
        body.Children.Add(BuildSectionLabel("PRÓXIMA VERSÃO"));
        body.Children.Add(NewPageButton("✦  Recursos Alpha.12", pages.Features, pages, pageButtons));

        body.Children.Add(NewSeparator());
        body.Children.Add(BuildSectionLabel("AJUDA"));
        var manualButton = NewNavigationButton(GetManualButtonText(), () =>
        {
            var manual = new NavBRManualWindow { Owner = window };
            manual.ShowDialog();
        });
        body.Children.Add(manualButton);
        window.LanguageComboBox.SelectionChanged += (_, _) => manualButton.Content = GetManualButtonText();

        var toolsPanel = new StackPanel { Margin = new Thickness(0d, 6d, 0d, 0d) };
        window.RegisterName(ToolsPanelName, toolsPanel);

        var advancedContent = new StackPanel();
        advancedContent.Children.Add(NewNavigationButton("◫  Diagnóstico técnico", () =>
        {
            ShowPage(pages.Diagnostics, pages, pageButtons, selectedButton: null);
            window.ProcessDetailsText.BringIntoView();
        }));
        advancedContent.Children.Add(toolsPanel);
        advancedExpander.Content = advancedContent;
        body.Children.Add(advancedExpander);

        ShowPage(pages.Home, pages, pageButtons, homeButton);

        return new Border
        {
            Background = Brush(8, 15, 20),
            BorderBrush = Brush(27, 40, 49),
            BorderThickness = new Thickness(0d, 0d, 1d, 0d),
            Child = panel
        };
    }

    private static FrameworkElement NewExpanderHeader(string text) => new TextBlock
    {
        Text = "⋯  " + text,
        Foreground = Brush(183, 199, 213),
        FontSize = 11.5d,
        FontWeight = FontWeights.SemiBold
    };

    private static FrameworkElement BuildFooter(MainWindow window, Expander advancedExpander)
    {
        var stack = new StackPanel { Margin = new Thickness(0d, 12d, 0d, 0d) };
        stack.Children.Add(NewSeparator());

        var advancedToggle = new CheckBox
        {
            Content = "Mostrar modo avançado",
            Foreground = Brush(171, 187, 199),
            FontSize = 10.5d,
            Margin = new Thickness(4d, 0d, 0d, 12d),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        advancedToggle.Checked += (_, _) => advancedExpander.Visibility = Visibility.Visible;
        advancedToggle.Unchecked += (_, _) =>
        {
            advancedExpander.IsExpanded = false;
            advancedExpander.Visibility = Visibility.Collapsed;
        };
        stack.Children.Add(advancedToggle);

        MoveToPanel(window.LanguageLabelText, stack);
        window.LanguageLabelText.Margin = new Thickness(3d, 0d, 0d, 4d);
        MoveToPanel(window.LanguageComboBox, stack);
        window.LanguageComboBox.Margin = new Thickness(0d, 0d, 0d, 8d);
        window.LanguageComboBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        window.LanguageComboBox.Width = double.NaN;

        stack.Children.Add(NewNavigationButton("—  Minimizar para bandeja", () =>
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

    private static UIElement BuildBrand()
    {
        var stack = new StackPanel { Margin = new Thickness(5d, 5d, 5d, 22d) };
        stack.Children.Add(new TextBlock
        {
            Text = "NavBR",
            Foreground = Brushes.White,
            FontSize = 25d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "ALPHA.12",
            Foreground = Brush(255, 154, 61),
            FontSize = 9.5d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 2d, 0d, 0d)
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Mais simples por fora. Mais completo por dentro.",
            Foreground = Brush(102, 121, 136),
            FontSize = 9.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 6d, 0d, 0d)
        });
        return stack;
    }

    private static Button NewPageButton(
        string text,
        FrameworkElement page,
        ShellPages pages,
        List<Button> pageButtons)
    {
        Button? button = null;
        button = NewNavigationButton(text, () => ShowPage(page, pages, pageButtons, button));
        pageButtons.Add(button);
        return button;
    }

    private static void ShowPage(
        FrameworkElement page,
        ShellPages pages,
        IReadOnlyList<Button> pageButtons,
        Button? selectedButton)
    {
        foreach (var candidate in pages.All)
        {
            candidate.Visibility = ReferenceEquals(candidate, page) ? Visibility.Visible : Visibility.Hidden;
        }

        foreach (var button in pageButtons)
        {
            NormalizeButton(button);
        }

        if (selectedButton is not null)
        {
            selectedButton.Background = Brush(22, 35, 45);
            selectedButton.BorderBrush = Brush(255, 137, 45);
            selectedButton.BorderThickness = new Thickness(3d, 0d, 0d, 0d);
            selectedButton.Padding = new Thickness(11d, 9d, 13d, 9d);
            selectedButton.Foreground = Brushes.White;
        }
    }

    private static string GetManualButtonText() => LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
    {
        "pt" => "?  Manual de uso",
        "es" => "?  Manual de uso",
        "de" => "?  Benutzerhandbuch",
        "fr" => "?  Manuel d’utilisation",
        _ => "?  User manual"
    };

    private static Button NewNavigationButton(string text, Action action)
    {
        var button = new Button { Content = text };
        NormalizeButton(button);
        button.Click += (_, _) => action();
        return button;
    }

    internal static Button CreateToolButton(string text, string tooltip)
    {
        var button = new Button { Content = text, ToolTip = tooltip };
        NormalizeButton(button);
        return button;
    }

    private static void NormalizeSidebarButtons(Panel root)
    {
        foreach (var button in EnumerateButtons(root))
        {
            if (button.Name == "MultiplayerButton")
            {
                NormalizePrimaryButton(button);
            }
        }
    }

    private static IEnumerable<Button> EnumerateButtons(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Button button)
            {
                yield return button;
            }
            foreach (var descendant in EnumerateButtons(child))
            {
                yield return descendant;
            }
        }
    }

    private static void NormalizeButton(Button button)
    {
        button.Height = 44d;
        button.MinWidth = 0d;
        button.Margin = new Thickness(0d, 0d, 0d, 6d);
        button.Padding = new Thickness(13d, 9d, 13d, 9d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Background = Brush(10, 19, 25);
        button.Foreground = Brush(218, 230, 238);
        button.BorderBrush = Brush(28, 42, 51);
        button.BorderThickness = new Thickness(1d);
        button.FontSize = 12.5d;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static void NormalizePrimaryButton(Button button)
    {
        NormalizeButton(button);
        button.Background = Brush(205, 88, 17);
        button.Foreground = Brushes.White;
        button.BorderBrush = Brush(255, 139, 48);
        button.FontWeight = FontWeights.SemiBold;
    }

    private static TextBlock BuildSectionLabel(string text) => new()
    {
        Text = text,
        Margin = new Thickness(4d, 0d, 0d, 8d),
        Foreground = Brush(106, 126, 141),
        FontSize = 8.8d,
        FontWeight = FontWeights.Bold
    };

    private static Border NewSeparator() => new()
    {
        Height = 1d,
        Margin = new Thickness(0d, 9d, 0d, 14d),
        Background = Brush(29, 43, 52)
    };

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    private static void PrepareWorkspaceCard(FrameworkElement card, Thickness margin)
    {
        card.Margin = margin;
        card.HorizontalAlignment = HorizontalAlignment.Stretch;
        card.VerticalAlignment = VerticalAlignment.Top;
    }

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
            case Panel oldPanel:
                oldPanel.Children.Remove(element);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, element):
                decorator.Child = null;
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, element):
                contentControl.Content = null;
                break;
        }
    }

    private static void MoveToPanel(UIElement element, Panel target)
    {
        DetachFromParent(element);
        target.Children.Add(element);
    }

    private sealed record ShellPages(
        Grid Host,
        FrameworkElement Home,
        FrameworkElement Navigation,
        FrameworkElement Multiplayer,
        FrameworkElement Hardware,
        FrameworkElement Features,
        FrameworkElement Diagnostics)
    {
        public IReadOnlyList<FrameworkElement> All { get; } =
            new[] { Home, Navigation, Multiplayer, Hardware, Features, Diagnostics };
    }
}