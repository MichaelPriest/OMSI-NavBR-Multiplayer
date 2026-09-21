using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Multiplayer;
using NavBR.Client.Localization;
using NavBR.Shared.Telemetry;

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
    private TextBlock? _immersiveVehicleStatusText;
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
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
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

        _immersiveVehicleStatusText = new TextBlock
        {
            Text = ImmersiveText(
                "Operação normal",
                "Normal operation",
                "Operación normal",
                "Normalbetrieb",
                "Exploitation normale"),
            Margin = new Thickness(9d, 7d, 9d, 0d),
            Foreground = new SolidColorBrush(Color.FromRgb(115, 222, 166)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 9d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(_immersiveVehicleStatusText, 1);
        Grid.SetColumnSpan(_immersiveVehicleStatusText, 6);
        root.Children.Add(_immersiveVehicleStatusText);

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
        var preset = HudProfileCatalog.ResolvePreset(settings.DashboardPreset);
        var active = settings.DashboardEnabled && HudProfileCatalog.IsComposedPreset(preset.Id);

        _immersiveOperationActive = active;
        if (_immersiveTopBar is null ||
            _immersiveMiniMapPanel is null ||
            _immersiveMultiplayerPanel is null)
        {
            return;
        }

        _immersiveTopBar.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        if (_immersiveVehicleStatusText is not null)
        {
            _immersiveVehicleStatusText.Visibility = active && settings.DashboardShowStatus
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
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
        var presetId = HudProfileCatalog.ResolvePreset(_hudSettings.DashboardPreset).Id;

        _immersiveTopBar.HorizontalAlignment = HorizontalAlignment.Stretch;
        _immersiveTopBar.Width = double.NaN;
        _immersiveTopBar.Margin = compact
            ? new Thickness(9d, 8d, 9d, 0d)
            : new Thickness(14d, 12d, 14d, 0d);

        _immersiveMiniMapPanel.HorizontalAlignment = HorizontalAlignment.Left;
        _immersiveMiniMapPanel.VerticalAlignment = VerticalAlignment.Bottom;
        _immersiveMultiplayerPanel.HorizontalAlignment = HorizontalAlignment.Right;
        _immersiveMultiplayerPanel.VerticalAlignment = VerticalAlignment.Bottom;

        var mapWidth = compact ? 292d : 342d;
        var mapHeight = compact ? 196d : 226d;
        var multiplayerWidth = compact ? 292d : 342d;
        var edge = compact ? 10d : 18d;

        if (_immersiveLineText is not null) _immersiveLineText.FontSize = 23d;
        if (_immersiveRouteText is not null) _immersiveRouteText.FontSize = 10d;
        if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 16d;
        if (_immersiveNextStopText is not null) _immersiveNextStopText.FontSize = 15d;
        if (_immersiveSpeedText is not null) _immersiveSpeedText.FontSize = 15d;

        switch (presetId)
        {
            case "transit-control":
                mapWidth = compact ? 320d : 390d;
                mapHeight = compact ? 210d : 258d;
                multiplayerWidth = compact ? 320d : 370d;
                if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 17d;
                if (_immersiveNextStopText is not null) _immersiveNextStopText.FontSize = 16d;
                break;

            case "cockpit-digital":
                _immersiveTopBar.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveTopBar.Width = compact ? 710d : 900d;
                _immersiveTopBar.Margin = compact
                    ? new Thickness(0d, 9d, 0d, 0d)
                    : new Thickness(0d, 14d, 0d, 0d);
                mapWidth = compact ? 230d : 275d;
                mapHeight = compact ? 160d : 188d;
                multiplayerWidth = compact ? 260d : 300d;
                if (_immersiveLineText is not null) _immersiveLineText.FontSize = 28d;
                if (_immersiveSpeedText is not null) _immersiveSpeedText.FontSize = 28d;
                if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 18d;
                break;

            case "navigation-pro":
                _immersiveTopBar.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveTopBar.Width = compact ? 760d : 980d;
                mapWidth = compact ? 390d : 500d;
                mapHeight = compact ? 250d : 320d;
                multiplayerWidth = compact ? 280d : 320d;
                if (_immersiveNextStopText is not null) _immersiveNextStopText.FontSize = 17d;
                break;

            case "multiplayer-focus":
                mapWidth = compact ? 270d : 315d;
                mapHeight = compact ? 185d : 210d;
                multiplayerWidth = compact ? 390d : 455d;
                if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 15d;
                break;

            case "classic-omsi-plus":
                _immersiveTopBar.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveTopBar.Width = compact ? 650d : 790d;
                _immersiveTopBar.Margin = compact
                    ? new Thickness(0d, 8d, 0d, 0d)
                    : new Thickness(0d, 12d, 0d, 0d);
                mapWidth = compact ? 250d : 290d;
                mapHeight = compact ? 165d : 195d;
                multiplayerWidth = compact ? 280d : 320d;
                if (_immersiveLineText is not null) _immersiveLineText.FontSize = 25d;
                if (_immersiveSpeedText is not null) _immersiveSpeedText.FontSize = 18d;
                if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 15d;
                break;
        }

        _immersiveMiniMapPanel.Width = mapWidth;
        _immersiveMiniMapPanel.Height = mapHeight;
        _immersiveMultiplayerPanel.Width = multiplayerWidth;
        _immersiveMiniMapPanel.Margin = new Thickness(edge, 0d, 0d, edge);
        _immersiveMultiplayerPanel.Margin = new Thickness(0d, 0d, edge, edge);

        ApplyComposedPresetPalette(presetId);
    }

    private void ApplyComposedPresetPalette(string presetId)
    {
        if (_immersiveTopBar is null ||
            _immersiveMiniMapPanel is null ||
            _immersiveMultiplayerPanel is null)
        {
            return;
        }

        var palette = presetId switch
        {
            "transit-control" => new ComposedHudPalette(
                Color.FromRgb(4, 18, 27), Color.FromRgb(40, 126, 161), Color.FromRgb(54, 211, 152), Color.FromRgb(238, 248, 251), 10d),
            "cockpit-digital" => new ComposedHudPalette(
                Color.FromRgb(4, 13, 20), Color.FromRgb(38, 116, 153), Color.FromRgb(52, 199, 255), Color.FromRgb(239, 249, 253), 16d),
            "navigation-pro" => new ComposedHudPalette(
                Color.FromRgb(6, 14, 24), Color.FromRgb(51, 102, 148), Color.FromRgb(255, 166, 59), Color.FromRgb(241, 247, 251), 12d),
            "multiplayer-focus" => new ComposedHudPalette(
                Color.FromRgb(9, 12, 25), Color.FromRgb(91, 77, 158), Color.FromRgb(130, 193, 255), Color.FromRgb(244, 242, 255), 12d),
            "classic-omsi-plus" => new ComposedHudPalette(
                Color.FromRgb(17, 10, 3), Color.FromRgb(126, 80, 22), Color.FromRgb(255, 177, 49), Color.FromRgb(255, 209, 126), 4d),
            _ => new ComposedHudPalette(
                Color.FromRgb(4, 15, 24), Color.FromRgb(54, 121, 166), Color.FromRgb(58, 169, 255), Color.FromRgb(235, 244, 250), 12d)
        };

        var opacity = (byte)Math.Clamp((int)Math.Round(Math.Clamp(_hudSettings.DashboardOpacity, 0.35d, 1d) * 255d), 0, 255);
        var background = new SolidColorBrush(Color.FromArgb(opacity, palette.Background.R, palette.Background.G, palette.Background.B));
        var border = new SolidColorBrush(palette.Border);
        var accent = new SolidColorBrush(palette.Accent);
        var text = new SolidColorBrush(palette.Text);

        foreach (var panel in new[] { _immersiveTopBar, _immersiveMiniMapPanel, _immersiveMultiplayerPanel })
        {
            panel.Background = background;
            panel.BorderBrush = border;
            panel.CornerRadius = new CornerRadius(palette.CornerRadius);
        }

        if (_immersiveLineText is not null) _immersiveLineText.Foreground = accent;
        if (_immersiveRouteText is not null) _immersiveRouteText.Foreground = new SolidColorBrush(Color.FromArgb(220, palette.Accent.R, palette.Accent.G, palette.Accent.B));
        if (_immersiveDestinationText is not null) _immersiveDestinationText.Foreground = text;
        if (_immersiveNextStopText is not null) _immersiveNextStopText.Foreground = text;
        if (_immersiveSpeedText is not null) _immersiveSpeedText.Foreground = accent;
        if (_immersiveDelayText is not null) _immersiveDelayText.Foreground = text;
        if (_immersiveFuelText is not null) _immersiveFuelText.Foreground = text;
        if (_immersiveMapTitleText is not null) _immersiveMapTitleText.Foreground = accent;
        if (_immersivePlayersText is not null) _immersivePlayersText.Foreground = accent;

        var font = presetId == "classic-omsi-plus"
            ? new FontFamily("Consolas")
            : new FontFamily("Bahnschrift");

        foreach (var label in new[]
        {
            _immersiveLineText, _immersiveRouteText, _immersiveDestinationText,
            _immersiveNextStopText, _immersiveSpeedText, _immersiveDelayText,
            _immersiveFuelText, _immersiveMapTitleText, _immersiveStreetText,
            _immersiveSessionText, _immersivePlayersText, _immersiveNearbyPlayersText,
            _immersiveChatText, _immersiveVoiceText
        })
        {
            if (label is not null)
            {
                label.FontFamily = font;
            }
        }
    }

    private readonly record struct ComposedHudPalette(
        Color Background,
        Color Border,
        Color Accent,
        Color Text,
        double CornerRadius);

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
        if (_immersiveVehicleStatusText is not null)
        {
            var status = BuildImmersiveVehicleStatus(telemetry);
            _immersiveVehicleStatusText.Text = status.Text;
            _immersiveVehicleStatusText.Foreground = new SolidColorBrush(status.Color);
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

    private static (string Text, Color Color) BuildImmersiveVehicleStatus(VehicleTelemetry? telemetry)
    {
        if (telemetry is null)
        {
            return (
                ImmersiveText(
                    "Aguardando telemetria",
                    "Waiting for telemetry",
                    "Esperando telemetría",
                    "Warte auf Telemetrie",
                    "En attente de télémétrie"),
                Color.FromRgb(150, 177, 195));
        }

        var states = new List<string>();
        var warning = false;

        if (telemetry.Doors != VehicleDoorFlags.None)
        {
            states.Add(ImmersiveText("PORTAS ABERTAS", "DOORS OPEN", "PUERTAS ABIERTAS", "TÜREN OFFEN", "PORTES OUVERTES"));
            warning = true;
        }
        if (telemetry.StopRequested)
        {
            states.Add(ImmersiveText("PARADA SOLICITADA", "STOP REQUESTED", "PARADA SOLICITADA", "HALTEWUNSCH", "ARRÊT DEMANDÉ"));
        }
        if (telemetry.ParkingBrakeActive)
        {
            states.Add(ImmersiveText("FREIO P", "PARK BRAKE", "FRENO P", "FESTSTELLBREMSE", "FREIN DE PARC"));
        }
        if (telemetry.ReverseGear)
        {
            states.Add(ImmersiveText("RÉ", "REVERSE", "REVERSA", "RÜCKWÄRTS", "MARCHE ARRIÈRE"));
            warning = true;
        }

        switch (telemetry.TurnSignal)
        {
            case TurnSignalState.Left:
                states.Add("← " + ImmersiveText("SETA", "TURN", "GIRO", "BLINKER", "CLIGNOTANT"));
                break;
            case TurnSignalState.Right:
                states.Add(ImmersiveText("SETA", "TURN", "GIRO", "BLINKER", "CLIGNOTANT") + " →");
                break;
            case TurnSignalState.Hazard:
                states.Add(ImmersiveText("PISCA-ALERTA", "HAZARDS", "EMERGENCIA", "WARNBLINKER", "FEUX DE DÉTRESSE"));
                warning = true;
                break;
        }

        if (telemetry.Lights.HasFlag(VehicleLightFlags.LowBeam) ||
            telemetry.Lights.HasFlag(VehicleLightFlags.HighBeam))
        {
            states.Add(ImmersiveText("FARÓIS", "LIGHTS", "LUCES", "LICHT", "FEUX"));
        }
        if (telemetry.WipersActive)
        {
            states.Add(ImmersiveText("LIMPADOR", "WIPERS", "LIMPIAPARABRISAS", "WISCHER", "ESSUIE-GLACES"));
        }

        if (states.Count == 0)
        {
            return (
                ImmersiveText(
                    "Operação normal",
                    "Normal operation",
                    "Operación normal",
                    "Normalbetrieb",
                    "Exploitation normale"),
                Color.FromRgb(115, 222, 166));
        }

        return (
            string.Join("   •   ", states),
            warning ? Color.FromRgb(255, 184, 87) : Color.FromRgb(132, 205, 235));
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
