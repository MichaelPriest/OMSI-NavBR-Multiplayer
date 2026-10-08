using System.Windows;

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
            window.HideInGameShortcutLegend();
        }
    }

    private void HideInGameShortcutLegend()
    {
        if (_shortcutLegendInstalled)
        {
            return;
        }

        _shortcutLegendInstalled = true;

        // Hotkeys remain configurable and functional, but the gameplay HUD no
        // longer renders keyboard shortcut hints or conflict banners. Those
        // belong in the React settings/help surfaces instead of over OMSI.
        HudShortcutsText.Visibility = Visibility.Collapsed;
        HotkeyWarningPanel.Visibility = Visibility.Collapsed;
    }
}
