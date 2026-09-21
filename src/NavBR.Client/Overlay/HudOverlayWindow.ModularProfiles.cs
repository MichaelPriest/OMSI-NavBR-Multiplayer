using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

internal static class HudModularProfileBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(
            typeof(HudOverlayWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnHudLoaded));
    }

    private static void OnHudLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not HudOverlayWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            window.InitializeModularHudProfiles);
    }
}

public partial class HudOverlayWindow
{
    private bool _modularHudProfilesInitialized;

    internal void InitializeModularHudProfiles()
    {
        if (_modularHudProfilesInitialized || _busDashboardDock is null)
        {
            return;
        }

        _modularHudProfilesInitialized = true;
        _busDashboardDock.PreviewMouseWheel += ModularDashboard_PreviewMouseWheel;
        _busDashboardDock.ContextMenuOpening += ModularDashboard_ContextMenuOpening;
        MultiplayerSettingsStore.SettingsSaved += ModularHud_SettingsSaved;
        Closed += ModularHud_Closed;

        ApplyModularHudProfile();
    }

    private void ModularHud_SettingsSaved(MultiplayerSettings settings)
    {
        _hudSettings = settings;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, ApplyModularHudProfile);
    }

    private void ModularHud_Closed(object? sender, EventArgs e)
    {
        MultiplayerSettingsStore.SettingsSaved -= ModularHud_SettingsSaved;
    }

    private void ModularDashboard_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_busDashboardDock is null)
        {
            return;
        }

        _busDashboardDock.ContextMenu = BuildModularHudMenu();
    }

    private void ModularDashboard_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_hudLayoutEditMode)
        {
            return;
        }

        var direction = e.Delta > 0 ? 1d : -1d;
        MultiplayerSettings updated;

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            updated = _hudSettings with
            {
                DashboardWidth = Math.Clamp(_hudSettings.DashboardWidth + direction * 24d, 280d, 960d)
            };
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            updated = _hudSettings with
            {
                DashboardOpacity = Math.Clamp(_hudSettings.DashboardOpacity + direction * 0.05d, 0.35d, 1d)
            };
        }
        else
        {
            updated = _hudSettings with
            {
                DashboardScale = Math.Clamp(_hudSettings.DashboardScale + direction * 0.05d, 0.60d, 1.80d)
            };
        }

        SaveDashboardSettings(updated);
        e.Handled = true;
    }

    private ContextMenu BuildModularHudMenu()
    {
        var menu = new ContextMenu();

        var presets = new MenuItem { Header = DashboardText("Estilo do HUD", "HUD style") };
        foreach (var preset in HudProfileCatalog.Presets)
        {
            var captured = preset;
            var item = new MenuItem
            {
                Header = preset.DisplayName,
                IsCheckable = true,
                IsChecked = string.Equals(_hudSettings.DashboardPreset, preset.Id, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => SaveDashboardSettings(HudProfileCatalog.ApplyPreset(_hudSettings, captured.Id));
            presets.Items.Add(item);
        }
        menu.Items.Add(presets);

        var themes = new MenuItem { Header = DashboardText("Tema", "Theme") };
        foreach (var theme in HudProfileCatalog.Themes)
        {
            var captured = theme;
            var item = new MenuItem
            {
                Header = theme.DisplayName,
                IsCheckable = true,
                IsChecked = string.Equals(_hudSettings.DashboardTheme, theme.Id, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => SaveDashboardSettings(_hudSettings with { DashboardTheme = captured.Id });
            themes.Items.Add(item);
        }
        menu.Items.Add(themes);

        var sizes = new MenuItem { Header = DashboardText("Tamanho", "Size") };
        sizes.Items.Add(NewHudAction("Pequeno • 72%", () => SaveDashboardSettings(HudProfileCatalog.ApplySizePreset(_hudSettings, "small"))));
        sizes.Items.Add(NewHudAction("Médio • 100%", () => SaveDashboardSettings(HudProfileCatalog.ApplySizePreset(_hudSettings, "medium"))));
        sizes.Items.Add(NewHudAction("Grande • 120%", () => SaveDashboardSettings(HudProfileCatalog.ApplySizePreset(_hudSettings, "large"))));
        sizes.Items.Add(NewHudAction("Extra grande • 145%", () => SaveDashboardSettings(HudProfileCatalog.ApplySizePreset(_hudSettings, "xl"))));
        sizes.Items.Add(new Separator());
        sizes.Items.Add(NewHudAction(DashboardText("Largura -", "Width -"), () => SaveDashboardSettings(_hudSettings with
        {
            DashboardWidth = Math.Clamp(_hudSettings.DashboardWidth - 30d, 280d, 960d)
        })));
        sizes.Items.Add(NewHudAction(DashboardText("Largura +", "Width +"), () => SaveDashboardSettings(_hudSettings with
        {
            DashboardWidth = Math.Clamp(_hudSettings.DashboardWidth + 30d, 280d, 960d)
        })));
        sizes.Items.Add(NewHudAction(DashboardText("Altura automática", "Automatic height"), () => SaveDashboardSettings(_hudSettings with
        {
            DashboardHeight = 0d
        })));
        menu.Items.Add(sizes);

        var widgets = new MenuItem { Header = DashboardText("Módulos", "Widgets") };
        widgets.Items.Add(NewHudToggle(DashboardText("Combustível", "Fuel"), _hudSettings.DashboardShowFuel,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowFuel = value })));
        widgets.Items.Add(NewHudToggle(DashboardText("Pedais", "Pedals"), _hudSettings.DashboardShowPedals,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowPedals = value })));
        widgets.Items.Add(NewHudToggle(DashboardText("Indicadores", "Indicators"), _hudSettings.DashboardShowStatus,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowStatus = value })));
        widgets.Items.Add(NewHudToggle(DashboardText("Minimapa integrado", "Integrated minimap"), _hudSettings.DashboardShowMinimap,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowMinimap = value })));
        widgets.Items.Add(NewHudToggle(DashboardText("Multiplayer no painel", "Multiplayer panel"), _hudSettings.DashboardShowMultiplayer,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowMultiplayer = value })));
        widgets.Items.Add(NewHudToggle(DashboardText("Alertas discretos", "Discrete alerts"), _hudSettings.DashboardShowAlerts,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowAlerts = value })));
        widgets.Items.Add(NewHudToggle(DashboardText("Indicadores laterais", "Side indicators"), _hudSettings.DashboardShowSideIndicators,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowSideIndicators = value })));
        menu.Items.Add(widgets);

        var anchors = new MenuItem { Header = DashboardText("Ancoragem", "Anchor") };
        foreach (var anchor in new[]
                 {
                     (Id: "free", Label: "Livre"),
                     (Id: "top-left", Label: "Superior esquerdo"),
                     (Id: "top-center", Label: "Superior centro"),
                     (Id: "top-right", Label: "Superior direito"),
                     (Id: "bottom-left", Label: "Inferior esquerdo"),
                     (Id: "bottom-center", Label: "Inferior centro"),
                     (Id: "bottom-right", Label: "Inferior direito")
                 })
        {
            var captured = anchor;
            var item = new MenuItem
            {
                Header = captured.Label,
                IsCheckable = true,
                IsChecked = string.Equals(_hudSettings.DashboardAnchor, captured.Id, StringComparison.OrdinalIgnoreCase)
            };
            item.Click += (_, _) => SaveDashboardSettings(_hudSettings with { DashboardAnchor = captured.Id });
            anchors.Items.Add(item);
        }
        menu.Items.Add(anchors);

        menu.Items.Add(new Separator());
        menu.Items.Add(NewHudToggle(DashboardText("Escala automática por resolução", "Automatic resolution scaling"), _hudSettings.DashboardAutoScale,
            value => SaveDashboardSettings(_hudSettings with { DashboardAutoScale = value })));
        menu.Items.Add(NewHudAction(DashboardText("Mover componentes", "Move components"), () => SetLayoutEditMode(true)));

        return menu;
    }

    private static MenuItem NewHudAction(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private static MenuItem NewHudToggle(string header, bool value, Action<bool> changed)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = value
        };
        item.Click += (_, _) => changed(item.IsChecked);
        return item;
    }

    private void ApplyModularHudProfile()
    {
        if (_busDashboardDock is null || _busDashboardScale is null)
        {
            return;
        }

        var settings = MultiplayerSettingsStore.Load();
        _hudSettings = settings;

        _busDashboardDock.Width = settings.DashboardWidth;
        if (settings.DashboardHeight <= 0d)
        {
            _busDashboardDock.ClearValue(HeightProperty);
        }
        else
        {
            _busDashboardDock.Height = settings.DashboardHeight;
        }

        var resolutionScale = settings.DashboardAutoScale
            ? GetResolutionScaleFactor()
            : 1d;
        var effectiveScale = Math.Clamp(settings.DashboardScale * resolutionScale, 0.60d, 1.80d);
        _busDashboardScale.ScaleX = effectiveScale;
        _busDashboardScale.ScaleY = effectiveScale;
        _busDashboardDock.Opacity = settings.DashboardOpacity;

        if (_dashboardFuelPanel is not null)
        {
            _dashboardFuelPanel.Visibility = settings.DashboardShowFuel ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_dashboardPedalsPanel is not null)
        {
            _dashboardPedalsPanel.Visibility = settings.DashboardShowPedals ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_dashboardStatusPanel is not null)
        {
            _dashboardStatusPanel.Visibility = settings.DashboardShowStatus ? Visibility.Visible : Visibility.Collapsed;
        }

        ApplyDashboardTheme(settings.DashboardTheme);
        ApplyDashboardAnchor(settings.DashboardAnchor, effectiveScale);

        if (_busDashboardDock.ContextMenu is not null)
        {
            _busDashboardDock.ContextMenu = BuildModularHudMenu();
        }
    }

    private double GetResolutionScaleFactor()
    {
        var width = ActualWidth > 0d ? ActualWidth : SystemParameters.PrimaryScreenWidth;
        return width switch
        {
            < 1400d => 0.88d,
            < 1800d => 0.95d,
            > 3000d => 1.18d,
            > 2400d => 1.10d,
            _ => 1d
        };
    }

    private void ApplyDashboardAnchor(string? anchor, double scale)
    {
        if (_busDashboardDock is null || _busDashboardTransform is null ||
            ActualWidth <= 1d || ActualHeight <= 1d)
        {
            return;
        }

        anchor = HudProfileCatalog.ResolveAnchor(anchor);
        if (anchor == HudProfileCatalog.DefaultAnchor)
        {
            ApplyDashboardPosition();
            return;
        }

        var width = Math.Max(1d, _busDashboardDock.ActualWidth * scale);
        var height = Math.Max(1d, _busDashboardDock.ActualHeight * scale);
        const double margin = 18d;

        var left = margin;
        var centerX = Math.Max(margin, (ActualWidth - width) / 2d);
        var right = Math.Max(margin, ActualWidth - width - margin);
        var top = margin;
        var bottom = Math.Max(margin, ActualHeight - height - margin);

        var (x, y) = anchor switch
        {
            "top-left" => (left, top),
            "top-center" => (centerX, top),
            "top-right" => (right, top),
            "bottom-left" => (left, bottom),
            "bottom-center" => (centerX, bottom),
            "bottom-right" => (right, bottom),
            _ => (_busDashboardTransform.X, _busDashboardTransform.Y)
        };

        _busDashboardTransform.X = x;
        _busDashboardTransform.Y = y;
    }

    private void ApplyDashboardTheme(string? themeId)
    {
        if (_busDashboardDock is null)
        {
            return;
        }

        var theme = HudProfileCatalog.ResolveTheme(themeId).Id;
        var palette = theme switch
        {
            "immersive-operation" => new HudPalette(
                Color.FromRgb(4, 15, 24), Color.FromRgb(46, 110, 154), Color.FromRgb(58, 169, 255), Color.FromRgb(235, 244, 250)),
            "bus-panel" => new HudPalette(
                Color.FromRgb(5, 7, 8), Color.FromRgb(118, 79, 36), Color.FromRgb(255, 174, 67), Colors.White),
            "lcd" => new HudPalette(
                Color.FromRgb(5, 16, 19), Color.FromRgb(93, 116, 121), Color.FromRgb(211, 231, 235), Color.FromRgb(223, 239, 241)),
            "amber-classic" => new HudPalette(
                Color.FromRgb(18, 10, 2), Color.FromRgb(135, 78, 8), Color.FromRgb(255, 171, 31), Color.FromRgb(255, 187, 48)),
            "light" => new HudPalette(
                Color.FromRgb(231, 238, 244), Color.FromRgb(140, 164, 183), Color.FromRgb(26, 111, 176), Color.FromRgb(19, 38, 54)),
            _ => new HudPalette(
                Color.FromRgb(6, 23, 34), Color.FromRgb(35, 70, 95), Color.FromRgb(46, 159, 255), Colors.White)
        };

        var alpha = (byte)Math.Clamp((int)Math.Round(settingsOpacityToAlpha(_hudSettings.DashboardOpacity)), 0, 255);
        _busDashboardDock.Background = new SolidColorBrush(Color.FromArgb(alpha, palette.Background.R, palette.Background.G, palette.Background.B));
        _busDashboardDock.BorderBrush = new SolidColorBrush(palette.Border);

        if (_dashboardSpeedText is not null)
        {
            _dashboardSpeedText.Foreground = new SolidColorBrush(palette.Accent);
        }
        if (_dashboardFuelBar is not null)
        {
            _dashboardFuelBar.Foreground = new SolidColorBrush(palette.Accent);
        }
        if (_alpha12DestinationText is not null)
        {
            _alpha12DestinationText.Foreground = new SolidColorBrush(palette.Accent);
        }
        if (_alpha12NextStopText is not null)
        {
            _alpha12NextStopText.Foreground = new SolidColorBrush(palette.Text);
        }

        foreach (var indicator in _dashboardIndicators.Values)
        {
            if (!ReferenceEquals(indicator.Label.Foreground, Brushes.White))
            {
                indicator.Label.Foreground = new SolidColorBrush(palette.Text);
            }
        }
    }

    private static double settingsOpacityToAlpha(double opacity) =>
        Math.Clamp(opacity, 0.35d, 1d) * 255d;

    private readonly record struct HudPalette(Color Background, Color Border, Color Accent, Color Text);
}
