using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Overlay;

namespace NavBR.Client.Windows;

internal static class Alpha12HudThemeService
{
    private static readonly HashSet<HudOverlayWindow> Attached = new();

    public static void Attach(HudOverlayWindow window)
    {
        if (!Attached.Add(window))
        {
            return;
        }

        EventHandler? layoutUpdated = null;
        layoutUpdated = (_, _) =>
        {
            if (TryApply(window, Alpha12PreferencesStore.Load().HudTheme) && layoutUpdated is not null)
            {
                window.LayoutUpdated -= layoutUpdated;
            }
        };
        window.LayoutUpdated += layoutUpdated;

        Action<Alpha12Preferences> preferencesSaved = preferences =>
        {
            if (window.Dispatcher.CheckAccess())
            {
                TryApply(window, preferences.HudTheme);
            }
            else
            {
                _ = window.Dispatcher.BeginInvoke(() => TryApply(window, preferences.HudTheme));
            }
        };
        Alpha12PreferencesStore.PreferencesSaved += preferencesSaved;

        window.Closed += (_, _) =>
        {
            if (layoutUpdated is not null)
            {
                window.LayoutUpdated -= layoutUpdated;
            }
            Alpha12PreferencesStore.PreferencesSaved -= preferencesSaved;
            Attached.Remove(window);
        };
    }

    public static void ApplyToOpenHud(string theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        foreach (var hud in Application.Current.Windows.OfType<HudOverlayWindow>())
        {
            TryApply(hud, theme);
        }
    }

    public static void RefreshOpenHudLocalization()
    {
        if (Application.Current is null)
        {
            return;
        }

        foreach (var hud in Application.Current.Windows.OfType<HudOverlayWindow>())
        {
            Alpha12ExperienceInstaller.TagAndTranslateTree(hud);
            TryApply(hud, Alpha12PreferencesStore.Load().HudTheme);
        }
    }

    private static bool TryApply(HudOverlayWindow window, string theme)
    {
        var dashboard = FindDashboardRoot(window);
        if (dashboard is null)
        {
            return false;
        }

        Alpha12ExperienceInstaller.TagAndTranslateTree(dashboard);
        var palette = Palette.For(theme);
        dashboard.Background = new SolidColorBrush(palette.Background);
        dashboard.BorderBrush = new SolidColorBrush(palette.Border);

        foreach (var border in EnumerateVisualChildren<Border>(dashboard))
        {
            if (ReferenceEquals(border, dashboard))
            {
                continue;
            }

            if (palette.Minimal)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(75, 0, 0, 0));
                if (border.BorderThickness != new Thickness(0d))
                {
                    border.BorderBrush = new SolidColorBrush(Color.FromArgb(55, palette.Accent.R, palette.Accent.G, palette.Accent.B));
                }
            }
            else if (border.Background is SolidColorBrush)
            {
                border.Background = new SolidColorBrush(palette.Panel);
            }
        }

        foreach (var text in EnumerateVisualChildren<TextBlock>(dashboard))
        {
            if (text.FontSize >= 28d)
            {
                text.Foreground = new SolidColorBrush(palette.PrimaryText);
                continue;
            }

            if (text.Text.Contains("km/h", StringComparison.OrdinalIgnoreCase) ||
                Alpha12Text.TryResolveKey(text.Text, out var key) && key is
                    "DashboardMove" or "DashboardSpeed" or "DashboardFuel" or
                    "DashboardThrottle" or "DashboardBrake")
            {
                text.Foreground = new SolidColorBrush(palette.Accent);
                continue;
            }

            if (text.Foreground is SolidColorBrush brush && brush.Color.A > 100)
            {
                text.Foreground = new SolidColorBrush(palette.SecondaryText);
            }
        }

        foreach (var progress in EnumerateVisualChildren<ProgressBar>(dashboard))
        {
            progress.Foreground = new SolidColorBrush(palette.Accent);
        }

        return true;
    }

    private static Border? FindDashboardRoot(DependencyObject root)
    {
        foreach (var border in EnumerateVisualChildren<Border>(root))
        {
            if (Math.Abs(border.Width - 370d) < 0.5d && Panel.GetZIndex(border) >= 1000)
            {
                return border;
            }
        }
        return null;
    }

    private static IEnumerable<T> EnumerateVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (var child in EnumerateVisualChildren<T>(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }

    private sealed record Palette(
        Color Background,
        Color Panel,
        Color Border,
        Color Accent,
        Color PrimaryText,
        Color SecondaryText,
        bool Minimal)
    {
        public static Palette For(string? theme) => theme?.Trim().ToLowerInvariant() switch
        {
            "amber" => new(
                Color.FromArgb(242, 8, 6, 2),
                Color.FromArgb(215, 18, 12, 4),
                Color.FromArgb(210, 255, 147, 35),
                Color.FromRgb(255, 166, 53),
                Color.FromRgb(255, 199, 112),
                Color.FromRgb(227, 157, 75),
                false),
            "lcd" => new(
                Color.FromArgb(238, 6, 20, 25),
                Color.FromArgb(205, 10, 35, 40),
                Color.FromArgb(190, 86, 210, 222),
                Color.FromRgb(102, 226, 211),
                Color.FromRgb(220, 255, 244),
                Color.FromRgb(148, 207, 199),
                false),
            "minimal" => new(
                Color.FromArgb(205, 8, 10, 12),
                Color.FromArgb(72, 0, 0, 0),
                Color.FromArgb(65, 255, 255, 255),
                Color.FromRgb(235, 239, 242),
                Color.FromRgb(255, 255, 255),
                Color.FromRgb(190, 199, 205),
                true),
            _ => new(
                Color.FromArgb(235, 8, 13, 18),
                Color.FromArgb(165, 0, 0, 0),
                Color.FromArgb(120, 255, 255, 255),
                Color.FromRgb(255, 157, 36),
                Color.FromRgb(255, 255, 255),
                Color.FromRgb(205, 214, 221),
                false)
        };
    }
}
