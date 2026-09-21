using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

internal static class ImmersiveOperationHudBootstrap
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
                window.InitializeImmersiveOperationHud);
        }
    }
}

public partial class HudOverlayWindow
{
    private bool _immersiveOperationInitialized;
    private bool _immersiveOperationActive;
    private Border? _immersiveTopBar;
    private Border? _immersiveMiniMapPanel;
    private Border? _immersiveMultiplayerPanel;
    private TextBlock? _immersiveLineText;
    private TextBlock? _immersiveRouteText;
    private TextBlock? _immersiveDestinationText;
    private TextBlock? _immersiveNextStopText;
    private TextBlock? _immersiveSpeedText;
    private TextBlock? _immersiveDelayText;
    private TextBlock? _immersiveFuelText;
    private TextBlock? _immersiveMapTitleText;
    private TextBlock? _immersiveStreetText;
    private TextBlock? _immersiveSessionText;
    private TextBlock? _immersivePlayersText;
    private TextBlock? _immersiveVoiceText;
    private DispatcherTimer? _immersiveOperationTimer;

    internal void InitializeImmersiveOperationHud()
    {
        if (_immersiveOperationInitialized)
        {
            return;
        }

        _immersiveOperationInitialized = true;
        BuildImmersiveOperationVisuals();

        _immersiveOperationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _immersiveOperationTimer.Tick += ImmersiveOperationTimer_Tick;
        _immersiveOperationTimer.Start();

        MultiplayerSettingsStore.SettingsSaved += ImmersiveOperation_SettingsSaved;
        Closed += ImmersiveOperation_Closed;
        SizeChanged += ImmersiveOperation_SizeChanged;

        ApplyImmersiveOperationMode(MultiplayerSettingsStore.Load());
        RenderImmersiveOperationState();
    }

    private void BuildImmersiveOperationVisuals()
    {
        _immersiveTopBar = BuildImmersiveTopBar();
        _immersiveMiniMapPanel = BuildImmersiveMiniMap();
        _immersiveMultiplayerPanel = BuildImmersiveMultiplayerPanel();

        Panel.SetZIndex(_immersiveTopBar, 1090);
        Panel.SetZIndex(_immersiveMiniMapPanel, 1090);
        Panel.SetZIndex(_immersiveMultiplayerPanel, 1090);

        OverlayRoot.Children.Add(_immersiveTopBar);
        OverlayRoot.Children.Add(_immersiveMiniMapPanel);
        OverlayRoot.Children.Add(_immersiveMultiplayerPanel);
    }

