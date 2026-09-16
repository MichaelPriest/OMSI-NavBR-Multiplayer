using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private Border? _busDashboardDock;
    private Border? _busDashboardHandle;
    private TranslateTransform? _busDashboardTransform;
    private ScaleTransform? _busDashboardScale;
    private DispatcherTimer? _busDashboardTimer;
    private Button? _mainDashboardButton;

    private TextBlock? _dashboardSpeedText;
    private TextBlock? _dashboardAccelerationText;
    private TextBlock? _dashboardFuelText;
    private ProgressBar? _dashboardFuelBar;
    private TextBlock? _dashboardThrottleText;
    private ProgressBar? _dashboardThrottleBar;
    private TextBlock? _dashboardBrakeText;
    private ProgressBar? _dashboardBrakeBar;
    private Border? _dashboardFuelPanel;
    private Border? _dashboardPedalsPanel;
    private Border? _dashboardStatusPanel;

    private readonly Dictionary<string, (Border Badge, TextBlock Label)> _dashboardIndicators = new();
    private bool _dashboardDragging;
    private Point _dashboardDragStartMouse;
    private Point _dashboardDragStartPosition;
    private bool _lastDashboardEditMode;

    private void EnsureBusDashboard()
    {
        if (_busDashboardDock is not null)
        {
            return;
        }

        _hudSettings = MultiplayerSettingsStore.Load();
        RemoveLegacyDriveCard();

        // The vehicle pointer stays fixed pointing up. The map/route rotates.
        LocalMarker.LayoutTransform = new ScaleTransform(0.62d, 0.62d);
        LocalMarkerRotation.Angle = 0d;
        CompositionTarget.Rendering += KeepLocalGpsMarkerHeadingUp;

        _busDashboardTransform = new TranslateTransform();
        _busDashboardScale = new ScaleTransform(1d, 1d);

        var stack = new StackPanel();
        _busDashboardHandle = BuildDashboardMoveHandle();
        stack.Children.Add(_busDashboardHandle);
        stack.Children.Add(BuildDashboardBody());

        _busDashboardDock = new Border
        {
            Width = 370d,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(235, 8, 13, 18)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(15d),
            Padding = new Thickness(9d),
            Child = stack,
            RenderTransform = _busDashboardTransform,
            LayoutTransform = _busDashboardScale
        };
        Panel.SetZIndex(_busDashboardDock, 1002);
        OverlayRoot.Children.Add(_busDashboardDock);

        _busDashboardDock.SizeChanged += (_, _) =>
        {
            if (!_dashboardDragging)
            {
                ApplyDashboardPosition();
            }
        };
        SizeChanged += (_, _) =>
        {
            if (!_dashboardDragging)
            {
                ApplyDashboardPosition();
            }
        };

        _busDashboardTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _busDashboardTimer.Tick += DashboardTimer_Tick;
        _busDashboardTimer.Start();

        MultiplayerSettingsStore.SettingsSaved += DashboardSettingsSaved;
        Closed += DashboardWindow_Closed;

        InstallMainWindowDashboardButton();
        ApplyDashboardSettings();
        RenderDashboard();
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ApplyDashboardPosition);
    }

    private Border BuildDashboardMoveHandle()
    {
        var handle = new Border
        {
            Margin = new Thickness(0, 0, 0, 7),
            Padding = new Thickness(8, 5, 8, 5),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(225, 24, 31, 39)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 157, 36)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.SizeAll,
            Visibility = Visibility.Collapsed,
            Child = new TextBlock
            {
                Text = "▦  PAINEL DO ÔNIBUS • ARRASTE PARA MOVER",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 179, 71)),
                FontSize = 10,
                FontWeight = FontWeights.Bold
            }
        };

        handle.MouseLeftButtonDown += DashboardHandle_MouseLeftButtonDown;
        handle.MouseMove += DashboardHandle_MouseMove;
        handle.MouseLeftButtonUp += DashboardHandle_MouseLeftButtonUp;
        return handle;
    }

    private FrameworkElement BuildDashboardBody()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var speedStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        speedStack.Children.Add(new TextBlock
        {
            Text = "VELOCIDADE",
            Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        _dashboardSpeedText = new TextBlock
        {
            Text = "--",
            Foreground = Brushes.White,
            FontSize = 39,
            FontWeight = FontWeights.Black,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        speedStack.Children.Add(_dashboardSpeedText);
        speedStack.Children.Add(new TextBlock
        {
            Text = "km/h",
            Foreground = new SolidColorBrush(Color.FromRgb(255, 157, 36)),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        _dashboardAccelerationText = new TextBlock
        {
            Text = "acel. —",
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)),
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        speedStack.Children.Add(_dashboardAccelerationText);

        var speedPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 9, 0),
            Child = speedStack
        };
        Grid.SetColumn(speedPanel, 0);
        grid.Children.Add(speedPanel);

        var right = new StackPanel();
        _dashboardFuelPanel = BuildFuelPanel();
        _dashboardPedalsPanel = BuildPedalsPanel();
        _dashboardStatusPanel = BuildStatusPanel();
        right.Children.Add(_dashboardFuelPanel);
        right.Children.Add(_dashboardPedalsPanel);
        right.Children.Add(_dashboardStatusPanel);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private Border BuildFuelPanel()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = "COMBUSTÍVEL",
            Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)),
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0)
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        _dashboardFuelBar = NewProgressBar();
        Grid.SetColumn(_dashboardFuelBar, 1);
        grid.Children.Add(_dashboardFuelBar);

        _dashboardFuelText = NewPercentText();
        Grid.SetColumn(_dashboardFuelText, 2);
        grid.Children.Add(_dashboardFuelText);

        return NewDashboardSection(grid, new Thickness(0, 0, 0, 5));
    }

    private Border BuildPedalsPanel()
    {
        var stack = new StackPanel();
        stack.Children.Add(BuildPedalRow("ACEL", out _dashboardThrottleBar, out _dashboardThrottleText));
        stack.Children.Add(BuildPedalRow("FREIO", out _dashboardBrakeBar, out _dashboardBrakeText));
        return NewDashboardSection(stack, new Thickness(0, 0, 0, 5));
    }

    private Border BuildStatusPanel()
    {
        var wrap = new WrapPanel();
        foreach (var item in new[]
                 {
                     (Key: "doors", Text: "PORTAS"),
                     (Key: "turn", Text: "SETA"),
                     (Key: "lights", Text: "LUZ"),
                     (Key: "park", Text: "P"),
                     (Key: "reverse", Text: "R"),
                     (Key: "wipers", Text: "LIMP")
                 })
        {
            var label = new TextBlock
            {
                Text = item.Text,
                Foreground = new SolidColorBrush(Color.FromArgb(145, 255, 255, 255)),
                FontSize = 8,
                FontWeight = FontWeights.Bold
            };
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 0, 4, 2),
                Child = label
            };
            _dashboardIndicators[item.Key] = (badge, label);
            wrap.Children.Add(badge);
        }

        return NewDashboardSection(wrap, new Thickness(0));
    }

    private static Border NewDashboardSection(UIElement child, Thickness margin) => new()
    {
        Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(8, 5, 8, 5),
        Margin = margin,
        Child = child
    };

    private static ProgressBar NewProgressBar() => new()
    {
        Minimum = 0,
        Maximum = 100,
        Height = 7,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static TextBlock NewPercentText() => new()
    {
        Text = "—",
        Foreground = Brushes.White,
        FontSize = 9,
        MinWidth = 38,
        Margin = new Thickness(7, 0, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static FrameworkElement BuildPedalRow(
        string caption,
        out ProgressBar bar,
        out TextBlock value)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

        var label = new TextBlock
        {
            Text = caption,
            Foreground = new SolidColorBrush(Color.FromArgb(175, 255, 255, 255)),
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        bar = NewProgressBar();
        Grid.SetColumn(bar, 1);
        grid.Children.Add(bar);

        value = NewPercentText();
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        return grid;
    }

    private void DashboardTimer_Tick(object? sender, EventArgs e)
    {
        if (_lastDashboardEditMode != _hudLayoutEditMode)
        {
            _lastDashboardEditMode = _hudLayoutEditMode;
            ApplyDashboardSettings();
        }
        RenderDashboard();
    }

    private void RenderDashboard()
    {
        if (_dashboardSpeedText is null)
        {
            return;
        }

        var telemetry = _localTelemetry;
        if (telemetry is null)
        {
            _dashboardSpeedText.Text = "--";
            if (_dashboardAccelerationText is not null) _dashboardAccelerationText.Text = "acel. —";
            SetProgress(_dashboardFuelBar, _dashboardFuelText, null);
            SetProgress(_dashboardThrottleBar, _dashboardThrottleText, null);
            SetProgress(_dashboardBrakeBar, _dashboardBrakeText, null);
            foreach (var indicator in _dashboardIndicators.Values)
            {
                SetIndicator(indicator, false, indicator.Label.Text);
            }
            return;
        }

        _dashboardSpeedText.Text = Math.Clamp(telemetry.SpeedKph, 0d, 999d)
            .ToString("F0", LocalizationService.CurrentCulture);
        if (_dashboardAccelerationText is not null)
        {
            _dashboardAccelerationText.Text = telemetry.AccelerationMps2 is double acceleration
                ? $"{acceleration:+0.0;-0.0;0.0} m/s²"
                : "acel. —";
        }

        SetProgress(_dashboardFuelBar, _dashboardFuelText, telemetry.FuelPercent);
        SetProgress(_dashboardThrottleBar, _dashboardThrottleText, telemetry.ThrottlePercent);
        SetProgress(_dashboardBrakeBar, _dashboardBrakeText, telemetry.BrakePercent);

        SetIndicator("doors", telemetry.Doors != VehicleDoorFlags.None,
            telemetry.Doors != VehicleDoorFlags.None ? "PORTAS!" : "PORTAS");
        SetIndicator("turn", telemetry.TurnSignal != TurnSignalState.Off, telemetry.TurnSignal switch
        {
            TurnSignalState.Left => "◀",
            TurnSignalState.Right => "▶",
            TurnSignalState.Hazard => "⚠",
            _ => "SETA"
        });
        SetIndicator("lights", telemetry.Lights != VehicleLightFlags.None, "LUZ");
        SetIndicator("park", telemetry.ParkingBrakeActive, "P");
        SetIndicator("reverse", telemetry.ReverseGear, "R");
        SetIndicator("wipers", telemetry.WipersActive, "LIMP");
    }

    private static void SetProgress(ProgressBar? bar, TextBlock? text, double? value)
    {
        if (bar is null || text is null)
        {
            return;
        }

        if (value is not double number || !double.IsFinite(number))
        {
            bar.Value = 0d;
            text.Text = "—";
            return;
        }

        number = Math.Clamp(number, 0d, 100d);
        bar.Value = number;
        text.Text = $"{number:F0}%";
    }

    private void SetIndicator(string key, bool active, string text)
    {
        if (_dashboardIndicators.TryGetValue(key, out var indicator))
        {
            SetIndicator(indicator, active, text);
        }
    }

    private static void SetIndicator((Border Badge, TextBlock Label) indicator, bool active, string text)
    {
        indicator.Label.Text = text;
        indicator.Label.Foreground = active
            ? Brushes.White
            : new SolidColorBrush(Color.FromArgb(145, 255, 255, 255));
        indicator.Badge.Background = active
            ? new SolidColorBrush(Color.FromArgb(205, 255, 132, 0))
            : new SolidColorBrush(Color.FromArgb(55, 255, 255, 255));
        indicator.Badge.BorderBrush = active
            ? new SolidColorBrush(Color.FromArgb(235, 255, 179, 71))
            : new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));
    }

    private void ApplyDashboardSettings()
    {
        if (_busDashboardDock is null || _busDashboardHandle is null || _busDashboardScale is null)
        {
            return;
        }

        _busDashboardScale.ScaleX = _hudSettings.DashboardScale;
        _busDashboardScale.ScaleY = _hudSettings.DashboardScale;
        _busDashboardDock.Opacity = _hudSettings.DashboardEnabled
            ? _hudSettings.DashboardOpacity
            : 0.42d;
        _busDashboardDock.Visibility = _hudSettings.DashboardEnabled || _hudLayoutEditMode
            ? Visibility.Visible
            : Visibility.Collapsed;
        _busDashboardHandle.Visibility = _hudLayoutEditMode ? Visibility.Visible : Visibility.Collapsed;

        if (_dashboardFuelPanel is not null)
            _dashboardFuelPanel.Visibility = _hudSettings.DashboardShowFuel ? Visibility.Visible : Visibility.Collapsed;
        if (_dashboardPedalsPanel is not null)
            _dashboardPedalsPanel.Visibility = _hudSettings.DashboardShowPedals ? Visibility.Visible : Visibility.Collapsed;
        if (_dashboardStatusPanel is not null)
            _dashboardStatusPanel.Visibility = _hudSettings.DashboardShowStatus ? Visibility.Visible : Visibility.Collapsed;

        ApplyDashboardPosition();
        UpdateMainDashboardButtonText();
    }

    private void ApplyDashboardPosition()
    {
        if (_busDashboardDock is null || _busDashboardTransform is null || ActualWidth <= 1d || ActualHeight <= 1d)
        {
            return;
        }

        var scale = Math.Clamp(_hudSettings.DashboardScale, 0.70d, 1.60d);
        var width = Math.Max(1d, _busDashboardDock.ActualWidth * scale);
        var height = Math.Max(1d, _busDashboardDock.ActualHeight * scale);
        var maxX = Math.Max(0d, ActualWidth - width - 16d);
        var maxY = Math.Max(0d, ActualHeight - height - 16d);
        SetDashboardPosition(
            8d + _hudSettings.DashboardX * maxX,
            8d + _hudSettings.DashboardY * maxY);
    }

    private void SetDashboardPosition(double x, double y)
    {
        if (_busDashboardDock is null || _busDashboardTransform is null)
        {
            return;
        }

        var scale = Math.Clamp(_hudSettings.DashboardScale, 0.70d, 1.60d);
        var width = Math.Max(1d, _busDashboardDock.ActualWidth * scale);
        var height = Math.Max(1d, _busDashboardDock.ActualHeight * scale);
        _busDashboardTransform.X = Math.Clamp(x, 8d, Math.Max(8d, ActualWidth - width - 8d));
        _busDashboardTransform.Y = Math.Clamp(y, 8d, Math.Max(8d, ActualHeight - height - 8d));
    }

    private void DashboardHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_hudLayoutEditMode || _busDashboardTransform is null || _busDashboardHandle is null)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            ResetDashboard();
            e.Handled = true;
            return;
        }

        _dashboardDragging = true;
        _dashboardDragStartMouse = e.GetPosition(OverlayRoot);
        _dashboardDragStartPosition = new Point(_busDashboardTransform.X, _busDashboardTransform.Y);
        _busDashboardHandle.CaptureMouse();
        e.Handled = true;
    }

    private void DashboardHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dashboardDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(OverlayRoot);
        SetDashboardPosition(
            _dashboardDragStartPosition.X + current.X - _dashboardDragStartMouse.X,
            _dashboardDragStartPosition.Y + current.Y - _dashboardDragStartMouse.Y);
        e.Handled = true;
    }

    private void DashboardHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dashboardDragging)
        {
            return;
        }

        _dashboardDragging = false;
        _busDashboardHandle?.ReleaseMouseCapture();
        SaveDashboardPosition();
        e.Handled = true;
    }

    private void SaveDashboardPosition()
    {
        if (_busDashboardDock is null || _busDashboardTransform is null)
        {
            return;
        }

        var scale = Math.Clamp(_hudSettings.DashboardScale, 0.70d, 1.60d);
        var width = Math.Max(1d, _busDashboardDock.ActualWidth * scale);
        var height = Math.Max(1d, _busDashboardDock.ActualHeight * scale);
        var maxX = Math.Max(1d, ActualWidth - width - 16d);
        var maxY = Math.Max(1d, ActualHeight - height - 16d);
        _hudSettings = _hudSettings with
        {
            DashboardX = Math.Clamp((_busDashboardTransform.X - 8d) / maxX, 0d, 1d),
            DashboardY = Math.Clamp((_busDashboardTransform.Y - 8d) / maxY, 0d, 1d)
        };
        MultiplayerSettingsStore.Save(_hudSettings);
    }

    private void ResetDashboard()
    {
        _hudSettings = _hudSettings with
        {
            DashboardX = 0.02d,
            DashboardY = 0.58d,
            DashboardScale = 1d,
            DashboardOpacity = 0.92d
        };
        MultiplayerSettingsStore.Save(_hudSettings);
        ApplyDashboardSettings();
    }

    private void InstallMainWindowDashboardButton()
    {
        if (_mainDashboardButton is not null || Application.Current.MainWindow is not NavBR.Client.MainWindow mainWindow)
        {
            return;
        }

        if (mainWindow.FindName("MultiplayerButton") is not Button multiplayerButton ||
            multiplayerButton.Parent is not Panel parent)
        {
            return;
        }

        _mainDashboardButton = new Button
        {
            MinWidth = 108,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _mainDashboardButton.Click += (_, _) => ShowDashboardOptions();
        var index = parent.Children.IndexOf(multiplayerButton);
        parent.Children.Insert(Math.Max(0, index), _mainDashboardButton);
        UpdateMainDashboardButtonText();
    }

    private void ShowDashboardOptions()
    {
        if (_mainDashboardButton is null)
        {
            return;
        }

        var menu = new ContextMenu();
        menu.Items.Add(NewToggleMenuItem(DashboardText("Ativar painel", "Enable dashboard"),
            _hudSettings.DashboardEnabled,
            value => SaveDashboardSettings(_hudSettings with { DashboardEnabled = value })));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewToggleMenuItem(DashboardText("Mostrar combustível", "Show fuel"),
            _hudSettings.DashboardShowFuel,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowFuel = value })));
        menu.Items.Add(NewToggleMenuItem(DashboardText("Mostrar acelerador/freio", "Show throttle/brake"),
            _hudSettings.DashboardShowPedals,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowPedals = value })));
        menu.Items.Add(NewToggleMenuItem(DashboardText("Mostrar indicadores", "Show indicators"),
            _hudSettings.DashboardShowStatus,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowStatus = value })));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewActionMenuItem(DashboardText("Diminuir painel", "Smaller dashboard"), () =>
            SaveDashboardSettings(_hudSettings with { DashboardScale = Math.Clamp(_hudSettings.DashboardScale - 0.10d, 0.70d, 1.60d) })));
        menu.Items.Add(NewActionMenuItem(DashboardText("Aumentar painel", "Larger dashboard"), () =>
            SaveDashboardSettings(_hudSettings with { DashboardScale = Math.Clamp(_hudSettings.DashboardScale + 0.10d, 0.70d, 1.60d) })));
        menu.Items.Add(NewActionMenuItem(DashboardText("Mais transparente", "More transparent"), () =>
            SaveDashboardSettings(_hudSettings with { DashboardOpacity = Math.Clamp(_hudSettings.DashboardOpacity - 0.08d, 0.45d, 1d) })));
        menu.Items.Add(NewActionMenuItem(DashboardText("Mais opaco", "More opaque"), () =>
            SaveDashboardSettings(_hudSettings with { DashboardOpacity = Math.Clamp(_hudSettings.DashboardOpacity + 0.08d, 0.45d, 1d) })));
        menu.Items.Add(new Separator());
        menu.Items.Add(NewActionMenuItem(DashboardText("Mover componentes do HUD", "Move HUD components"), () => SetLayoutEditMode(true)));
        menu.Items.Add(NewActionMenuItem(DashboardText("Resetar painel", "Reset dashboard"), ResetDashboard));

        _mainDashboardButton.ContextMenu = menu;
        menu.PlacementTarget = _mainDashboardButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static MenuItem NewToggleMenuItem(string text, bool value, Action<bool> update)
    {
        var item = new MenuItem { Header = text, IsCheckable = true, IsChecked = value };
        item.Click += (_, _) => update(item.IsChecked);
        return item;
    }

    private static MenuItem NewActionMenuItem(string text, Action action)
    {
        var item = new MenuItem { Header = text };
        item.Click += (_, _) => action();
        return item;
    }

    private void SaveDashboardSettings(MultiplayerSettings settings)
    {
        _hudSettings = settings;
        MultiplayerSettingsStore.Save(_hudSettings);
        ApplyDashboardSettings();
    }

    private void UpdateMainDashboardButtonText()
    {
        if (_mainDashboardButton is null)
        {
            return;
        }

        _mainDashboardButton.Content = LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => _hudSettings.DashboardEnabled ? "Painel ônibus ✓" : "Painel ônibus",
            "es" => _hudSettings.DashboardEnabled ? "Panel bus ✓" : "Panel bus",
            "de" => _hudSettings.DashboardEnabled ? "Bus-Panel ✓" : "Bus-Panel",
            "fr" => _hudSettings.DashboardEnabled ? "Tableau bus ✓" : "Tableau bus",
            _ => _hudSettings.DashboardEnabled ? "Bus panel ✓" : "Bus panel"
        };
    }

    private static string DashboardText(string pt, string en) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName == "pt" ? pt : en;

    private void DashboardSettingsSaved(MultiplayerSettings settings)
    {
        _hudSettings = settings;
        _ = Dispatcher.BeginInvoke(ApplyDashboardSettings);
    }

    private void DashboardWindow_Closed(object? sender, EventArgs e)
    {
        if (_busDashboardTimer is not null)
        {
            _busDashboardTimer.Stop();
            _busDashboardTimer.Tick -= DashboardTimer_Tick;
            _busDashboardTimer = null;
        }
        MultiplayerSettingsStore.SettingsSaved -= DashboardSettingsSaved;
        CompositionTarget.Rendering -= KeepLocalGpsMarkerHeadingUp;
        Closed -= DashboardWindow_Closed;
    }

    private void KeepLocalGpsMarkerHeadingUp(object? sender, EventArgs e)
    {
        if (LocalMarkerRotation.Angle != 0d)
        {
            LocalMarkerRotation.Angle = 0d;
        }
    }

    private void RemoveLegacyDriveCard()
    {
        foreach (var text in FindDashboardVisualChildren<TextBlock>(HudDock))
        {
            if (!string.Equals(text.Text, "NAVBR DRIVE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DependencyObject? current = text;
            while (current is not null && !ReferenceEquals(current, HudDock))
            {
                if (current is StackPanel stackPanel)
                {
                    stackPanel.Visibility = Visibility.Collapsed;
                    return;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }
    }

    private static IEnumerable<T> FindDashboardVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in FindDashboardVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
