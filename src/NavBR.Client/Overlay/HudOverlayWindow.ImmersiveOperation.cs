using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Multiplayer;
using NavBR.Client.Localization;

namespace NavBR.Client.Overlay;

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
    private TextBlock? _immersiveNearbyPlayersText;
    private TextBlock? _immersiveChatText;
    private TextBlock? _immersiveVoiceText;
    private DispatcherTimer? _immersiveOperationTimer;
    private bool _immersivePresentationApplied;
    private Visibility _immersiveSavedTopStatusVisibility = Visibility.Visible;
    private Visibility _immersiveSavedTripInfoVisibility = Visibility.Visible;
    private Visibility _immersiveSavedMiniMapVisibility = Visibility.Visible;
    private double _immersiveSavedMiniMapOpacity = 1d;
    private bool _immersiveSavedMiniMapHitTest = true;

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

        _immersiveLineText = AddMetricCell(root, 0, ImmersiveText("LINHA", "LINE", "LÍNEA", "LINIE", "LIGNE"), "—", true);
        var routeCell = BuildRouteCell();
        Grid.SetColumn(routeCell, 1);
        root.Children.Add(routeCell);
        _immersiveNextStopText = AddMetricCell(root, 2, ImmersiveText("PRÓXIMA PARADA", "NEXT STOP", "PRÓXIMA PARADA", "NÄCHSTER HALT", "PROCHAIN ARRÊT"), "—");
        _immersiveSpeedText = AddMetricCell(root, 3, ImmersiveText("VELOCIDADE", "SPEED", "VELOCIDAD", "GESCHWINDIGKEIT", "VITESSE"), "— km/h");
        _immersiveDelayText = AddMetricCell(root, 4, ImmersiveText("ATRASO", "DELAY", "RETRASO", "VERSPÄTUNG", "RETARD"), "—");
        _immersiveFuelText = AddMetricCell(root, 5, ImmersiveText("COMBUSTÍVEL", "FUEL", "COMBUSTIBLE", "KRAFTSTOFF", "CARBURANT"), "—");

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
            Text = ImmersiveText("ROTA —", "ROUTE —", "RUTA —", "ROUTE —", "ITINÉRAIRE —"),
            Foreground = new SolidColorBrush(Color.FromRgb(139, 191, 224)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 10d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _immersiveDestinationText = new TextBlock
        {
            Text = ImmersiveText("Destino não informado", "Destination unavailable", "Destino no disponible", "Ziel nicht verfügbar", "Destination indisponible"),
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
            Text = ImmersiveText("MAPA", "MAP", "MAPA", "KARTE", "CARTE"),
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

        var titleRow = new DockPanel { LastChildFill = false };
        titleRow.Children.Add(new TextBlock
        {
            Text = ImmersiveText("MULTIPLAYER", "MULTIPLAYER", "MULTIJUGADOR", "MULTIPLAYER", "MULTIJOUEUR"),
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
            Text = ImmersiveText("NavBR offline", "NavBR offline", "NavBR sin conexión", "NavBR offline", "NavBR hors ligne"),
            Margin = new Thickness(0d, 8d, 0d, 0d),
            Foreground = new SolidColorBrush(Color.FromRgb(179, 202, 216)),
            FontSize = 10d,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(_immersiveSessionText);

        _immersiveNearbyPlayersText = new TextBlock
        {
            Text = ImmersiveText(
                "Nenhum jogador próximo",
                "No nearby players",
                "Ningún jugador cercano",
                "Keine Spieler in der Nähe",
                "Aucun joueur à proximité"),
            Margin = new Thickness(0d, 9d, 0d, 0d),
            Padding = new Thickness(9d, 8d, 9d, 8d),
            Background = new SolidColorBrush(Color.FromArgb(92, 17, 36, 49)),
            Foreground = new SolidColorBrush(Color.FromRgb(216, 231, 239)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 9.5d,
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(_immersiveNearbyPlayersText);

        _immersiveChatText = new TextBlock
        {
            Text = ImmersiveText(
                "CHAT • sem mensagens recentes",
                "CHAT • no recent messages",
                "CHAT • sin mensajes recientes",
                "CHAT • keine neuen Nachrichten",
                "CHAT • aucun message récent"),
            Margin = new Thickness(0d, 8d, 0d, 0d),
            Foreground = new SolidColorBrush(Color.FromRgb(157, 184, 201)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 9d,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 48d
        };
        stack.Children.Add(_immersiveChatText);

        _immersiveVoiceText = new TextBlock
        {
            Text = "PTT • F10",
            Foreground = new SolidColorBrush(Color.FromRgb(111, 234, 168)),
            FontSize = 10d,
            FontWeight = FontWeights.SemiBold
        };
        stack.Children.Add(new Border
        {
            Margin = new Thickness(0d, 8d, 0d, 0d),
            Padding = new Thickness(9d, 7d, 9d, 7d),
            Background = new SolidColorBrush(Color.FromArgb(120, 12, 47, 36)),
            CornerRadius = new CornerRadius(7d),
            Child = _immersiveVoiceText
        });

        stack.Children.Add(new TextBlock
        {
            Text = ImmersiveText("F9 CHAT   •   F10 PTT", "F9 CHAT   •   F10 PTT", "F9 CHAT   •   F10 PTT", "F9 CHAT   •   F10 PTT", "F9 CHAT   •   F10 PTT"),
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
        _hudSettings = settings;
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

        _immersiveTopBar.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        _immersiveMiniMapPanel.Visibility = active && settings.DashboardShowMinimap
            ? Visibility.Visible
            : Visibility.Collapsed;
        _immersiveMultiplayerPanel.Visibility = active && settings.DashboardShowMultiplayer
            ? Visibility.Visible
            : Visibility.Collapsed;

        var opacity = Math.Clamp(settings.DashboardOpacity, 0.35d, 1d);
        _immersiveTopBar.Opacity = opacity;
        _immersiveMiniMapPanel.Opacity = opacity;
        _immersiveMultiplayerPanel.Opacity = opacity;

        _immersiveMiniMapPanel.LayoutTransform = new ScaleTransform(
            Math.Clamp(settings.DashboardMinimapScale, 0.55d, 2d),
            Math.Clamp(settings.DashboardMinimapScale, 0.55d, 2d));
        _immersiveMultiplayerPanel.LayoutTransform = new ScaleTransform(
            Math.Clamp(settings.DashboardMultiplayerScale, 0.55d, 2d),
            Math.Clamp(settings.DashboardMultiplayerScale, 0.55d, 2d));

        if (active && !_immersivePresentationApplied)
        {
            _immersiveSavedTopStatusVisibility = TopStatusPanel.Visibility;
            _immersiveSavedTripInfoVisibility = TripInfoPanel.Visibility;
            _immersiveSavedMiniMapVisibility = MiniMapHudPanel.Visibility;
            _immersiveSavedMiniMapOpacity = MiniMapHudPanel.Opacity;
            _immersiveSavedMiniMapHitTest = MiniMapHudPanel.IsHitTestVisible;
            _immersivePresentationApplied = true;
        }

        if (active)
        {
            TopStatusPanel.Visibility = Visibility.Collapsed;
            TripInfoPanel.Visibility = Visibility.Collapsed;

            // Keep the real minimap alive as the source for the immersive VisualBrush.
            // A tiny opacity keeps WPF rendering the source while making the original panel effectively invisible.
            MiniMapHudPanel.Visibility = Visibility.Visible;
            MiniMapHudPanel.Opacity = 0.01d;
            MiniMapHudPanel.IsHitTestVisible = false;

            if (_busDashboardDock is not null)
            {
                _busDashboardDock.Visibility = Visibility.Collapsed;
            }
        }
        else if (_immersivePresentationApplied)
        {
            TopStatusPanel.Visibility = _immersiveSavedTopStatusVisibility;
            TripInfoPanel.Visibility = _immersiveSavedTripInfoVisibility;
            MiniMapHudPanel.Visibility = _immersiveSavedMiniMapVisibility;
            MiniMapHudPanel.Opacity = _immersiveSavedMiniMapOpacity;
            MiniMapHudPanel.IsHitTestVisible = _immersiveSavedMiniMapHitTest;
            _immersivePresentationApplied = false;
            ApplyDashboardSettings();
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
            var routeLabel = ImmersiveText("ROTA", "ROUTE", "RUTA", "ROUTE", "ITINÉRAIRE");
            _immersiveRouteText.Text = string.IsNullOrWhiteSpace(telemetry?.Route)
                ? $"{routeLabel} —"
                : $"{routeLabel} {telemetry.Route}";
        }
        if (_immersiveDestinationText is not null)
        {
            _immersiveDestinationText.Text = !string.IsNullOrWhiteSpace(telemetry?.DestinationName)
                ? telemetry.DestinationName
                : ImmersiveText(
                    "Destino não informado",
                    "Destination unavailable",
                    "Destino no disponible",
                    "Ziel nicht verfügbar",
                    "Destination indisponible");
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
            var mapLabel = ImmersiveText("MAPA", "MAP", "MAPA", "KARTE", "CARTE");
            _immersiveMapTitleText.Text = string.IsNullOrWhiteSpace(telemetry?.MapName)
                ? mapLabel
                : $"{mapLabel}  •  {telemetry.MapName}";
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
            _immersivePlayersText.Text = $"{PlayerCountText.Text} {ImmersiveText("online", "online", "en línea", "online", "en ligne")}";
        }
        if (_immersiveNearbyPlayersText is not null)
        {
            _immersiveNearbyPlayersText.Text = BuildImmersiveNearbyPlayersText();
        }
        if (_immersiveChatText is not null)
        {
            _immersiveChatText.Text = BuildImmersiveChatText();
        }
        if (_immersiveVoiceText is not null)
        {
            _immersiveVoiceText.Text = BuildImmersiveVoiceStatus();
        }
    }

    private string BuildImmersiveNearbyPlayersText()
    {
        var local = _localTelemetry;
        if (local is null || _remotePlayers.Count == 0)
        {
            return ImmersiveText(
                "Nenhum jogador próximo",
                "No nearby players",
                "Ningún jugador cercano",
                "Keine Spieler in der Nähe",
                "Aucun joueur à proximité");
        }

        var nearby = _remotePlayers.Values
            .Where(frame => IsSameImmersiveMap(local, frame.Telemetry))
            .Select(frame => new
            {
                Frame = frame,
                Distance = ImmersiveDistanceMeters(local, frame.Telemetry)
            })
            .Where(item => double.IsFinite(item.Distance))
            .OrderBy(item => item.Distance)
            .Take(3)
            .ToArray();

        if (nearby.Length == 0)
        {
            return ImmersiveText(
                "Nenhum jogador neste mapa",
                "No players on this map",
                "Ningún jugador en este mapa",
                "Keine Spieler auf dieser Karte",
                "Aucun joueur sur cette carte");
        }

        return string.Join(
            Environment.NewLine,
            nearby.Select(item =>
            {
                var name = string.IsNullOrWhiteSpace(item.Frame.Player.DisplayName)
                    ? item.Frame.Player.PlayerId
                    : item.Frame.Player.DisplayName.Trim();
                var speed = $"{Math.Clamp(item.Frame.Telemetry.SpeedKph, 0d, 999d):F0} km/h";
                var distance = FormatImmersiveDistance(item.Distance);
                var latency = item.Frame.Player.LatencyMs is int latencyMs && latencyMs >= 0
                    ? $" • {latencyMs} ms"
                    : string.Empty;
                return $"{name}  •  {speed}  •  {distance}{latency}";
            }));
    }

    private static bool IsSameImmersiveMap(VehicleTelemetry local, VehicleTelemetry remote)
    {
        if (!string.IsNullOrWhiteSpace(local.MapCompatibilityId) &&
            !string.IsNullOrWhiteSpace(remote.MapCompatibilityId))
        {
            return string.Equals(
                local.MapCompatibilityId,
                remote.MapCompatibilityId,
                StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(local.MapName) &&
               !string.IsNullOrWhiteSpace(remote.MapName) &&
               string.Equals(local.MapName, remote.MapName, StringComparison.OrdinalIgnoreCase);
    }

    private static double ImmersiveDistanceMeters(VehicleTelemetry local, VehicleTelemetry remote)
    {
        var dx = remote.X - local.X;
        var dy = remote.Y - local.Y;
        var dz = remote.Z - local.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static string FormatImmersiveDistance(double meters)
    {
        if (!double.IsFinite(meters))
        {
            return "—";
        }

        return meters < 1000d
            ? $"{Math.Max(0d, meters):F0} m"
            : $"{Math.Max(0d, meters) / 1000d:F1} km";
    }

    private string BuildImmersiveChatText()
    {
        if (_chatMessages.Count == 0)
        {
            return ImmersiveText(
                "CHAT • sem mensagens recentes",
                "CHAT • no recent messages",
                "CHAT • sin mensajes recientes",
                "CHAT • keine neuen Nachrichten",
                "CHAT • aucun message récent");
        }

        var recent = _chatMessages
            .TakeLast(2)
            .Select(message =>
            {
                var name = string.IsNullOrWhiteSpace(message.DisplayName)
                    ? "Driver"
                    : message.DisplayName.Trim();
                var text = string.IsNullOrWhiteSpace(message.Text)
                    ? "…"
                    : message.Text.Trim();
                return $"{name}: {text}";
            });

        return "CHAT" + Environment.NewLine + string.Join(Environment.NewLine, recent);
    }

    private string BuildImmersiveVoiceStatus()
    {
        if (_localPushToTalk)
        {
            return $"{_localDisplayName} • {ImmersiveText("PTT ativo", "PTT active", "PTT activo", "PTT aktiv", "PTT actif")}";
        }

        var activeSpeakers = _speakers.Values
            .Select(value => value.DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Take(2)
            .ToArray();

        return activeSpeakers.Length > 0
            ? $"{string.Join(", ", activeSpeakers)} {ImmersiveText("falando", "speaking", "hablando", "spricht", "parle")}"
            : "PTT • F10";
    }

    private static string ImmersiveText(
        string pt,
        string en,
        string es,
        string de,
        string fr)
    {
        return LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
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
