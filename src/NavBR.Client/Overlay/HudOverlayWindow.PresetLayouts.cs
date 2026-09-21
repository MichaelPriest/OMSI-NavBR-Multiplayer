using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

internal static class HudPresetLayoutBootstrap
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
                DispatcherPriority.ApplicationIdle,
                window.InitializeHudPresetLayouts);
        }
    }
}

public partial class HudOverlayWindow
{
    private bool _hudPresetLayoutsInitialized;
    private Border? _presetSpeedPanel;

    internal void InitializeHudPresetLayouts()
    {
        if (_hudPresetLayoutsInitialized)
        {
            return;
        }

        if (_busDashboardDock is null || _dashboardSpeedText is null)
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                InitializeHudPresetLayouts);
            return;
        }

        _hudPresetLayoutsInitialized = true;
        _presetSpeedPanel = (_dashboardSpeedText.Parent as FrameworkElement)?.Parent as Border;
        MultiplayerSettingsStore.SettingsSaved += HudPresetLayouts_SettingsSaved;
        Closed += HudPresetLayouts_Closed;
        ApplyHudPresetLayout(MultiplayerSettingsStore.Load());
    }

    private void HudPresetLayouts_SettingsSaved(MultiplayerSettings settings)
    {
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            () => ApplyHudPresetLayout(settings));
    }

    private void HudPresetLayouts_Closed(object? sender, EventArgs e)
    {
        MultiplayerSettingsStore.SettingsSaved -= HudPresetLayouts_SettingsSaved;
        Closed -= HudPresetLayouts_Closed;
    }

    private void ApplyHudPresetLayout(MultiplayerSettings settings)
    {
        if (_busDashboardDock is null || _dashboardSpeedText is null)
        {
            return;
        }

        var preset = HudProfileCatalog.ResolvePreset(settings.DashboardPreset).Id;
        ResetPresetLayoutDefaults();

        switch (preset)
        {
            case "immersive-operation":
                ApplyImmersiveOperationPreset();
                break;
            case "compact":
                ApplyCompactPreset();
                break;
            case "full":
                ApplyFullPreset();
                break;
            case "digital-cluster":
                ApplyDigitalClusterPreset();
                break;
            case "lcd-amber":
                ApplyLcdAmberPreset();
                break;
            case "transparent":
                ApplyTransparentPreset();
                break;
            default:
                ApplyNormalPreset();
                break;
        }
    }

    private void ResetPresetLayoutDefaults()
    {
        if (_presetSpeedPanel is not null)
        {
            _presetSpeedPanel.ClearValue(FrameworkElement.WidthProperty);
            _presetSpeedPanel.ClearValue(FrameworkElement.HeightProperty);
            _presetSpeedPanel.CornerRadius = new CornerRadius(12d);
            _presetSpeedPanel.BorderThickness = new Thickness(0d);
            _presetSpeedPanel.Margin = new Thickness(0d, 0d, 9d, 0d);
            _presetSpeedPanel.Padding = new Thickness(8d);
            _presetSpeedPanel.Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
        }

        _dashboardSpeedText!.FontFamily = new FontFamily("Bahnschrift");
        _dashboardSpeedText.FontSize = 49d;
        _dashboardSpeedText.FontWeight = FontWeights.SemiBold;
        _dashboardSpeedText.TextAlignment = TextAlignment.Center;

        if (_dashboardAccelerationText is not null)
        {
            _dashboardAccelerationText.Visibility = Visibility.Visible;
            _dashboardAccelerationText.FontFamily = new FontFamily("Bahnschrift");
            _dashboardAccelerationText.FontSize = 9d;
        }

        if (_alpha12DestinationText is not null)
        {
            _alpha12DestinationText.FontFamily = new FontFamily("Bahnschrift");
            _alpha12DestinationText.FontSize = 17d;
        }
        if (_alpha12NextStopText is not null)
        {
            _alpha12NextStopText.FontFamily = new FontFamily("Bahnschrift");
            _alpha12NextStopText.FontSize = 14.5d;
        }

        _busDashboardDock.CornerRadius = new CornerRadius(18d);
        _busDashboardDock.BorderThickness = new Thickness(1.2d);
        _busDashboardDock.Padding = new Thickness(10d);
    }

    private void ApplyImmersiveOperationPreset()
    {
        _dashboardSpeedText!.FontSize = 44d;
        _dashboardSpeedText.FontWeight = FontWeights.Bold;

        if (_presetSpeedPanel is not null)
        {
            _presetSpeedPanel.Width = 124d;
            _presetSpeedPanel.Height = 104d;
            _presetSpeedPanel.CornerRadius = new CornerRadius(14d);
            _presetSpeedPanel.BorderBrush = new SolidColorBrush(Color.FromArgb(150, 80, 170, 235));
            _presetSpeedPanel.BorderThickness = new Thickness(1d);
            _presetSpeedPanel.Background = new SolidColorBrush(Color.FromArgb(205, 5, 17, 27));
            _presetSpeedPanel.Padding = new Thickness(8d);
        }

        if (_dashboardAccelerationText is not null)
        {
            _dashboardAccelerationText.FontSize = 8.5d;
        }

        if (_alpha12DestinationText is not null)
        {
            _alpha12DestinationText.FontSize = 20d;
            _alpha12DestinationText.FontWeight = FontWeights.Bold;
        }

        if (_alpha12NextStopText is not null)
        {
            _alpha12NextStopText.FontSize = 15.5d;
            _alpha12NextStopText.FontWeight = FontWeights.SemiBold;
        }

        _busDashboardDock.CornerRadius = new CornerRadius(13d);
        _busDashboardDock.BorderThickness = new Thickness(1d);
        _busDashboardDock.Padding = new Thickness(10d, 8d, 10d, 8d);
        _busDashboardDock.Background = new SolidColorBrush(Color.FromArgb(220, 4, 15, 24));
        _busDashboardDock.BorderBrush = new SolidColorBrush(Color.FromArgb(145, 61, 139, 194));
    }

    private void ApplyCompactPreset()
    {
        _dashboardSpeedText!.FontSize = 40d;
        if (_dashboardAccelerationText is not null)
        {
            _dashboardAccelerationText.Visibility = Visibility.Collapsed;
        }
        if (_presetSpeedPanel is not null)
        {
            _presetSpeedPanel.Padding = new Thickness(6d);
            _presetSpeedPanel.CornerRadius = new CornerRadius(9d);
        }
        if (_alpha12DestinationText is not null) _alpha12DestinationText.FontSize = 14d;
        if (_alpha12NextStopText is not null) _alpha12NextStopText.FontSize = 11.5d;
        _busDashboardDock.CornerRadius = new CornerRadius(13d);
        _busDashboardDock.Padding = new Thickness(7d);
    }

    private void ApplyNormalPreset()
    {
        _dashboardSpeedText!.FontSize = 49d;
    }

    private void ApplyFullPreset()
    {
        _dashboardSpeedText!.FontSize = 54d;
        if (_presetSpeedPanel is not null)
        {
            _presetSpeedPanel.Width = 138d;
        }
        if (_alpha12DestinationText is not null) _alpha12DestinationText.FontSize = 18d;
    }

    private void ApplyDigitalClusterPreset()
    {
        _dashboardSpeedText!.FontSize = 58d;
        _dashboardSpeedText.FontWeight = FontWeights.Bold;
        if (_presetSpeedPanel is not null)
        {
            _presetSpeedPanel.Width = 154d;
            _presetSpeedPanel.Height = 154d;
            _presetSpeedPanel.CornerRadius = new CornerRadius(77d);
            _presetSpeedPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(42, 144, 219));
            _presetSpeedPanel.BorderThickness = new Thickness(2d);
            _presetSpeedPanel.Background = new RadialGradientBrush(
                Color.FromRgb(11, 35, 52),
                Color.FromRgb(2, 11, 18));
        }
        if (_dashboardAccelerationText is not null)
        {
            _dashboardAccelerationText.FontSize = 8.5d;
        }
        _busDashboardDock.CornerRadius = new CornerRadius(24d);
        _busDashboardDock.BorderThickness = new Thickness(1.5d);
    }

    private void ApplyLcdAmberPreset()
    {
        var mono = new FontFamily("Consolas");
        _dashboardSpeedText!.FontFamily = mono;
        _dashboardSpeedText.FontSize = 47d;
        _dashboardSpeedText.FontWeight = FontWeights.Bold;
        _dashboardSpeedText.Foreground = new SolidColorBrush(Color.FromRgb(255, 174, 34));
        if (_dashboardAccelerationText is not null)
        {
            _dashboardAccelerationText.Visibility = Visibility.Collapsed;
        }
        if (_alpha12DestinationText is not null)
        {
            _alpha12DestinationText.FontFamily = mono;
            _alpha12DestinationText.Foreground = new SolidColorBrush(Color.FromRgb(255, 174, 34));
        }
        if (_alpha12NextStopText is not null)
        {
            _alpha12NextStopText.FontFamily = mono;
            _alpha12NextStopText.Foreground = new SolidColorBrush(Color.FromRgb(255, 190, 67));
        }
        if (_presetSpeedPanel is not null)
        {
            _presetSpeedPanel.CornerRadius = new CornerRadius(3d);
            _presetSpeedPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(105, 64, 13));
            _presetSpeedPanel.BorderThickness = new Thickness(1d);
            _presetSpeedPanel.Background = new SolidColorBrush(Color.FromRgb(13, 8, 2));
        }
        _busDashboardDock.CornerRadius = new CornerRadius(5d);
        _busDashboardDock.BorderBrush = new SolidColorBrush(Color.FromRgb(112, 68, 15));
    }

    private void ApplyTransparentPreset()
    {
        _dashboardSpeedText!.FontSize = 52d;
        if (_presetSpeedPanel is not null)
        {
            _presetSpeedPanel.Background = new SolidColorBrush(Color.FromArgb(76, 2, 16, 25));
            _presetSpeedPanel.BorderBrush = new SolidColorBrush(Color.FromArgb(92, 79, 174, 235));
            _presetSpeedPanel.BorderThickness = new Thickness(1d);
        }
        _busDashboardDock.Background = new SolidColorBrush(Color.FromArgb(105, 4, 20, 31));
        _busDashboardDock.BorderBrush = new SolidColorBrush(Color.FromArgb(118, 87, 181, 239));
        _busDashboardDock.BorderThickness = new Thickness(1d);
        _busDashboardDock.CornerRadius = new CornerRadius(20d);
    }
}
