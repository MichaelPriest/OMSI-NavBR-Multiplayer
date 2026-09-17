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

        var button = new Button
        {
            Content = "HUD",
            Tag = "alpha12-hud-shortcut"
        };
        StyleNavigationButton(button);
        button.Click += (_, _) => OpenHudSettings(window);

        // Figma system order: Hardware, HUD, Settings, Session Health.
        var insertIndex = Math.Min(1, systemPanel.Children.Count);
        systemPanel.Children.Insert(insertIndex, button);

        window.Closed += (_, _) => Installed.Remove(window);
    }

    private static void OpenHudSettings(MainWindow owner)
    {
        var dialog = new Alpha12SettingsWindow(owner);

        RoutedEventHandler? loaded = null;
        loaded = (_, _) =>
        {
            if (loaded is not null)
            {
                dialog.Loaded -= loaded;
            }

            _ = dialog.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                () =>
                {
                    var hudButton = Enumerate<Button>(dialog)
                        .FirstOrDefault(candidate =>
                            string.Equals(candidate.Content?.ToString(), "HUD", StringComparison.OrdinalIgnoreCase));
                    hudButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
        };

        dialog.Loaded += loaded;
        dialog.ShowDialog();
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

    private static IEnumerable<T> Enumerate<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in Enumerate<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }
}
