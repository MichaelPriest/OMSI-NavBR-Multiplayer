using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Overlay;

internal static class HudModularWidgetBootstrap
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
        if (sender is HudOverlayWindow window)
        {
            _ = window.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                window.InitializeModularHudWidgets);
        }
    }
}

public partial class HudOverlayWindow
{
    private Border? _modularWidgetsRoot;
    private Border? _modularMinimapWidget;
    private Border? _modularMinimapMapFrame;
    private Border? _modularMultiplayerWidget;
    private Border? _modularAlertsWidget;
    private Border? _modularSideIndicatorsWidget;
    private TextBlock? _modularMultiplayerText;
    private TextBlock? _modularAlertsText;
    private TextBlock? _modularSideIndicatorsText;
    private TextBlock? _modularMinimapTitle;
    private DispatcherTimer? _modularWidgetsTimer;
    private bool _modularWidgetsInitialized;

    internal void InitializeModularHudWidgets()
    {
        if (_modularWidgetsInitialized)
        {
            return;
        }

        if (_busDashboardDock?.Child is not StackPanel dashboardStack)
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                InitializeModularHudWidgets);
            return;
        }

        _modularWidgetsInitialized = true;
        _modularWidgetsRoot = BuildModularWidgetsRoot();
        dashboardStack.Children.Add(_modularWidgetsRoot);

        _modularWidgetsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _modularWidgetsTimer.Tick += ModularWidgetsTimer_Tick;
        _modularWidgetsTimer.Start();

        MultiplayerSettingsStore.SettingsSaved += ModularWidgets_SettingsSaved;
        MultiplayerSettingsStore.HudPreviewChanged += ModularWidgets_PreviewChanged;
        MultiplayerSettingsStore.HudPreviewCleared += ModularWidgets_PreviewCleared;
        Closed += ModularWidgets_Closed;

        ApplyModularWidgetSettings(MultiplayerSettingsStore.Load());
        RenderModularHudWidgets();
    }

    private Border BuildModularWidgetsRoot()
    {
        var content = new StackPanel();

        var topGrid = new Grid();
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        _modularMinimapWidget = BuildMinimapWidget();
        Grid.SetColumn(_modularMinimapWidget, 0);
        topGrid.Children.Add(_modularMinimapWidget);

        _modularMultiplayerWidget = BuildMultiplayerWidget();
        Grid.SetColumn(_modularMultiplayerWidget, 1);
        topGrid.Children.Add(_modularMultiplayerWidget);

        content.Children.Add(topGrid);

        _modularAlertsWidget = BuildTextWidget(
            "ALERTAS",
            out _modularAlertsText,
            new Thickness(0d, 6d, 0d, 0d));
        _modularAlertsWidget.PreviewMouseWheel += AlertsWidget_PreviewMouseWheel;
        content.Children.Add(_modularAlertsWidget);

        _modularSideIndicatorsWidget = BuildTextWidget(
            "STATUS DO VEÍCULO",
            out _modularSideIndicatorsText,
            new Thickness(0d, 6d, 0d, 0d));
        _modularSideIndicatorsWidget.PreviewMouseWheel += SideIndicatorsWidget_PreviewMouseWheel;
        content.Children.Add(_modularSideIndicatorsWidget);

        return new Border
        {
            Margin = new Thickness(0d, 7d, 0d, 0d),
            Child = content
        };
    }

    private Border BuildMinimapWidget()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });

        _modularMinimapTitle = NewWidgetTitle("MINIMAPA");
        Grid.SetRow(_modularMinimapTitle, 0);
        grid.Children.Add(_modularMinimapTitle);

        var map = new Grid { Margin = new Thickness(0d, 6d, 0d, 0d) };
        _modularMinimapMapFrame = new Border
        {
            CornerRadius = new CornerRadius(7d),
            BorderBrush = new SolidColorBrush(Color.FromArgb(75, 255, 255, 255)),
            BorderThickness = new Thickness(1d),
            ClipToBounds = true,
            Background = new VisualBrush(MiniMapViewport)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            }
        };
        map.Children.Add(_modularMinimapMapFrame);
        Grid.SetRow(map, 1);
        grid.Children.Add(map);

        var panel = NewWidgetBorder(grid, new Thickness(0d, 0d, 3d, 0d));
        panel.Height = 132d;
        panel.ToolTip = "Modo de edição: roda = zoom do GPS";
        panel.PreviewMouseWheel += MinimapWidget_PreviewMouseWheel;
        return panel;
    }

    private Border BuildMultiplayerWidget()
    {
        var stack = new StackPanel();
        stack.Children.Add(NewWidgetTitle("MULTIPLAYER"));
        _modularMultiplayerText = new TextBlock
        {
            Margin = new Thickness(0d, 6d, 0d, 0d),
            Foreground = Brushes.White,
            FontSize = 10d,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16d
        };
        stack.Children.Add(_modularMultiplayerText);

        var panel = NewWidgetBorder(stack, new Thickness(3d, 0d, 0d, 0d));
        panel.MinHeight = 132d;
        panel.ToolTip = "Modo de edição: roda = tamanho do painel multiplayer";
        panel.PreviewMouseWheel += MultiplayerWidget_PreviewMouseWheel;
        return panel;
    }

    private static Border BuildTextWidget(
        string title,
        out TextBlock text,
        Thickness margin)
    {
        var stack = new StackPanel();
        stack.Children.Add(NewWidgetTitle(title));
        text = new TextBlock
        {
            Margin = new Thickness(0d, 5d, 0d, 0d),
            Foreground = Brushes.White,
            FontSize = 10d,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 15d
        };
        stack.Children.Add(text);
        return NewWidgetBorder(stack, margin);
    }

    private static TextBlock NewWidgetTitle(string text) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.FromRgb(117, 176, 216)),
        FontFamily = new FontFamily("Bahnschrift"),
        FontSize = 8.5d,
        FontWeight = FontWeights.Bold
    };

    private static Border NewWidgetBorder(UIElement child, Thickness margin) => new()
    {
        Margin = margin,
        Padding = new Thickness(9d, 7d, 9d, 7d),
        CornerRadius = new CornerRadius(8d),
        Background = new SolidColorBrush(Color.FromArgb(168, 5, 15, 23)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(72, 100, 155, 194)),
        BorderThickness = new Thickness(1d),
        Child = child
    };

    private void ModularWidgetsTimer_Tick(object? sender, EventArgs e)
    {
        RenderModularHudWidgets();
    }

    private void ModularWidgets_SettingsSaved(MultiplayerSettings settings)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            ApplyModularWidgetSettings(settings);
            RenderEnhancedMiniMap();
            RenderModularHudWidgets();
        });
    }

    private void ModularWidgets_PreviewChanged(MultiplayerSettings settings)
    {
        _ = Dispatcher.BeginInvoke(() =>
        {
            ApplyModularWidgetSettings(settings);
            RenderEnhancedMiniMap();
            RenderModularHudWidgets();
        });
    }

    private void ModularWidgets_PreviewCleared()
    {
        var settings = MultiplayerSettingsStore.Load();
        _ = Dispatcher.BeginInvoke(() =>
        {
            ApplyModularWidgetSettings(settings);
            RenderEnhancedMiniMap();
            RenderModularHudWidgets();
        });
    }

    private void ModularWidgets_Closed(object? sender, EventArgs e)
    {
        MultiplayerSettingsStore.SettingsSaved -= ModularWidgets_SettingsSaved;
        MultiplayerSettingsStore.HudPreviewChanged -= ModularWidgets_PreviewChanged;
        MultiplayerSettingsStore.HudPreviewCleared -= ModularWidgets_PreviewCleared;
        if (_modularWidgetsTimer is not null)
        {
            _modularWidgetsTimer.Stop();
            _modularWidgetsTimer.Tick -= ModularWidgetsTimer_Tick;
            _modularWidgetsTimer = null;
        }
        Closed -= ModularWidgets_Closed;
    }

    private void ApplyModularWidgetSettings(MultiplayerSettings settings)
    {
        if (_modularWidgetsRoot is null)
        {
            return;
        }

        _hudSettings = settings;

        var mode = settings.HudSelectionMode?.Trim().ToLowerInvariant() ?? "all";
        var single = string.Equals(mode, "single", StringComparison.Ordinal);
        var selected = string.Equals(mode, "selected", StringComparison.Ordinal);
        var singleWidget = settings.HudSingleWidget?.Trim().ToLowerInvariant() ?? "dashboard";

        var showDashboard =
            mode == "all" ||
            single && singleWidget == "dashboard" ||
            selected && settings.DashboardEnabled;
        var showMinimap =
            mode == "all" ||
            single && singleWidget == "minimap" ||
            selected && settings.DashboardShowMinimap;
        var showMultiplayer =
            mode == "all"
                ? settings.DashboardShowMultiplayer
                : single
                    ? singleWidget == "multiplayer"
                    : settings.DashboardShowMultiplayer;
        var showAlerts =
            mode == "all"
                ? settings.DashboardShowAlerts
                : single
                    ? singleWidget == "alerts"
                    : settings.DashboardShowAlerts;
        var showStatus =
            mode == "all"
                ? settings.DashboardShowSideIndicators
                : single
                    ? singleWidget == "status"
                    : settings.DashboardShowSideIndicators;

        TopStatusPanel.Visibility = showDashboard
            ? Visibility.Visible
            : Visibility.Collapsed;
        TripInfoPanel.Visibility = showDashboard
            ? Visibility.Visible
            : Visibility.Collapsed;

        var composedPreset = HudProfileCatalog.IsComposedPreset(settings.DashboardPreset);
        var integratedMinimap =
            showDashboard &&
            settings.DashboardEnabled &&
            settings.DashboardShowMinimap &&
            !composedPreset &&
            mode == "all";
        if (!_immersiveOperationActive)
        {
            MiniMapHudPanel.Opacity = integratedMinimap ? 0.01d : 1d;
            MiniMapHudPanel.IsHitTestVisible = !integratedMinimap;
            MiniMapHudPanel.Visibility = showMinimap || integratedMinimap
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        _modularMinimapWidget!.Visibility =
            showMinimap && settings.DashboardShowMinimap && mode == "all"
                ? Visibility.Visible
                : Visibility.Collapsed;
        _modularMultiplayerWidget!.Visibility = showMultiplayer
            ? Visibility.Visible
            : Visibility.Collapsed;
        _modularAlertsWidget!.Visibility = showAlerts
            ? Visibility.Visible
            : Visibility.Collapsed;
        _modularSideIndicatorsWidget!.Visibility = showStatus
            ? Visibility.Visible
            : Visibility.Collapsed;

        var minimapHeight = Math.Clamp(
            132d * settings.DashboardMinimapScale,
            78d,
            264d);
        _modularMinimapWidget.Height = minimapHeight;
        if (_modularMinimapMapFrame is not null)
        {
            var circular = string.Equals(
                settings.DashboardMinimapStyle,
                "circular",
                StringComparison.OrdinalIgnoreCase);
            if (circular)
            {
                var diameter = Math.Clamp(
                    minimapHeight,
                    78d,
                    220d);

                _modularMinimapTitle!.Visibility = Visibility.Collapsed;
                _modularMinimapWidget.Width = diameter;
                _modularMinimapWidget.Height = diameter;
                _modularMinimapWidget.Padding = new Thickness(5d);
                _modularMinimapWidget.CornerRadius =
                    new CornerRadius(diameter / 2d);
                _modularMinimapWidget.ClipToBounds = true;
                _modularMinimapWidget.Clip = new EllipseGeometry(
                    new Point(diameter / 2d, diameter / 2d),
                    diameter / 2d,
                    diameter / 2d);

                var innerDiameter = Math.Max(1d, diameter - 10d);
                _modularMinimapMapFrame.Width = innerDiameter;
                _modularMinimapMapFrame.Height = innerDiameter;
                _modularMinimapMapFrame.HorizontalAlignment =
                    HorizontalAlignment.Center;
                _modularMinimapMapFrame.VerticalAlignment =
                    VerticalAlignment.Center;
                _modularMinimapMapFrame.CornerRadius =
                    new CornerRadius(innerDiameter / 2d);
                _modularMinimapMapFrame.Clip = new EllipseGeometry(
                    new Point(innerDiameter / 2d, innerDiameter / 2d),
                    innerDiameter / 2d,
                    innerDiameter / 2d);
            }
            else
            {
                _modularMinimapTitle!.Visibility = Visibility.Visible;
                _modularMinimapWidget.ClearValue(WidthProperty);
                _modularMinimapWidget.Height = minimapHeight;
                _modularMinimapWidget.Padding =
                    new Thickness(9d, 7d, 9d, 7d);
                _modularMinimapWidget.CornerRadius =
                    new CornerRadius(8d);
                _modularMinimapWidget.ClipToBounds = false;
                _modularMinimapWidget.Clip = null;

                _modularMinimapMapFrame.ClearValue(WidthProperty);
                _modularMinimapMapFrame.ClearValue(HeightProperty);
                _modularMinimapMapFrame.HorizontalAlignment =
                    HorizontalAlignment.Stretch;
                _modularMinimapMapFrame.VerticalAlignment =
                    VerticalAlignment.Stretch;
                _modularMinimapMapFrame.CornerRadius =
                    new CornerRadius(7d);
                _modularMinimapMapFrame.Clip = null;
            }
        }
        _modularMultiplayerWidget.MinHeight = Math.Clamp(132d * settings.DashboardMultiplayerScale, 78d, 264d);
        _modularMultiplayerText!.FontSize = Math.Clamp(10d * settings.DashboardMultiplayerScale, 8d, 18d);
        _modularAlertsText!.FontSize = Math.Clamp(10d * settings.DashboardAlertsScale, 8d, 18d);
        _modularSideIndicatorsText!.FontSize = Math.Clamp(10d * settings.DashboardSideIndicatorsScale, 8d, 18d);

        UpdateModularWidgetsRootVisibility();
        ApplyModularWidgetTheme(settings.DashboardTheme);
    }

    private void RenderModularHudWidgets()
    {
        if (!_modularWidgetsInitialized)
        {
            return;
        }

        RenderModularMultiplayerWidget();
        RenderModularAlertsWidget();
        RenderModularSideIndicatorsWidget();

        if (_modularMinimapTitle is not null)
        {
            _modularMinimapTitle.Text = string.IsNullOrWhiteSpace(_localTelemetry?.MapName)
                ? "MINIMAPA"
                : $"MINIMAPA • {_localTelemetry.MapName}";
        }

        UpdateModularWidgetsRootVisibility();
    }

    private void UpdateModularWidgetsRootVisibility()
    {
        if (_modularWidgetsRoot is null)
        {
            return;
        }

        var anyVisible =
            _modularMinimapWidget?.Visibility == Visibility.Visible ||
            _modularMultiplayerWidget?.Visibility == Visibility.Visible ||
            _modularAlertsWidget?.Visibility == Visibility.Visible ||
            _modularSideIndicatorsWidget?.Visibility == Visibility.Visible;
        _modularWidgetsRoot.Visibility =
            anyVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderModularMultiplayerWidget()
    {
        if (_modularMultiplayerText is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var frames = _smoothedHudFrames.Values
            .Where(frame => now - frame.Telemetry.Timestamp <= TimeSpan.FromSeconds(3))
            .OrderBy(frame => frame.Player.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(5)
            .ToArray();

        if (frames.Length == 0)
        {
            _modularMultiplayerText.Text = DashboardText("Nenhum motorista remoto", "No remote drivers");
            return;
        }

        _modularMultiplayerText.Text = string.Join(
            Environment.NewLine,
            frames.Select(frame =>
            {
                var line = string.IsNullOrWhiteSpace(frame.Telemetry.Line) ? "—" : frame.Telemetry.Line.Trim();
                var distance = GetModularDistanceText(_localTelemetry, frame.Telemetry);
                return $"{frame.Player.DisplayName}  •  {line}  •  {frame.Telemetry.SpeedKph:F0} km/h  •  {distance}";
            }));
    }

    private static string GetModularDistanceText(VehicleTelemetry? local, VehicleTelemetry remote)
    {
        if (local is null ||
            string.IsNullOrWhiteSpace(local.MapName) ||
            string.IsNullOrWhiteSpace(remote.MapName) ||
            !string.Equals(local.MapName, remote.MapName, StringComparison.OrdinalIgnoreCase))
        {
            return "—";
        }

        var dx = remote.X - local.X;
        var dy = remote.Y - local.Y;
        var meters = Math.Sqrt(dx * dx + dy * dy);
        if (!double.IsFinite(meters))
        {
            return "—";
        }

        return meters >= 1000d
            ? $"{meters / 1000d:F1} km"
            : $"{meters:F0} m";
    }

    private void RenderModularAlertsWidget()
    {
        if (_modularAlertsText is null)
        {
            return;
        }

        if (!_hudSettings.DashboardShowAlerts)
        {
            _modularAlertsWidget!.Visibility = Visibility.Collapsed;
            return;
        }

        var telemetry = _localTelemetry;
        if (telemetry is null)
        {
            _modularAlertsText.Text = string.Empty;
            _modularAlertsWidget!.Visibility = Visibility.Collapsed;
            return;
        }

        var alerts = new List<string>();
        var critical = false;
        var attention = false;

        if (telemetry.FuelPercent is double fuel && fuel <= 15d)
        {
            alerts.Add(DashboardText($"Combustível baixo • {fuel:F0}%", $"Low fuel • {fuel:F0}%"));
            critical = true;
        }
        if (telemetry.Doors != VehicleDoorFlags.None)
        {
            alerts.Add(DashboardText("Portas abertas", "Doors open"));
            attention = true;
        }
        if (telemetry.StopRequested)
        {
            alerts.Add(DashboardText("Parada solicitada", "Stop requested"));
            attention = true;
        }
        if (telemetry.DelaySeconds is int delay && Math.Abs(delay) >= 60)
        {
            var minutes = Math.Max(1, Math.Abs(delay) / 60);
            alerts.Add(delay > 0
                ? DashboardText($"Atraso • +{minutes} min", $"Delay • +{minutes} min")
                : DashboardText($"Adiantado • {minutes} min", $"Early • {minutes} min"));
            attention = true;
        }

        if (alerts.Count == 0)
        {
            _modularAlertsText.Text = string.Empty;
            _modularAlertsWidget!.Visibility = Visibility.Collapsed;
            return;
        }

        _modularAlertsWidget!.Visibility = Visibility.Visible;
        _modularAlertsText.Text = string.Join("  •  ", alerts.Take(4));
        _modularAlertsText.Foreground = critical
            ? new SolidColorBrush(Color.FromRgb(255, 104, 104))
            : attention
                ? new SolidColorBrush(Color.FromRgb(255, 191, 79))
                : Brushes.White;
    }

    private void RenderModularSideIndicatorsWidget()
    {
        if (_modularSideIndicatorsText is null)
        {
            return;
        }

        var telemetry = _localTelemetry;
        if (telemetry is null)
        {
            _modularSideIndicatorsText.Text = "—";
            return;
        }

        var fuel = telemetry.FuelPercent is double fuelValue ? $"{fuelValue:F0}%" : "—";
        var doors = telemetry.Doors == VehicleDoorFlags.None
            ? DashboardText("fechadas", "closed")
            : DashboardText("abertas", "open");
        var parking = telemetry.ParkingBrakeActive ? "P ON" : "P OFF";
        var turn = telemetry.TurnSignal switch
        {
            TurnSignalState.Left => "SETA ◀",
            TurnSignalState.Right => "SETA ▶",
            TurnSignalState.Hazard => "PISCA ⚠",
            _ => "SETA —"
        };
        var lights = telemetry.Lights == VehicleLightFlags.None ? "LUZ —" : "LUZ ON";
        var wipers = telemetry.WipersActive ? "LIMP ON" : "LIMP —";

        _modularSideIndicatorsText.Text =
            $"COMB {fuel}  |  PORTAS {doors}  |  {parking}  |  {turn}  |  {lights}  |  {wipers}";
    }

    private void ApplyModularWidgetTheme(string? themeId)
    {
        var theme = HudProfileCatalog.ResolveTheme(themeId).Id;
        var (background, border, title, text) = theme switch
        {
            "amber-classic" =>
                (Color.FromArgb(220, 20, 11, 2), Color.FromRgb(125, 76, 15), Color.FromRgb(255, 174, 45), Color.FromRgb(255, 202, 91)),
            "lcd" =>
                (Color.FromArgb(218, 6, 18, 20), Color.FromRgb(73, 103, 108), Color.FromRgb(191, 220, 224), Color.FromRgb(220, 236, 238)),
            "light" =>
                (Color.FromArgb(232, 234, 241, 246), Color.FromRgb(150, 174, 193), Color.FromRgb(22, 105, 169), Color.FromRgb(28, 48, 64)),
            "bus-panel" =>
                (Color.FromArgb(225, 8, 9, 10), Color.FromRgb(78, 59, 38), Color.FromRgb(255, 174, 67), Colors.White),
            _ =>
                (Color.FromArgb(205, 6, 23, 34), Color.FromRgb(35, 70, 95), Color.FromRgb(104, 185, 255), Colors.White)
        };

        foreach (var panel in new[]
                 {
                     _modularMinimapWidget,
                     _modularMultiplayerWidget,
                     _modularAlertsWidget,
                     _modularSideIndicatorsWidget
                 })
        {
            if (panel is null)
            {
                continue;
            }

            panel.Background = new SolidColorBrush(background);
            panel.BorderBrush = new SolidColorBrush(border);
        }

        if (_modularMinimapTitle is not null) _modularMinimapTitle.Foreground = new SolidColorBrush(title);
        if (_modularMultiplayerWidget?.Child is StackPanel mp && mp.Children[0] is TextBlock mpTitle)
            mpTitle.Foreground = new SolidColorBrush(title);
        if (_modularAlertsWidget?.Child is StackPanel alerts && alerts.Children[0] is TextBlock alertsTitle)
            alertsTitle.Foreground = new SolidColorBrush(title);
        if (_modularSideIndicatorsWidget?.Child is StackPanel side && side.Children[0] is TextBlock sideTitle)
            sideTitle.Foreground = new SolidColorBrush(title);

        if (_modularMultiplayerText is not null) _modularMultiplayerText.Foreground = new SolidColorBrush(text);
        if (_modularSideIndicatorsText is not null) _modularSideIndicatorsText.Foreground = new SolidColorBrush(text);
    }

    private void MinimapWidget_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_hudLayoutEditMode)
        {
            return;
        }

        var multiplier = e.Delta > 0 ? 1.15d : 1d / 1.15d;
        _hudSettings = _hudSettings with
        {
            HudZoom = Math.Clamp(
                _hudSettings.HudZoom * multiplier,
                0.65d,
                10d)
        };
        _renderedHudZoom = _hudSettings.HudZoom;
        MultiplayerSettingsStore.Save(_hudSettings);
        RenderEnhancedMiniMap();
        e.Handled = true;
    }

    private void MultiplayerWidget_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ResizeModularWidget(e, _hudSettings.DashboardMultiplayerScale,
            value => _hudSettings with { DashboardMultiplayerScale = value });
    }

    private void AlertsWidget_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ResizeModularWidget(e, _hudSettings.DashboardAlertsScale,
            value => _hudSettings with { DashboardAlertsScale = value });
    }

    private void SideIndicatorsWidget_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ResizeModularWidget(e, _hudSettings.DashboardSideIndicatorsScale,
            value => _hudSettings with { DashboardSideIndicatorsScale = value });
    }

    private void ResizeModularWidget(
        MouseWheelEventArgs e,
        double current,
        Func<double, MultiplayerSettings> update)
    {
        if (!_hudLayoutEditMode)
        {
            return;
        }

        var delta = e.Delta > 0 ? 0.10d : -0.10d;
        SaveDashboardSettings(update(Math.Clamp(current + delta, 0.55d, 2d)));
        e.Handled = true;
    }
}
