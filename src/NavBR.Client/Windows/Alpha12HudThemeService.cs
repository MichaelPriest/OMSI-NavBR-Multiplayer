using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Multiplayer;
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
            var settings = MultiplayerSettingsStore.Load();
            if (TryApply(window, settings.DashboardTheme) && layoutUpdated is not null)
            {
                window.LayoutUpdated -= layoutUpdated;
            }
        };
        window.LayoutUpdated += layoutUpdated;

        Action<MultiplayerSettings> dashboardSettingsSaved = settings =>
        {
            if (window.Dispatcher.CheckAccess())
            {
                TryApply(window, settings.DashboardTheme);
            }
            else
            {
                _ = window.Dispatcher.BeginInvoke(() => TryApply(window, settings.DashboardTheme));
            }
        };
        MultiplayerSettingsStore.SettingsSaved += dashboardSettingsSaved;

        Action<Alpha12Preferences> preferencesSaved = preferences =>
        {
            var mappedTheme = MapLegacyTheme(preferences.HudTheme);
            var current = MultiplayerSettingsStore.Load();
            if (!string.Equals(current.DashboardTheme, mappedTheme, StringComparison.OrdinalIgnoreCase))
            {
                MultiplayerSettingsStore.Save(current with { DashboardTheme = mappedTheme });
                return;
            }

            if (window.Dispatcher.CheckAccess())
            {
                TryApply(window, mappedTheme);
            }
            else
            {
                _ = window.Dispatcher.BeginInvoke(() => TryApply(window, mappedTheme));
            }
        };
        Alpha12PreferencesStore.PreferencesSaved += preferencesSaved;

        window.Closed += (_, _) =>
        {
            if (layoutUpdated is not null)
            {
                window.LayoutUpdated -= layoutUpdated;
            }
            MultiplayerSettingsStore.SettingsSaved -= dashboardSettingsSaved;
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

        var mappedTheme = MapLegacyTheme(theme);
        var settings = MultiplayerSettingsStore.Load();
        if (!string.Equals(settings.DashboardTheme, mappedTheme, StringComparison.OrdinalIgnoreCase))
        {
            MultiplayerSettingsStore.Save(settings with { DashboardTheme = mappedTheme });
        }

        foreach (var hud in Application.Current.Windows.OfType<HudOverlayWindow>())
        {
            TryApply(hud, mappedTheme);
        }
    }

    public static void RefreshOpenHudLocalization()
    {
        if (Application.Current is null)
        {
            return;
        }

        var theme = MultiplayerSettingsStore.Load().DashboardTheme;
        foreach (var hud in Application.Current.Windows.OfType<HudOverlayWindow>())
        {
            Alpha12ExperienceInstaller.TagAndTranslateTree(hud);
            TryApply(hud, theme);
        }
    }

    private static bool TryApply(HudOverlayWindow window, string? theme)
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
                border.Background = new SolidColorBrush(Color.FromArgb(72, palette.Panel.R, palette.Panel.G, palette.Panel.B));
                if (border.BorderThickness != new Thickness(0d))
                {
                    border.BorderBrush = new SolidColorBrush(Color.FromArgb(70, palette.Accent.R, palette.Accent.G, palette.Accent.B));
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
        return EnumerateVisualChildren<Border>(root)
            .FirstOrDefault(border =>
                Panel.GetZIndex(border) >= 1000 &&
                border.Child is StackPanel);
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

    private static string MapLegacyTheme(string? theme) => theme?.Trim().ToLowerInvariant() switch
    {
        "classic" => "bus-panel",
        "amber" => "amber-classic",
        "minimal" => "navbr-modern",
        "navbr-modern" or "bus-panel" or "lcd" or "amber-classic" or "light" => theme.Trim().ToLowerInvariant(),
        _ => HudProfileCatalog.DefaultTheme
    };

    private sealed record Palette(
        Color Background,
        Color Panel,
        Color Border,
        Color Accent,
        Color PrimaryText,
        Color SecondaryText,
        bool Minimal)
    {
        public static Palette For(string? theme) => MapLegacyTheme(theme) switch
        {
            "bus-panel" => new(
                Color.FromArgb(242, 7, 9, 10),
                Color.FromArgb(215, 15, 18, 19),
                Color.FromArgb(185, 118, 79, 36),
                Color.FromRgb(255, 174, 67),
                Color.FromRgb(255, 255, 255),
                Color.FromRgb(199, 211, 217),
                false),
            "amber-classic" => new(
                Color.FromArgb(245, 14, 8, 2),
                Color.FromArgb(220, 22, 12, 3),
                Color.FromArgb(210, 137, 81, 10),
                Color.FromRgb(255, 174, 34),
                Color.FromRgb(255, 205, 105),
                Color.FromRgb(231, 167, 69),
                false),
            "lcd" => new(
                Color.FromArgb(238, 6, 20, 25),
                Color.FromArgb(205, 10, 35, 40),
                Color.FromArgb(190, 86, 210, 222),
                Color.FromRgb(102, 226, 211),
                Color.FromRgb(220, 255, 244),
                Color.FromRgb(148, 207, 199),
                false),
            "light" => new(
                Color.FromArgb(245, 232, 239, 244),
                Color.FromArgb(230, 245, 248, 250),
                Color.FromArgb(200, 139, 166, 185),
                Color.FromRgb(25, 112, 181),
                Color.FromRgb(18, 37, 52),
                Color.FromRgb(68, 91, 107),
                false),
            _ => new(
                Color.FromArgb(232, 6, 23, 34),
                Color.FromArgb(190, 7, 27, 40),
                Color.FromArgb(150, 35, 70, 95),
                Color.FromRgb(74, 171, 244),
                Color.FromRgb(255, 255, 255),
                Color.FromRgb(190, 211, 225),
                true)
        };
    }
}