    private Border BuildImmersiveTopBar()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112d) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35d, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104d) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104d) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104d) });

        _immersiveLineText = AddMetricCell(root, 0, "LINHA", "—", true);
        var routeCell = BuildRouteCell();
        Grid.SetColumn(routeCell, 1);
        root.Children.Add(routeCell);
        _immersiveNextStopText = AddMetricCell(root, 2, "PRÓXIMA PARADA", "—");
        _immersiveSpeedText = AddMetricCell(root, 3, "VELOCIDADE", "— km/h");
        _immersiveDelayText = AddMetricCell(root, 4, "ATRASO", "—");
        _immersiveFuelText = AddMetricCell(root, 5, "COMBUSTÍVEL", "—");

        return new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(14d, 12d, 14d, 0d),
            Padding = new Thickness(10d, 8d, 10d, 8d),
            Background = new SolidColorBrush(Color.FromArgb(228, 4, 15, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(155, 54, 121, 166)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = root,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
    }

    private FrameworkElement BuildRouteCell()
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(11d, 1d, 11d, 1d),
            VerticalAlignment = VerticalAlignment.Center
        };
        _immersiveRouteText = new TextBlock
        {
            Text = "ROTA —",
            Foreground = new SolidColorBrush(Color.FromRgb(139, 191, 224)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 10d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _immersiveDestinationText = new TextBlock
        {
            Text = "Destino não informado",
            Margin = new Thickness(0d, 2d, 0d, 0d),
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 16d,
            FontWeight = FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(_immersiveRouteText);
        stack.Children.Add(_immersiveDestinationText);

        return new Border
        {
            Margin = new Thickness(4d, 0d, 4d, 0d),
            Padding = new Thickness(5d, 1d, 5d, 1d),
            BorderBrush = new SolidColorBrush(Color.FromArgb(45, 130, 180, 215)),
            BorderThickness = new Thickness(0d, 0d, 1d, 0d),
            Child = stack
        };
    }

    private static TextBlock AddMetricCell(
        Grid grid,
        int column,
        string label,
        string value,
        bool accent = false)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(9d, 0d, 9d, 0d),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(126, 155, 175)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 8.5d,
            FontWeight = FontWeights.Bold
        });
        var text = new TextBlock
        {
            Text = value,
            Margin = new Thickness(0d, 2d, 0d, 0d),
            Foreground = accent
                ? new SolidColorBrush(Color.FromRgb(88, 180, 255))
                : Brushes.White,
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = accent ? 23d : 15d,
            FontWeight = FontWeights.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(text);

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 125, 166, 194)),
            BorderThickness = column == 0 ? new Thickness(0d, 0d, 1d, 0d) : new Thickness(0d),
            Child = stack
        };
        Grid.SetColumn(border, column);
        grid.Children.Add(border);
        return text;
    }

    private Border BuildImmersiveMiniMap()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new DockPanel { Margin = new Thickness(0d, 0d, 0d, 7d) };
        _immersiveMapTitleText = new TextBlock
        {
            Text = "MAPA",
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 11d,
            FontWeight = FontWeights.Bold
        };
        header.Children.Add(_immersiveMapTitleText);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var mapLayer = new Grid();
        mapLayer.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(8d),
            BorderBrush = new SolidColorBrush(Color.FromArgb(95, 104, 177, 222)),
            BorderThickness = new Thickness(1d),
            Background = new VisualBrush(MiniMapCanvas)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            }
        });
        mapLayer.Children.Add(new TextBlock
        {
            Text = "▲",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 22d,
            FontWeight = FontWeights.Black
        });
        Grid.SetRow(mapLayer, 1);
        root.Children.Add(mapLayer);

        _immersiveStreetText = new TextBlock
        {
            Text = "—",
            Margin = new Thickness(0d, 7d, 0d, 0d),
            Foreground = new SolidColorBrush(Color.FromRgb(150, 177, 195)),
            FontSize = 9.5d,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(_immersiveStreetText, 2);
        root.Children.Add(_immersiveStreetText);

        return new Border
        {
            Width = 342d,
            Height = 226d,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(18d, 0d, 0d, 18d),
            Padding = new Thickness(10d),
            Background = new SolidColorBrush(Color.FromArgb(226, 5, 17, 27)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 60, 130, 176)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = root,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
    }

    private Border BuildImmersiveMultiplayerPanel()
    {
        var stack = new StackPanel();

        var titleRow = new DockPanel();
        titleRow.Children.Add(new TextBlock
        {
            Text = "MULTIPLAYER",
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 11d,
            FontWeight = FontWeights.Bold
        });
        _immersivePlayersText = new TextBlock
        {
            Text = "—",
            Foreground = new SolidColorBrush(Color.FromRgb(89, 226, 151)),
            FontSize = 10d,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(_immersivePlayersText, Dock.Right);
        titleRow.Children.Add(_immersivePlayersText);
        stack.Children.Add(titleRow);

        _immersiveSessionText = new TextBlock
        {
            Text = "NavBR offline",
            Margin = new Thickness(0d, 8d, 0d, 0d),
            Foreground = new SolidColorBrush(Color.FromRgb(179, 202, 216)),
            FontSize = 10d,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(_immersiveSessionText);

        _immersiveVoiceText = new TextBlock
        {
            Text = "PTT • F10",
            Margin = new Thickness(0d, 8d, 0d, 0d),
            Padding = new Thickness(9d, 7d, 9d, 7d),
            Background = new SolidColorBrush(Color.FromArgb(120, 12, 47, 36)),
            Foreground = new SolidColorBrush(Color.FromRgb(111, 234, 168)),
            FontSize = 10d,
            FontWeight = FontWeights.SemiBold
        };
        stack.Children.Add(_immersiveVoiceText);

        stack.Children.Add(new TextBlock
        {
            Text = "F9 CHAT   •   F10 PTT",
            Margin = new Thickness(0d, 8d, 0d, 0d),
            Foreground = new SolidColorBrush(Color.FromRgb(118, 151, 172)),
            FontSize = 9d,
            FontWeight = FontWeights.SemiBold
        });

        return new Border
        {
            Width = 342d,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0d, 0d, 18d, 18d),
            Padding = new Thickness(12d),
            Background = new SolidColorBrush(Color.FromArgb(226, 5, 17, 27)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 60, 130, 176)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = stack,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
    }

    private void ImmersiveOperationTimer_Tick(object? sender, EventArgs e)
    {
        if (!_immersiveOperationActive)
        {
            return;
        }

        if (_busDashboardDock is not null &&
            _busDashboardDock.Visibility != Visibility.Collapsed)
        {
            _busDashboardDock.Visibility = Visibility.Collapsed;
        }

        RenderImmersiveOperationState();
    }

    private void ImmersiveOperation_SettingsSaved(MultiplayerSettings settings)
    {
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            () => ApplyImmersiveOperationMode(settings));
    }

    private void ImmersiveOperation_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_immersiveOperationActive)
        {
            ApplyImmersiveOperationSizing();
        }
    }

    private void ApplyImmersiveOperationMode(MultiplayerSettings settings)
    {
        var active = settings.DashboardEnabled &&
                     string.Equals(
                         HudProfileCatalog.ResolvePreset(settings.DashboardPreset).Id,
                         "immersive-operation",
                         StringComparison.OrdinalIgnoreCase);

        _immersiveOperationActive = active;
        if (_immersiveTopBar is null ||
            _immersiveMiniMapPanel is null ||
            _immersiveMultiplayerPanel is null)
        {
            return;
        }

        var visibility = active ? Visibility.Visible : Visibility.Collapsed;
        _immersiveTopBar.Visibility = visibility;
        _immersiveMiniMapPanel.Visibility = visibility;
        _immersiveMultiplayerPanel.Visibility = visibility;

        TopStatusPanel.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        TripInfoPanel.Visibility = active ? Visibility.Collapsed : Visibility.Visible;

        // Keep the real minimap alive as the source for the immersive VisualBrush.
        // It is made transparent instead of collapsed so route/stop rendering keeps running.
        MiniMapHudPanel.Opacity = active ? 0d : 1d;
        MiniMapHudPanel.IsHitTestVisible = !active;

        if (_busDashboardDock is not null)
        {
            if (active)
            {
                _busDashboardDock.Visibility = Visibility.Collapsed;
            }
            else
            {
                ApplyDashboardSettings();
            }
        }

        ApplyImmersiveOperationSizing();
        if (active)
        {
            RenderImmersiveOperationState();
        }
    }

    private void ApplyImmersiveOperationSizing()
    {
        if (!_immersiveOperationActive ||
            _immersiveTopBar is null ||
            _immersiveMiniMapPanel is null ||
            _immersiveMultiplayerPanel is null)
        {
            return;
        }

        var compact = ActualWidth > 1d && ActualWidth < 1280d;
        _immersiveTopBar.Margin = compact
            ? new Thickness(9d, 8d, 9d, 0d)
            : new Thickness(14d, 12d, 14d, 0d);

        var panelWidth = compact ? 292d : 342d;
        _immersiveMiniMapPanel.Width = panelWidth;
        _immersiveMiniMapPanel.Height = compact ? 196d : 226d;
        _immersiveMultiplayerPanel.Width = panelWidth;
        _immersiveMiniMapPanel.Margin = compact
            ? new Thickness(10d, 0d, 0d, 10d)
            : new Thickness(18d, 0d, 0d, 18d);
        _immersiveMultiplayerPanel.Margin = compact
            ? new Thickness(0d, 0d, 10d, 10d)
            : new Thickness(0d, 0d, 18d, 18d);
    }

    private void RenderImmersiveOperationState()
    {
        if (!_immersiveOperationActive)
        {
            return;
        }

        var telemetry = _localTelemetry;
        if (_immersiveLineText is not null)
        {
            _immersiveLineText.Text = string.IsNullOrWhiteSpace(telemetry?.Line)
                ? "—"
                : telemetry.Line;
        }
        if (_immersiveRouteText is not null)
        {
            _immersiveRouteText.Text = string.IsNullOrWhiteSpace(telemetry?.Route)
                ? "ROTA —"
                : $"ROTA {telemetry.Route}";
        }
        if (_immersiveDestinationText is not null)
        {
            _immersiveDestinationText.Text = !string.IsNullOrWhiteSpace(telemetry?.DestinationName)
                ? telemetry.DestinationName
                : "Destino não informado";
        }
        if (_immersiveNextStopText is not null)
        {
            _immersiveNextStopText.Text = string.IsNullOrWhiteSpace(telemetry?.NextStopName)
                ? "—"
                : telemetry.NextStopName;
        }
        if (_immersiveSpeedText is not null)
        {
            _immersiveSpeedText.Text = telemetry is null
                ? "— km/h"
                : $"{Math.Clamp(telemetry.SpeedKph, 0d, 999d):F0} km/h";
        }
        if (_immersiveDelayText is not null)
        {
            _immersiveDelayText.Text = FormatImmersiveDelay(telemetry?.DelaySeconds);
        }
        if (_immersiveFuelText is not null)
        {
            _immersiveFuelText.Text = telemetry?.FuelPercent is double fuel && double.IsFinite(fuel)
                ? $"{Math.Clamp(fuel, 0d, 100d):F0}%"
                : "—";
        }
        if (_immersiveMapTitleText is not null)
        {
            _immersiveMapTitleText.Text = string.IsNullOrWhiteSpace(telemetry?.MapName)
                ? "MAPA"
                : $"MAPA  •  {telemetry.MapName}";
        }
        if (_immersiveStreetText is not null)
        {
            _immersiveStreetText.Text = !string.IsNullOrWhiteSpace(telemetry?.CurrentStreetName)
                ? telemetry.CurrentStreetName
                : MiniMapStatusText.Text;
        }
        if (_immersiveSessionText is not null)
        {
            _immersiveSessionText.Text = HudStatusText.Text;
        }
        if (_immersivePlayersText is not null)
        {
            _immersivePlayersText.Text = $"{PlayerCountText.Text} online";
        }
        if (_immersiveVoiceText is not null)
        {
            _immersiveVoiceText.Text = BuildImmersiveVoiceStatus();
        }
    }

    private string BuildImmersiveVoiceStatus()
    {
        if (_localPushToTalk)
        {
            return $"{_localDisplayName} • PTT ativo";
        }

        var activeSpeakers = _speakers.Values
            .Select(value => value.DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Take(2)
            .ToArray();

        return activeSpeakers.Length > 0
            ? $"{string.Join(", ", activeSpeakers)} falando"
            : "PTT • F10";
    }

    private static string FormatImmersiveDelay(int? seconds)
    {
        if (seconds is not int value)
        {
            return "—";
        }

        if (Math.Abs(value) < 60)
        {
            return value == 0 ? "0 min" : $"{value:+0;-0;0} s";
        }

        var minutes = (int)Math.Round(value / 60d);
        return $"{minutes:+0;-0;0} min";
    }

    private void ImmersiveOperation_Closed(object? sender, EventArgs e)
    {
        MultiplayerSettingsStore.SettingsSaved -= ImmersiveOperation_SettingsSaved;
        SizeChanged -= ImmersiveOperation_SizeChanged;

        if (_immersiveOperationTimer is not null)
        {
            _immersiveOperationTimer.Stop();
            _immersiveOperationTimer.Tick -= ImmersiveOperationTimer_Tick;
            _immersiveOperationTimer = null;
        }

        Closed -= ImmersiveOperation_Closed;
    }
}
