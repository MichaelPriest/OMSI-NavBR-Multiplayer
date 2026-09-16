using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;

namespace NavBR.Client.Windows;

internal static class Alpha11ShellUiInstaller
{
    private const string ToolsPanelName = "Alpha11ToolsPanel";
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (Installed.Contains(window) || window.Content is not UIElement originalContent)
        {
            return;
        }

        Installed.Add(window);
        window.Content = null;
        window.Width = Math.Max(window.Width, 1320d);
        window.Height = Math.Max(window.Height, 860d);
        window.MinWidth = 1040d;
        window.MinHeight = 720d;

        var root = new Grid
        {
            Background = new SolidColorBrush(Color.FromRgb(5, 9, 13))
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232d) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var sidebar = BuildSidebar(window);
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        var workspace = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(5, 9, 13)),
            Child = originalContent
        };
        Grid.SetColumn(workspace, 1);
        root.Children.Add(workspace);

        window.Content = root;
    }

    private static Border BuildSidebar(MainWindow window)
    {
        var panel = new DockPanel
        {
            Margin = new Thickness(15d)
        };

        var footer = BuildFooter(window);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);

        var body = new StackPanel();
        body.LayoutUpdated += (_, _) => NormalizeSidebarButtons(body);
        panel.Children.Add(body);

        body.Children.Add(BuildBrand());
        body.Children.Add(BuildSectionLabel("NAVEGAÇÃO"));
        body.Children.Add(NewNavigationButton("⌂  Visão geral", () => BringIntoView(window.StatusHeadingText)));
        body.Children.Add(NewNavigationButton("⌖  GPS e mapas", () => BringIntoView(window.RoadmapScrollViewer)));
        body.Children.Add(NewNavigationButton("◫  Plugin e diagnóstico", () => BringIntoView(window.ProcessDetailsText)));

        var manualButton = NewNavigationButton(GetManualButtonText(), () =>
        {
            var manual = new NavBRManualWindow
            {
                Owner = window
            };
            manual.ShowDialog();
        });
        body.Children.Add(manualButton);
        window.LanguageComboBox.SelectionChanged += (_, _) => manualButton.Content = GetManualButtonText();

        body.Children.Add(NewSeparator());
        body.Children.Add(BuildSectionLabel("FERRAMENTAS ALPHA.11"));

        var toolsPanel = new StackPanel();
        window.RegisterName(ToolsPanelName, toolsPanel);
        body.Children.Add(toolsPanel);

        MoveToPanel(window.MultiplayerButton, body);
        NormalizeButton(window.MultiplayerButton);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(7, 16, 23)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(29, 42, 52)),
            BorderThickness = new Thickness(0d, 0d, 1d, 0d),
            Child = panel
        };
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
        var stack = new StackPanel { Margin = new Thickness(3d, 3d, 3d, 22d) };
        stack.Children.Add(new TextBlock
        {
            Text = "NavBR",
            Foreground = Brushes.White,
            FontSize = 25d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "OMSI MULTIPLAYER • ALPHA.11 DEV",
            Foreground = new SolidColorBrush(Color.FromRgb(244, 122, 24)),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 2d, 0d, 0d)
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
            if (System.Windows.Application.Current is App app)
            {
                app.TrayIcon.HideMainWindow();
            }
            else
            {
                window.WindowState = WindowState.Minimized;
            }
        });
        stack.Children.Add(trayButton);

        stack.Children.Add(new TextBlock
        {
            Text = "O NavBR continua ativo em segundo plano.",
            Foreground = new SolidColorBrush(Color.FromRgb(101, 119, 134)),
            FontSize = 9d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(4d, 2d, 4d, 0d)
        });
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
            NormalizeButton(button);
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
        button.Height = 41d;
        button.MinWidth = 0d;
        button.Margin = new Thickness(0d, 0d, 0d, 7d);
        button.Padding = new Thickness(13d, 8d, 13d, 8d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Background = new SolidColorBrush(Color.FromRgb(11, 21, 29));
        button.Foreground = new SolidColorBrush(Color.FromRgb(221, 232, 242));
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(32, 48, 62));
        button.BorderThickness = new Thickness(1d);
        button.FontSize = 13d;
        button.Cursor = System.Windows.Input.Cursors.Hand;
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
        Margin = new Thickness(0d, 7d, 0d, 15d),
        Background = new SolidColorBrush(Color.FromRgb(30, 44, 54))
    };

    private static void BringIntoView(FrameworkElement element)
    {
        element.BringIntoView();
        element.Focus();
    }

    private static void MoveToPanel(UIElement element, Panel target)
    {
        // UIElement itself does not expose a Parent property. Resolve the
        // current visual/logical parent explicitly so controls can be moved
        // safely into the alpha.11 shell without depending on a concrete
        // FrameworkElement subtype.
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

        target.Children.Add(element);
    }
}
