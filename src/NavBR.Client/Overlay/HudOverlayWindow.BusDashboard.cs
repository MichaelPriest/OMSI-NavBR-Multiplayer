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
    private Border? _busDashboardMoveHandle;
    private TranslateTransform? _busDashboardTransform;
    private ScaleTransform? _busDashboardScale;
    private DispatcherTimer? _busDashboardTimer;
    private Button? _mainDashboardOptionsButton;

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

    private Border? _dashboardDoorsBadge;
    private TextBlock? _dashboardDoorsText;
    private Border? _dashboardTurnBadge;
    private TextBlock? _dashboardTurnText;
    private Border? _dashboardLightsBadge;
    private TextBlock? _dashboardLightsText;
    private Border? _dashboardParkingBadge;
    private TextBlock? _dashboardParkingText;
    private Border? _dashboardReverseBadge;
    private TextBlock? _dashboardReverseText;
    private Border? _dashboardWipersBadge;
    private TextBlock? _dashboardWipersText;

    private bool _busDashboardDragging;
    private Point _busDashboardDragStartMouse;
    private Point _busDashboardDragStartPosition;
    private bool _lastDashboardEditMode;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        EnsureBusDashboard();
    }

    private void EnsureBusDashboard()
    {
        if (_busDashboardDock is not null)
        {
            return;
        }

        _hudSettings = MultiplayerSettingsStore.Load();
        RemoveLegacyDriveCard();

        // Keep the local GPS pointer visually compact. The roadmap/route rotates
        // under it; the pointer itself must never rotate with HeadingDegrees.
        LocalMarker.LayoutTransform = new ScaleTransform(0.62d, 0.62d);
        LocalMarkerRotation.Angle = 0d;
        CompositionTarget.Rendering += KeepLocalGpsMarkerHeadingUp;

        _busDashboardTransform = new TranslateTransform();
        _busDashboardScale = new ScaleTransform(1d, 1d);

        var root = new StackPanel();
        _busDashboardMoveHandle = BuildDashboardMoveHandle();
        root.Children.Add(_busDashboardMoveHandle);
        root.Children.Add(BuildDashboardInstrumentPanel());

        _busDashboardDock = new Border
        {
            Width = 370d,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(235, 8, 13, 18)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(15d),
            Padding = new Thickness(9d),
            Child = root,
            RenderTransform = _busDashboardTransform,
            LayoutTransform = _busDashboardScale,
            Panel = { ZIndex = 1002 }
        };

        OverlayRoot.Children.Add(_busDashboardDock);

        _busDashboardDock.SizeChanged += (_, _) =>
        {
            if (!_busDashboardDragging)
            {
                ApplyBusDashboardPosition();
            }
        };
        SizeChanged += (_, _) =>
        {
            if (!_busDashboardDragging)
            {
                ApplyBusDashboardPosition();
            }
        };

        _busDashboardTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _busDashboardTimer.Tick += BusDashboardTimer_Tick;
        _busDashboardTimer.Start();

        MultiplayerSettingsStore.SettingsSaved += BusDashboardSettingsSaved;
        Closed += BusDashboardWindow_Closed;

        InstallMainWindowDashboardButton();
        ApplyBusDashboardSettings();
        RenderBusDashboard();
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ApplyBusDashboardPosition);
    }

    private Border BuildDashboardMoveHandle()
    {
        var label = new TextBlock
        {
            Text = "▦  PAINEL DO ÔNIBUS • ARRASTE PARA MOVER",
            Foreground = new SolidColorBrush(Color.FromRgb(255, 179, 71)),
            FontSize = 10d,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };

        var handle = new Border
        {
            Margin = new Thickness(0, 0, 0, 7),
            Padding = new Thickness(8, 5, 8, 5),
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(225, 24, 31, 39)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 157, 36)),
            BorderThickness = new Thickness(1),
            Child = label,
            Cursor = Cursors.SizeAll,
            Visibility = Visibility.Collapsed
        };

        handle.MouseLeftButtonDown += BusDashboardMoveHandle_MouseLeftButtonDown;
        handle.MouseMove += BusDashboardMoveHandle_MouseMove;
        handle.MouseLeftButtonUp += BusDashboardMoveHandle_MouseLeftButtonUp;
        return handle;
    }

    private FrameworkElement BuildDashboardInstrumentPanel()
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var speedPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 9, 0)
        };
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
            HorizontalAlignment = HorizontalAlignment.Center,
            LineHeight = 42
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
        speedPanel.Child = speedStack;
        Grid.SetColumn(speedPanel, 0);
        panel.Children.Add(speedPanel);

        var rightStack = new StackPanel();
        Grid.SetColumn(rightStack, 1);
        panel.Children.Add(rightStack);

        _dashboardFuelPanel = BuildFuelPanel();
        _dashboardPedalsPanel = BuildPedalsPanel();
        _dashboardStatusPanel = BuildStatusPanel();
        rightStack.Children.Add(_dashboardFuelPanel);
        rightStack.Children.Add(_dashboardPedalsPanel);
        rightStack.Children.Add(_dashboardStatusPanel);

        return panel;
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

        _dashboardFuelBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_dashboardFuelBar, 1);
        grid.Children.Add(_dashboardFuelBar);

        _dashboardFuelText = new TextBlock
        {
            Text = "—",
            Foreground = Brushes.White,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(7, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_dashboardFuelText, 2);
        grid.Children.Add(_dashboardFuelText);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 5),
            Child = grid
        };
    }

    private Border BuildPedalsPanel()
    {
        var stack = new StackPanel();
        stack.Children.Add(BuildPedalRow("ACEL", out _dashboardThrottleBar, out _dashboardThrottleText));
        stack.Children.Add(BuildPedalRow("FREIO", out _dashboardBrakeBar, out _dashboardBrakeText));

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(72, 255, 255, 255)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 5),
            Child = stack
        };
    }

    private static FrameworkElement BuildPedalRow(
        string labelText,
        out ProgressBar bar,
        out TextBlock valueText)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

        var label = new TextBlock
        {
            Text = labelText,
            Foreground = new SolidColorBrush(Color.FromArgb(175, 255, 255, 255)),
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(bar, 1);
        grid.Children.Add(bar);

        valueText = new TextBlock
        {
            Text = "—",
            Foreground = Brushes.White,
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(valueText, 2);
        grid.Children.Add(valueText);
        return grid;
    }

    private Border BuildStatusPanel()
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        (_dashboardDoorsBadge, _dashboardDoorsText) = BuildIndicator("PORTAS");
        (_dashboardTurnBadge, _dashboardTurnText) = BuildIndicator("SETA");
        (_dashboardLightsBadge, _dashboardLightsText) = BuildIndicator("LUZ");
        (_dashboardParkingBadge, _dashboardParkingText) = BuildIndicator("P");
        (_dashboardReverseBadge, _dashboardReverseText) = BuildIndicator("R");
        (_dashboardWipersBadge, _dashboardWipersText) = BuildIndicator("LIMP");

        wrap.Children.Add(_dashboardDoorsBadge);
        wrap.Children.Add(_dashboardTurnBadge);
        wrap.Children.Add(_dashboardLightsBadge);
        wrap.Children.Add(_dashboardParkingBadge);
        wrap.Children.Add(_dashboardReverseBadge);
        wrap.Children.Add(_dashboardWipersBadge);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(65, 255, 255, 255)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(5, 4, 5, 3),
            Child = wrap
        };
    }

    private static (Border Badge, TextBlock Text) BuildIndicator(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
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
        return (badge, label);
    }

    private void BusDashboardTimer_Tick(object? sender, EventArgs e)
    {
        if (_lastDashboardEditMode != _hudLayoutEditMode)
        {
            _lastDashboardEditMode = _hudLayoutEditMode;
            ApplyBusDashboardSettings();
        }

        RenderBusDashboard();
    }

    private void RenderBusDashboard()
    {
        if (_busDashboardDock is null || _dashboardSpeedText is null)
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
            SetAllIndicatorsInactive();
            return;
        }

        _dashboardSpeedText.Text = Math.Clamp(telemetry.SpeedKph, 0d, 999d).ToString("F0", LocalizationService.CurrentCulture);
        if (_dashboardAccelerationText is not null)
        {
            _dashboardAccelerationText.Text = telemetry.AccelerationMps2 is double acceleration
                ? $"{acceleration:+0.0;-0.0;0.0} m/s²"
                : "acel. —";
        }

        SetProgress(_dashboardFuelBar, _dashboardFuelText, telemetry.FuelPercent);
        SetProgress(_dashboardThrottleBar, _dashboardThrottleText, telemetry.ThrottlePercent);
        SetProgress(_dashboardBrakeBar, _dashboardBrakeText, telemetry.BrakePercent);

        var doorsOpen = telemetry.Doors != VehicleDoorFlags.None;
        SetIndicator(_dashboardDoorsBadge, _dashboardDoorsText, doorsOpen, doorsOpen ? "PORTAS!" : "PORTAS");

        var turnActive = telemetry.TurnSignal != TurnSignalState.Off;
        var turnText = telemetry.TurnSignal switch
        {
            TurnSignalState.Left => "◀",
            TurnSignalState.Right => "▶",
            TurnSignalState.Hazard => "⚠",
            _ => "SETA"
        };
        SetIndicator(_dashboardTurnBadge, _dashboardTurnText, turnActive, turnText);

        var lightsActive = telemetry.Lights != VehicleLightFlags.None;
        SetIndicator(_dashboardLightsBadge, _dashboardLightsText, lightsActive, "LUZ");
        SetIndicator(_dashboardParkingBadge, _dashboardParkingText, telemetry.ParkingBrakeActive, "P");
        SetIndicator(_dashboardReverseBadge, _dashboardReverseText, telemetry.ReverseGear, "R");
        SetIndicator(_dashboardWipersBadge, _dashboardWipersText, telemetry.WipersActive, "LIMP");
    }

    private static void SetProgress(ProgressBar? bar, TextBlock? text, double? value)
    {
        if (bar is null || text is null)
        {
            return;
        }

        if (value is not double number || !double.IsFinite(number))
        {
            bar.Value = 0;
            text.Text = "—";
            return;
        }

        number = Math.Clamp(number, 0d, 100d);
        bar.Value = number;
        text.Text = $"{number:F0}%";
    }

    private static void SetIndicator(Border? badge, TextBlock? text, bool active, string label)
    {
        if (badge is null || text is null)
        {
            return;
        }

        text.Text = label;
        text.Foreground = active
            ? Brushes.White
            : new SolidColorBrush(Color.FromArgb(145, 255, 255, 255));
        badge.Background = active
            ? new SolidColorBrush(Color.FromArgb(205, 255, 132, 0))
            : new SolidColorBrush(Color.FromArgb(55, 255, 255, 255));
        badge.BorderBrush = active
            ? new SolidColorBrush(Color.FromArgb(235, 255, 179, 71))
            : new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));
    }

    private void SetAllIndicatorsInactive()
    {
        SetIndicator(_dashboardDoorsBadge, _dashboardDoorsText, false, "PORTAS");
        SetIndicator(_dashboardTurnBadge, _dashboardTurnText, false, "SETA");
        SetIndicator(_dashboardLightsBadge, _dashboardLightsText, false, "LUZ");
        SetIndicator(_dashboardParkingBadge, _dashboardParkingText, false, "P");
        SetIndicator(_dashboardReverseBadge, _dashboardReverseText, false, "R");
        SetIndicator(_dashboardWipersBadge, _dashboardWipersText, false, "LIMP");
    }

    private void ApplyBusDashboardSettings()
    {
        if (_busDashboardDock is null || _busDashboardMoveHandle is null || _busDashboardScale is null)
        {
            return;
        }

        _busDashboardScale.ScaleX = _hudSettings.DashboardScale;
        _busDashboardScale.ScaleY = _hudSettings.DashboardScale;
        _busDashboardDock.Opacity = _hudSettings.DashboardEnabled ? _hudSettings.DashboardOpacity : 0.42d;
        _busDashboardDock.Visibility = _hudSettings.DashboardEnabled || _hudLayoutEditMode
            ? Visibility.Visible
            : Visibility.Collapsed;
        _busDashboardMoveHandle.Visibility = _hudLayoutEditMode
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (_dashboardFuelPanel is not null)
        {
            _dashboardFuelPanel.Visibility = _hudSettings.DashboardShowFuel ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_dashboardPedalsPanel is not null)
        {
            _dashboardPedalsPanel.Visibility = _hudSettings.DashboardShowPedals ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_dashboardStatusPanel is not null)
        {
            _dashboardStatusPanel.Visibility = _hudSettings.DashboardShowStatus ? Visibility.Visible : Visibility.Collapsed;
        }

        ApplyBusDashboardPosition();
        UpdateMainDashboardButtonText();
    }

    private void ApplyBusDashboardPosition()
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
        SetBusDashboardPosition(
            8d + _hudSettings.DashboardX * maxX,
            8d + _hudSettings.DashboardY * maxY);
    }

    private void SetBusDashboardPosition(double x, double y)
    {
        if (_busDashboardDock is null || _busDashboardTransform is null)
        {
            return;
        }

        var scale = Math.Clamp(_hudSettings.DashboardScale, 0.70d, 1.60d);
        var width = Math.Max(1d, _busDashboardDock.ActualWidth * scale);
        var height = Math.Max(1d, _busDashboardDock.ActualHeight * scale);
        var maxX = Math.Max(8d, ActualWidth - width - 8d);
        var maxY = Math.Max(8d, ActualHeight - height - 8d);
        _busDashboardTransform.X = Math.Clamp(x, 8d, maxX);
        _busDashboardTransform.Y = Math.Clamp(y, 8d, maxY);
    }

    private void BusDashboardMoveHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_hudLayoutEditMode || _busDashboardTransform is null || _busDashboardMoveHandle is null)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            ResetBusDashboardPosition();
            e.Handled = true;
            return;
        }

        _busDashboardDragging = true;
        _busDashboardDragStartMouse = e.GetPosition(OverlayRoot);
        _busDashboardDragStartPosition = new Point(_busDashboardTransform.X, _busDashboardTransform.Y);
        _busDashboardMoveHandle.CaptureMouse();
        e.Handled = true;
    }

    private void BusDashboardMoveHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_busDashboardDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(OverlayRoot);
        SetBusDashboardPosition(
            _busDashboardDragStartPosition.X + current.X - _busDashboardDragStartMouse.X,
            _busDashboardDragStartPosition.Y + current.Y - _busDashboardDragStartMouse.Y);
        e.Handled = true;
    }

    private void BusDashboardMoveHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_busDashboardDragging)
        {
            return;
        }

        SaveBusDashboardPosition();
        _busDashboardDragging = false;
        _busDashboardMoveHandle?.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void SaveBusDashboardPosition()
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

    private void ResetBusDashboardPosition()
    {
        _hudSettings = _hudSettings with
        {
            DashboardX = 0.02d,
            DashboardY = 0.58d,
            DashboardScale = 1d,
            DashboardOpacity = 0.92d
        };
        MultiplayerSettingsStore.Save(_hudSettings);
        ApplyBusDashboardSettings();
    }

    private void InstallMainWindowDashboardButton()
    {
        if (_mainDashboardOptionsButton is not null || Application.Current.MainWindow is not NavBR.Client.MainWindow mainWindow)
        {
            return;
        }

        if (mainWindow.FindName("MultiplayerButton") is not Button multiplayerButton ||
            multiplayerButton.Parent is not Panel parent)
        {
            return;
        }

        var button = new Button
        {
            MinWidth = 108,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Click += (_, _) => ShowBusDashboardOptions(button);

        var index = parent.Children.IndexOf(multiplayerButton);
        parent.Children.Insert(Math.Max(0, index), button);
        _mainDashboardOptionsButton = button;
        UpdateMainDashboardButtonText();
    }

    private void ShowBusDashboardOptions(Button owner)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateDashboardToggleItem(
            DashboardLabel("Ativar painel", "Enable dashboard"),
            _hudSettings.DashboardEnabled,
            value => SaveDashboardSettings(_hudSettings with { DashboardEnabled = value })));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateDashboardToggleItem(
            DashboardLabel("Mostrar combustível", "Show fuel"),
            _hudSettings.DashboardShowFuel,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowFuel = value })));
        menu.Items.Add(CreateDashboardToggleItem(
            DashboardLabel("Mostrar acelerador/freio", "Show throttle/brake"),
            _hudSettings.DashboardShowPedals,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowPedals = value })));
        menu.Items.Add(CreateDashboardToggleItem(
            DashboardLabel("Mostrar indicadores", "Show indicators"),
            _hudSettings.DashboardShowStatus,
            value => SaveDashboardSettings(_hudSettings with { DashboardShowStatus = value })));
        menu.Items.Add(new Separator());

        var smaller = new MenuItem { Header = DashboardLabel("Diminuir painel", "Smaller dashboard") };
        smaller.Click += (_, _) => SaveDashboardSettings(_hudSettings with
        {
            DashboardScale = Math.Clamp(_hudSettings.DashboardScale - 0.10d, 0.70d, 1.60d)
        });
        menu.Items.Add(smaller);

        var larger = new MenuItem { Header = DashboardLabel("Aumentar painel", "Larger dashboard") };
        larger.Click += (_, _) => SaveDashboardSettings(_hudSettings with
        {
            DashboardScale = Math.Clamp(_hudSettings.DashboardScale + 0.10d, 0.70d, 1.60d)
        });
        menu.Items.Add(larger);

        var transparent = new MenuItem { Header = DashboardLabel("Mais transparente", "More transparent") };
        transparent.Click += (_, _) => SaveDashboardSettings(_hudSettings with
        {
            DashboardOpacity = Math.Clamp(_hudSettings.DashboardOpacity - 0.08d, 0.45d, 1d)
        });
        menu.Items.Add(transparent);

        var opaque = new MenuItem { Header = DashboardLabel("Mais opaco", "More opaque") };
        opaque.Click += (_, _) => SaveDashboardSettings(_hudSettings with
        {
            DashboardOpacity = Math.Clamp(_hudSettings.DashboardOpacity + 0.08d, 0.45d, 1d)
        });
        menu.Items.Add(opaque);
        menu.Items.Add(new Separator());

        var move = new MenuItem { Header = DashboardLabel("Mover componentes do HUD", "Move HUD components") };
        move.Click += (_, _) => SetLayoutEditMode(true);
        menu.Items.Add(move);

        var reset = new MenuItem { Header = DashboardLabel("Resetar painel", "Reset dashboard") };
        reset.Click += (_, _) => ResetBusDashboardPosition();
        menu.Items.Add(reset);

        owner.ContextMenu = menu;
        menu.PlacementTarget = owner;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static MenuItem CreateDashboardToggleItem(string label, bool isChecked, Action<bool> update)
    {
        var item = new MenuItem
        {
            Header = label,
            IsCheckable = true,
            IsChecked = isChecked
        };
        item.Click += (_, _) => update(item.IsChecked);
        return item;
    }

    private void SaveDashboardSettings(MultiplayerSettings settings)
    {
        _hudSettings = settings;
        MultiplayerSettingsStore.Save(_hudSettings);
        ApplyBusDashboardSettings();
    }

    private void UpdateMainDashboardButtonText()
    {
        if (_mainDashboardOptionsButton is null)
        {
            return;
        }

        var enabled = _hudSettings.DashboardEnabled;
        _mainDashboardOptionsButton.Content = LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => enabled ? "Painel ônibus ✓" : "Painel ônibus",
            "es" => enabled ? "Panel bus ✓" : "Panel bus",
            "de" => enabled ? "Bus-Panel ✓" : "Bus-Panel",
            "fr" => enabled ? "Tableau bus ✓" : "Tableau bus",
            _ => enabled ? "Bus panel ✓" : "Bus panel"
        };
    }

    private static string DashboardLabel(string portuguese, string english) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName == "pt" ? portuguese : english;

    private void BusDashboardSettingsSaved(MultiplayerSettings settings)
    {
        _hudSettings = settings;
        Dispatcher.InvokeAsync(ApplyBusDashboardSettings);
    }

    private void BusDashboardWindow_Closed(object? sender, EventArgs e)
    {
        if (_busDashboardTimer is not null)
        {
            _busDashboardTimer.Stop();
            _busDashboardTimer.Tick -= BusDashboardTimer_Tick;
            _busDashboardTimer = null;
        }

        MultiplayerSettingsStore.SettingsSaved -= BusDashboardSettingsSaved;
        CompositionTarget.Rendering -= KeepLocalGpsMarkerHeadingUp;
        Closed -= BusDashboardWindow_Closed;
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
        foreach (var text in FindVisualChildren<TextBlock>(HudDock))
        {
            if (!string.Equals(text.Text, "NAVBR DRIVE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DependencyObject? current = text;
            while (current is not null && !ReferenceEquals(current, HudDock))
            {
                if (current is Border border)
                {
                    border.Visibility = Visibility.Collapsed;
                    return;
                }

                current = VisualTreeHelper.GetParent(current);
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
