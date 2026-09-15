using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private bool _shortcutLegendInstalled;

    static HudOverlayWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(HudOverlayWindow),
            LoadedEvent,
            new RoutedEventHandler(OnHudWindowLoadedForShortcutLegend));
    }

    private static void OnHudWindowLoadedForShortcutLegend(object sender, RoutedEventArgs e)
    {
        if (sender is HudOverlayWindow window)
        {
            window.InstallInGameShortcutLegend();
        }
    }

    private void InstallInGameShortcutLegend()
    {
        if (_shortcutLegendInstalled || HudShortcutsText.Parent is not Panel currentParent)
        {
            return;
        }

        _shortcutLegendInstalled = true;

        // Keep the existing TextBlock because RefreshHudChrome already updates
        // it with the real configured keys and conflict state. We only move it
        // out of the connection-status row into a dedicated in-game control bar.
        currentParent.Children.Remove(HudShortcutsText);
        HudShortcutsText.Margin = new Thickness(8, 0, 0, 0);
        HudShortcutsText.Foreground = new SolidColorBrush(Color.FromArgb(0xE8, 0xFF, 0xFF, 0xFF));
        HudShortcutsText.FontSize = 11;
        HudShortcutsText.VerticalAlignment = VerticalAlignment.Center;

        var title = new TextBlock
        {
            Text = "⌨  ATALHOS",
            FontFamily = new FontFamily("Segoe UI Emoji"),
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9D, 0x24)),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        content.Children.Add(title);
        content.Children.Add(HudShortcutsText);

        var legend = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 5, 0, 0),
            Padding = new Thickness(9, 5, 9, 5),
            Background = new SolidColorBrush(Color.FromArgb(0x96, 0x10, 0x18, 0x20)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x42, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Child = content,
            ToolTip = "Atalhos ativos do NavBR durante o jogo"
        };

        // Keep hotkey conflict warning below the shortcut legend when present.
        var warningIndex = HudDock.Children.IndexOf(HotkeyWarningPanel);
        if (warningIndex >= 0)
        {
            HudDock.Children.Insert(warningIndex, legend);
        }
        else
        {
            HudDock.Children.Add(legend);
        }
    }
}
