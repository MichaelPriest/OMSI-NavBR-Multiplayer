using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private MultiplayerSettings _hudSettings = MultiplayerSettingsStore.Load();
    private bool _hudLayoutInitialized;
    private bool _hudLayoutEditMode;
    private bool _hudDragging;
    private Point _hudDragStartMouse;
    private Point _hudDragStartPosition;
    private double _renderedHudZoom = 1d;
    private Button? _mainHudLayoutButton;

    public bool IsLayoutEditMode => _hudLayoutEditMode;

    public event Action<bool>? LayoutEditModeChanged;

    public void SetLayoutEditMode(bool enabled)
    {
        _hudLayoutEditMode = enabled;
        HudMoveHandle.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        SetInteractive(enabled || _chatInteractive);

        if (enabled)
        {
            _hudVisibilityTimer?.Stop();
            OverlayRoot.Visibility = Visibility.Visible;
            _hudVisibleForOmsi = true;
            HudMoveHandleText.Text = MoveHandleText();
        }
        else
        {
            if (_hudDragging)
            {
                EndHudDrag(save: true);
            }

            if (_hudVisibilityTimer is not null && !_hudVisibilityTimer.IsEnabled)
            {
                _hudVisibilityTimer.Start();
            }
        }

        UpdateMainHudLayoutButtonText();
        LayoutEditModeChanged?.Invoke(enabled);
        if (!enabled)
        {
            RefreshHudVisibility();
        }
    }

    public void ResetHudLayout()
    {
        _hudSettings = _hudSettings with
        {
            HudX = 0.02d,
            HudY = 1.0d,
            HudZoom = 1.0d,
            HudMapOpacity = 0.58d
        };
        MultiplayerSettingsStore.Save(_hudSettings);
        MiniMapImage.Opacity = _hudSettings.HudMapOpacity;
        _renderedHudZoom = _hudSettings.HudZoom;
        ApplyHudLayoutPosition();
        RenderEnhancedMiniMap();
    }

    private void HudDock_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hudLayoutInitialized)
        {
            return;
        }

        _hudLayoutInitialized = true;
        _hudSettings = MultiplayerSettingsStore.Load();
        _renderedHudZoom = _hudSettings.HudZoom;
        MiniMapImage.Opacity = _hudSettings.HudMapOpacity;
        MiniMapZoomText.Text = $"{_hudSettings.HudZoom:F1}×";

        HudDock.SizeChanged += (_, _) =>
        {
            if (!_hudDragging)
            {
                ApplyHudLayoutPosition();
            }
        };
        SizeChanged += (_, _) =>
        {
            if (!_hudDragging)
            {
                ApplyHudLayoutPosition();
            }
        };

        ApplyHudLayoutPosition();
        StartEnhancedMapRendering();
        InstallMainWindowLayoutButton();
    }

    private void InstallMainWindowLayoutButton()
    {
        if (_mainHudLayoutButton is not null || Application.Current.MainWindow is not NavBR.Client.MainWindow mainWindow)
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
            MinWidth = 110,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Click += (_, _) => SetLayoutEditMode(!IsLayoutEditMode);

        var index = parent.Children.IndexOf(multiplayerButton);
        parent.Children.Insert(Math.Max(0, index), button);
        _mainHudLayoutButton = button;
        UpdateMainHudLayoutButtonText();

        if (mainWindow.FindName("LanguageComboBox") is ComboBox languageComboBox)
        {
            languageComboBox.SelectionChanged += (_, _) => UpdateMainHudLayoutButtonText();
        }
    }

    private void UpdateMainHudLayoutButtonText()
    {
        if (_mainHudLayoutButton is null)
        {
            return;
        }

        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        _mainHudLayoutButton.Content = (language, _hudLayoutEditMode) switch
        {
            ("pt", false) => "Mover HUD",
            ("pt", true) => "Bloquear HUD",
            ("es", false) => "Mover HUD",
            ("es", true) => "Bloquear HUD",
            ("de", false) => "HUD verschieben",
            ("de", true) => "HUD sperren",
            ("fr", false) => "Déplacer HUD",
            ("fr", true) => "Verrouiller HUD",
            (_, false) => "Move HUD",
            _ => "Lock HUD"
        };
    }

    private static string MoveHandleText()
    {
        return LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => "Arraste para mover • roda do mouse = zoom • duplo clique = reset",
            "es" => "Arrastra para mover • rueda = zoom • doble clic = reset",
            "de" => "Ziehen zum Verschieben • Mausrad = Zoom • Doppelklick = Reset",
            "fr" => "Glisser pour déplacer • molette = zoom • double-clic = reset",
            _ => "Drag to move • mouse wheel = zoom • double-click = reset"
        };
    }

    private void HudMoveDoneButton_Click(object sender, RoutedEventArgs e)
    {
        SetLayoutEditMode(false);
        e.Handled = true;
    }

    private void HudMoveHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_hudLayoutEditMode)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            ResetHudLayout();
            e.Handled = true;
            return;
        }

        _hudDragging = true;
        _hudDragStartMouse = e.GetPosition(OverlayRoot);
        _hudDragStartPosition = new Point(HudDockTransform.X, HudDockTransform.Y);
        HudMoveHandle.CaptureMouse();
        e.Handled = true;
    }

    private void HudMoveHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_hudDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(OverlayRoot);
        var x = _hudDragStartPosition.X + current.X - _hudDragStartMouse.X;
        var y = _hudDragStartPosition.Y + current.Y - _hudDragStartMouse.Y;
        SetHudDockPosition(x, y);
        e.Handled = true;
    }

    private void HudMoveHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_hudDragging)
        {
            return;
        }

        EndHudDrag(save: true);
        e.Handled = true;
    }

    private void EndHudDrag(bool save)
    {
        _hudDragging = false;
        HudMoveHandle.ReleaseMouseCapture();

        if (!save)
        {
            return;
        }

        var maxX = Math.Max(1d, ActualWidth - Math.Max(1d, HudDock.ActualWidth) - 16d);
        var maxY = Math.Max(1d, ActualHeight - Math.Max(1d, HudDock.ActualHeight) - 16d);
        _hudSettings = _hudSettings with
        {
            HudX = Math.Clamp((HudDockTransform.X - 8d) / maxX, 0d, 1d),
            HudY = Math.Clamp((HudDockTransform.Y - 8d) / maxY, 0d, 1d)
        };
        MultiplayerSettingsStore.Save(_hudSettings);
    }

    private void HudDock_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_hudLayoutEditMode)
        {
            return;
        }

        var multiplier = e.Delta > 0 ? 1.10d : 1d / 1.10d;
        _hudSettings = _hudSettings with
        {
            HudZoom = Math.Clamp(_hudSettings.HudZoom * multiplier, 0.65d, 2.25d)
        };
        _renderedHudZoom = _hudSettings.HudZoom;
        MultiplayerSettingsStore.Save(_hudSettings);
        MiniMapZoomText.Text = $"{_hudSettings.HudZoom:F1}×";
        RenderEnhancedMiniMap();
        e.Handled = true;
    }

    private void ApplyHudLayoutPosition()
    {
        if (!_hudLayoutInitialized || ActualWidth <= 1d || ActualHeight <= 1d)
        {
            return;
        }

        var dockWidth = Math.Max(1d, HudDock.ActualWidth);
        var dockHeight = Math.Max(1d, HudDock.ActualHeight);
        var maxX = Math.Max(0d, ActualWidth - dockWidth - 16d);
        var maxY = Math.Max(0d, ActualHeight - dockHeight - 16d);
        SetHudDockPosition(8d + _hudSettings.HudX * maxX, 8d + _hudSettings.HudY * maxY);
    }

    private void SetHudDockPosition(double x, double y)
    {
        var dockWidth = Math.Max(1d, HudDock.ActualWidth);
        var dockHeight = Math.Max(1d, HudDock.ActualHeight);
        var maxX = Math.Max(8d, ActualWidth - dockWidth - 8d);
        var maxY = Math.Max(8d, ActualHeight - dockHeight - 8d);
        HudDockTransform.X = Math.Clamp(x, 8d, maxX);
        HudDockTransform.Y = Math.Clamp(y, 8d, maxY);
    }

    private double GetSmoothedHudZoom(VehicleTelemetry? telemetry)
    {
        var speedFactor = telemetry?.SpeedKph switch
        {
            < 15d => 1.18d,
            < 35d => 1.08d,
            > 70d => 0.82d,
            > 50d => 0.92d,
            _ => 1d
        };
        var target = Math.Clamp(_hudSettings.HudZoom * speedFactor, 0.65d, 2.25d);
        _renderedHudZoom += (target - _renderedHudZoom) * 0.16d;
        MiniMapZoomText.Text = $"{_renderedHudZoom:F1}×";
        return _renderedHudZoom;
    }
}
