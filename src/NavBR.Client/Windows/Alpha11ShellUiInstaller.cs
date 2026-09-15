using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
        panel.Children.Add(body);

        body.Children.Add(BuildBrand());
        body.Children.Add(BuildSectionLabel("NAVEGAÇÃO"));
        body.Children.Add(NewNavigationButton("⌂  Visão geral", () => BringIntoView(window.StatusHeadingText)));
        body.Children.Add(NewNavigationButton("⌖  GPS e mapas", () => BringIntoView(window.RoadmapScrollViewer)));
        body.Children.Add(NewNavigationButton("◫  Plugin e diagnóstico", () => BringIntoView(window.ProcessDetailsText)));
        body.Children.Add(NewSeparator());
        body.Children.Add(BuildSectionLabel("FERRAMENTAS ALPHA.11"));

        var toolsPanel = new StackPanel();
        window.RegisterName(ToolsPanelName, toolsPanel);
        body.Children.Add(toolsPanel);

        MoveToPanel(window.MultiplayerButton, body);
        window.MultiplayerButton.Margin = new Thickness(0d, 0d, 0d, 7d);
        window.MultiplayerButton.MinWidth = 0d;
        window.MultiplayerButton.HorizontalContentAlignment = HorizontalAlignment.Left;
        window.MultiplayerButton.Padding = new Thickness(13d, 8d, 13d, 8d);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(7, 16, 23)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(29, 42, 52)),
            BorderThickness = new Thickness(0d, 0d, 1d, 0d),
            Child = panel
        };
    }

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
        var button = new Button
        {
            Content = text,
            Height = 41d,
            Margin = new Thickness(0d, 0d, 0d, 7d),
            Padding = new Thickness(13d, 8d, 13d, 8d),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromRgb(11, 21, 29)),
            Foreground = new SolidColorBrush(Color.FromRgb(221, 232, 242)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(32, 48, 62)),
            BorderThickness = new Thickness(1d),
            FontSize = 13d,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += (_, _) => action();
        return button;
    }

    internal static Button CreateToolButton(string text, string tooltip)
    {
        return new Button
        {
            Content = text,
            ToolTip = tooltip,
            Height = 41d,
            Margin = new Thickness(0d, 0d, 0d, 7d),
            Padding = new Thickness(13d, 8d, 13d, 8d),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromRgb(11, 21, 29)),
            Foreground = new SolidColorBrush(Color.FromRgb(221, 232, 242)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(32, 48, 62)),
            BorderThickness = new Thickness(1d),
            FontSize = 13d,
            Cursor = System.Windows.Input.Cursors.Hand
        };
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
        switch (element.Parent)
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
