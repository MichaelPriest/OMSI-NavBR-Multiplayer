using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace NavBR.Client.Windows;

internal static class Alpha12HudShortcutInstaller
{
    private static readonly HashSet<MainWindow> Installed = new();

    public static void Install(MainWindow window)
    {
        if (!Installed.Add(window))
        {
            return;
        }

        if (window.FindName(Alpha12ProfessionalShellInstaller.SystemPanelName) is not Panel systemPanel)
        {
            Installed.Remove(window);
            return;
        }

        if (systemPanel.Children
            .OfType<Button>()
            .Any(candidate => string.Equals(candidate.Tag as string, "alpha14-hud-editor", StringComparison.Ordinal)))
        {
            return;
        }

        var button = new Button
        {
            Content = "HUD",
            Tag = "alpha12-hud-shortcut"
        };
        StyleNavigationButton(button);
        button.Click += (_, _) => window.NavigatePrimaryWebShell("settings-hud");

        // Figma system order: Hardware, HUD, Settings, Session Health.
        var insertIndex = Math.Min(1, systemPanel.Children.Count);
        systemPanel.Children.Insert(insertIndex, button);

        window.Closed += (_, _) => Installed.Remove(window);
    }

    private static void StyleNavigationButton(Button button)
    {
        button.Height = 42d;
        button.Margin = new Thickness(0d, 0d, 0d, 6d);
        button.Padding = new Thickness(13d, 8d, 13d, 8d);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Background = new SolidColorBrush(Color.FromRgb(10, 19, 25));
        button.Foreground = new SolidColorBrush(Color.FromRgb(218, 230, 238));
        button.BorderBrush = new SolidColorBrush(Color.FromRgb(28, 42, 51));
        button.BorderThickness = new Thickness(1d);
        button.FontSize = 12d;
        button.Cursor = System.Windows.Input.Cursors.Hand;
    }


}
