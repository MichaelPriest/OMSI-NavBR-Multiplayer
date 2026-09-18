using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Hardware;
using NavBR.Client.Localization;

namespace NavBR.Client.Windows;

internal static class Alpha11ShellUiInstaller
{
    private const string ToolsPanelName = "Alpha11ToolsPanel";
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (Installed.Contains(window) || window.Content is not UIElement)
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

        Installed.Add(window);

        DetachFromParent(statusCard);
        DetachFromParent(telemetryCard);
        DetachFromParent(navigationCard);
        DetachFromParent(diagnosticsCard);
        window.Content = null;

        window.Width = Math.Max(window.Width, 1240d);
        window.Height = Math.Max(window.Height, 820d);
        window.MinWidth = 980d;
        window.MinHeight = 660d;

        var pages = BuildPages(window, statusCard, telemetryCard, navigationCard, diagnosticsCard);

        var root = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(5, 9, 13))
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(224d) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var sidebar = BuildSidebar(window, pages);
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        var workspace = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(5, 9, 13)),
            Child = pages.Host
        };
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
        PrepareWorkspaceCard(statusCard, new Thickness(0d, 0d, 0d, 16d));
        PrepareWorkspaceCard(telemetryCard, new Thickness(0d));
        PrepareWorkspaceCard(navigationCard, new Thickness(0d));
        PrepareWorkspaceCard(diagnosticsCard, new Thickness(0d));

        var overviewStack = new StackPanel
        {
            Margin = new Thickness(28d)
        };
        overviewStack.Children.Add(statusCard);
        overviewStack.Children.Add(telemetryCard);

        var overviewPage = NewPage(overviewStack);

        var navigationGrid = new Grid
        {
            Margin = new Thickness(28d)
        };
        navigationGrid.Children.Add(navigationCard);
        var navigationPage = NewPage(navigationGrid);

        var hardwarePage = NewPage(new HardwareCockpitView(window.GetCurrentTelemetryForAlpha11)
        {
            Margin = new Thickness(28d)
        });

        var diagnosticsStack = new StackPanel
        {
            Margin = new Thickness(28d)
        };
        diagnosticsStack.Children.Add(diagnosticsCard);
        var diagnosticsPage = NewPage(diagnosticsStack);

        var host = new Grid();
        host.Children.Add(overviewPage);
        host.Children.Add(navigationPage);
        host.Children.Add(hardwarePage);
        host.Children.Add(diagnosticsPage);

        navigationPage.Visibility = Visibility.Hidden;
        hardwarePage.Visibility = Visibility.Hidden;
        diagnosticsPage.Visibility = Visibility.Hidden;

        return new ShellPages(host, overviewPage, navigationPage, hardwarePage, diagnosticsPage);
    }

    private static ScrollViewer NewPage(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        CanContentScroll = false
    };

    private static Border BuildSidebar(MainWindow window, ShellPages pages)
    {
        var panel = new DockPanel
        {
            Margin = new Thickness(13d)
        };

        var footer = BuildFooter(window);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);

        var body = new StackPanel();
        body.LayoutUpdated += (_, _) => NormalizeSidebarButtons(body);

        var bodyScroller = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            PanningMode = PanningMode.VerticalOnly,
            Focusable = false
        };
        panel.Children.Add(bodyScroller);

        body.Children.Add(BuildBrand());
        body.Children.Add(BuildSectionLabel("PRINCIPAL"));

        var pageButtons = new List<Button>();
        var overviewButton = NewPageButton("⌂  Visão geral", pages.Overview, pages, pageButtons);
        body.Children.Add(overviewButton);

        var navigationButton = NewPageButton("⌖  Navegação", pages.Navigation, pages, pageButtons);
        body.Children.Add(navigationButton);

        MoveToPanel(window.MultiplayerButton, body);
        NormalizePrimaryButton(window.MultiplayerButton);

        var hardwareButton = NewPageButton("▣  Hardware cockpit", pages.Hardware, pages, pageButtons);
        body.Children.Add(hardwareButton);

        body.Children.Add(NewSeparator());
        body.Children.Add(BuildSectionLabel("SUPORTE"));

        var manualButton = NewNavigationButton(
            GetManualButtonText(),
            () => window.NavigatePrimaryWebShell("help"));
        body.Children.Add(manualButton);
        window.LanguageComboBox.SelectionChanged += (_, _) => manualButton.Content = GetManualButtonText();

        var toolsPanel = new StackPanel
        {
            Margin = new Thickness(0d, 6d, 0d, 0d)
        };
        window.RegisterName(ToolsPanelName, toolsPanel);

        var diagnosticsButton = NewNavigationButton("◫  Diagnóstico técnico", () =>
        {
            ShowPage(pages.Diagnostics, pages, pageButtons, selectedButton: null);
            window.ProcessDetailsText.BringIntoView();
        });

        var advancedContent = new StackPanel();
        advancedContent.Children.Add(diagnosticsButton);
        advancedContent.Children.Add(toolsPanel);

        body.Children.Add(new Expander
        {
            Header = new TextBlock
            {
                Text = "⋯  Ferramentas avançadas",
                Foreground = new SolidColorBrush(Color.FromRgb(183, 199, 213)),
                FontSize = 12d,
                FontWeight = FontWeights.SemiBold
            },
            Content = advancedContent,
            IsExpanded = false,
            Margin = new Thickness(2d, 4d, 2d, 0d),
            Foreground = new SolidColorBrush(Color.FromRgb(183, 199, 213))
        });

        ShowPage(pages.Overview, pages, pageButtons, overviewButton);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(7, 16, 23)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(29, 42, 52)),
            BorderThickness = new Thickness(0d, 0d, 1d, 0d),
            Child = panel
        };
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
            candidate.Visibility = ReferenceEquals(candidate, page)
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        foreach (var button in pageButtons)
        {
            NormalizeButton(button);
        }

        if (selectedButton is not null)
        {
            selectedButton.Background = new SolidColorBrush(Color.FromRgb(20, 38, 51));
            selectedButton.BorderBrush = new SolidColorBrush(Color.FromRgb(244, 122, 24));
            selectedButton.BorderThickness = new Thickness(3d, 0d, 0d, 0d);
            selectedButton.Padding = new Thickness(10d, 8d, 13d, 8d);
        }
    }

    private static string GetManualButtonText() =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "?  Manual de uso",
            "es" => "?  Manual de uso",
            "de" => "?  Benutzerhandbuch",
            "fr" => "?  Manuel d’utilisation",
            _ => "?  User manual"
        };

    private static UIElement BuildBrand()
    {
        var stack = new StackPanel { Margin = new Thickness(4d, 4d, 4d, 20d) };
        stack.Children.Add(new TextBlock
        {
            Text = "NavBR",
            Foreground = Brushes.White,
            FontSize = 24d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "OMSI • ALPHA.11 DEV",
            Foreground = new SolidColorBrush(Color.FromRgb(244, 122, 24)),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 2d, 0d, 0d)
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Dirija. Navegue. Conecte.",
            Foreground = new SolidColorBrush(Color.FromRgb(101, 119, 134)),
            FontSize = 10d,
            Margin = new Thickness(0d, 6d, 0d, 0d)
        });
        return stack;
    }

    private static FrameworkElement BuildFooter(MainWindow window)
    {
        var stack = new StackPanel { Margin = new Thickness(0d, 12d, 0d, 0d) };
        stack.Children.Add(NewSeparator());

        MoveToPanel(window.LanguageLabelText, stack);
        window.LanguageLabelText.Margin = new Thickness(3d, 4d, 0d, 4d);
        MoveToPanel(window.LanguageComboBox, stack);
        window.LanguageComboBox.Margin = new Thickness(0d, 0d, 0d, 8d);
        window.LanguageComboBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        window.LanguageComboBox.Width = double.NaN;

        var trayButton = NewNavigationButton("—  Minimizar para bandeja", () =>
        {
            if (Application.Current is App app)
            {
                app.TrayIcon.HideMainWindow();
            }
            else
            {
                window.WindowState = WindowState.Minimized;
            }
        });
        stack.Children.Add(trayButton);

        return stack;
    }

    private static Button NewNavigationButton(string text, Action action)
    {
        var button = new Button { Content = text };
        NormalizeButton(button);
        button.Click += (_, _) => action();
        return button;
    }

    internal static Button CreateToolButton(string text, string tooltip)
    {
        var button = new Button
        {
            Content = text,
            ToolTip = tooltip
        };
        NormalizeButton(button);
        return button;
    }

    private static void NormalizeSidebarButtons(Panel root)
    {
        foreach (var button in EnumerateButtons(root))
        {
            if (button == null)
            {
                continue;
            }

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
        button.Height = 42d;
        button.MinWidth = 0d;
        button.Margin = new Thickness(0d, 0d, 0d, 6d);
        button.Padding = new Thickness(13d, 8d, 13d, 8d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Background = new SolidColorBrush(Color.FromRgb(10, 20, 28));
        button.Foreground = new SolidColorBrush(Color.FromRgb(221, 232, 242));
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(26, 39, 50));
        button.BorderThickness = new Thickness(1d);
        button.FontSize = 12.5d;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static void NormalizePrimaryButton(Button button)
    {
        NormalizeButton(button);
        button.Background = new SolidColorBrush(Color.FromRgb(207, 91, 16));
        button.Foreground = Brushes.White;
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(244, 122, 24));
        button.FontWeight = FontWeights.SemiBold;
    }

    private static TextBlock BuildSectionLabel(string text) => new()
    {
        Text = text,
        Margin = new Thickness(4d, 0d, 0d, 8d),
        Foreground = new SolidColorBrush(Color.FromRgb(111, 131, 146)),
        FontSize = 9d,
        FontWeight = FontWeights.Bold
    };

    private static Border NewSeparator() => new()
    {
        Height = 1d,
        Margin = new Thickness(0d, 8d, 0d, 14d),
        Background = new SolidColorBrush(Color.FromRgb(30, 44, 54))
    };

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
        FrameworkElement Overview,
        FrameworkElement Navigation,
        FrameworkElement Hardware,
        FrameworkElement Diagnostics)
    {
        public IReadOnlyList<FrameworkElement> All { get; } =
            new[] { Overview, Navigation, Hardware, Diagnostics };
    }
}
