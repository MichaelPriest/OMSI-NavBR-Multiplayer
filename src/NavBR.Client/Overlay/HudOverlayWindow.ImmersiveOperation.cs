using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Multiplayer;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private bool _immersiveOperationInitialized;
    private bool _immersiveOperationActive;
    private Border? _immersiveTopBar;
    private Border? _immersiveMiniMapPanel;
    private Border? _immersiveMultiplayerPanel;
    private Border? _immersiveFocusPanel;
    private Border? _immersiveFocusMoveHandle;
    private bool _immersiveFocusDragging;
    private Point _immersiveFocusDragStartMouse;
    private Point _immersiveFocusDragStartPosition;
    private Border? _immersiveAlertPanel;
    private TextBlock? _immersiveAlertText;
    private Border? _immersiveSideIndicatorPanel;
    private TextBlock? _immersiveSideIndicatorText;
    private TextBlock? _immersiveFocusEyebrowText;
    private TextBlock? _immersiveFocusPrimaryText;
    private TextBlock? _immersiveFocusSecondaryText;
    private TextBlock? _immersiveFocusStopsText;
    private string? _immersiveOrderedStopsKey;
    private OmsiOrderedRouteStops? _immersiveOrderedStops;
    private readonly NavBRNavigationEtaEstimator _immersiveEtaEstimator = new();
    private NavBRNavigationEtaEstimate _immersiveEtaEstimate = NavBRNavigationEtaEstimate.Unavailable;
    private DateTimeOffset _immersiveEtaObservedAtUtc = DateTimeOffset.MinValue;
    private TextBlock? _immersiveLineText;
    private TextBlock? _immersiveRouteText;
    private TextBlock? _immersiveDestinationText;
    private TextBlock? _immersiveNextStopText;
    private TextBlock? _immersiveSpeedText;
    private TextBlock? _immersiveDelayText;
    private TextBlock? _immersiveFuelText;
    private Border? _immersiveFuelCell;
    private Border? _immersiveDelayCell;
    private Border? _immersiveSpeedCell;
    private Border? _immersiveNextStopCell;
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
        LayoutEditModeChanged += ImmersiveOperation_LayoutEditModeChanged;
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
        _immersiveFocusPanel = BuildImmersiveFocusPanel();
        _immersiveAlertPanel = BuildImmersiveAlertPanel();
        _immersiveSideIndicatorPanel = BuildImmersiveSideIndicatorPanel();

        Panel.SetZIndex(_immersiveTopBar, 1090);
        Panel.SetZIndex(_immersiveMiniMapPanel, 1090);
        Panel.SetZIndex(_immersiveMultiplayerPanel, 1090);
        Panel.SetZIndex(_immersiveFocusPanel, 1095);
        Panel.SetZIndex(_immersiveAlertPanel, 1100);
        Panel.SetZIndex(_immersiveSideIndicatorPanel, 1100);

        OverlayRoot.Children.Add(_immersiveTopBar);
        OverlayRoot.Children.Add(_immersiveMiniMapPanel);
        OverlayRoot.Children.Add(_immersiveMultiplayerPanel);
        OverlayRoot.Children.Add(_immersiveFocusPanel);
        OverlayRoot.Children.Add(_immersiveAlertPanel);
        OverlayRoot.Children.Add(_immersiveSideIndicatorPanel);
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
        _immersiveNextStopText = AddMetricCell(root, 2, ImmersiveText("PRÓXIMA PARADA", "NEXT STOP", "PRÓXIMA PARADA", "NÄCHSTER HALT", "PROCHAIN ARRÊT"), "—", false, out _immersiveNextStopCell);
        _immersiveSpeedText = AddMetricCell(root, 3, ImmersiveText("VELOCIDADE", "SPEED", "VELOCIDAD", "GESCHWINDIGKEIT", "VITESSE"), "— km/h", false, out _immersiveSpeedCell);
        _immersiveDelayText = AddMetricCell(root, 4, ImmersiveText("ATRASO", "DELAY", "RETRASO", "VERSPÄTUNG", "RETARD"), "—", false, out _immersiveDelayCell);
        _immersiveFuelText = AddMetricCell(root, 5, ImmersiveText("COMBUSTÍVEL", "FUEL", "COMBUSTIBLE", "KRAFTSTOFF", "CARBURANT"), "—", false, out _immersiveFuelCell);

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
        return AddMetricCell(grid, column, label, value, accent, out _);
    }

    private static TextBlock AddMetricCell(
        Grid grid,
        int column,
        string label,
        string value,
        bool accent,
        out Border cell)
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

        cell = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(42, 125, 166, 194)),
            BorderThickness = column == 0 ? new Thickness(0d, 0d, 1d, 0d) : new Thickness(0d),
            Child = stack
        };
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
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

    private Border BuildImmersiveFocusPanel()
    {
        var stack = new StackPanel();

        _immersiveFocusMoveHandle = new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            Padding = new Thickness(8d, 5d, 8d, 5d),
            Background = new SolidColorBrush(Color.FromArgb(225, 18, 38, 51)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(145, 90, 176, 226)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(7d),
            Cursor = Cursors.SizeAll,
            Visibility = Visibility.Collapsed,
            Child = new TextBlock
            {
                Text = ImmersiveText(
                    "▦ ARRASTE O PAINEL • DUPLO CLIQUE = RESET",
                    "▦ DRAG PANEL • DOUBLE CLICK = RESET",
                    "▦ ARRASTRA EL PANEL • DOBLE CLIC = RESET",
                    "▦ PANEL ZIEHEN • DOPPELKLICK = RESET",
                    "▦ GLISSER LE PANNEAU • DOUBLE-CLIC = RESET"),
                Foreground = new SolidColorBrush(Color.FromRgb(151, 211, 244)),
                FontFamily = new FontFamily("Bahnschrift"),
                FontSize = 8.5d,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            }
        };
        _immersiveFocusMoveHandle.MouseLeftButtonDown += ImmersiveFocusHandle_MouseLeftButtonDown;
        _immersiveFocusMoveHandle.MouseMove += ImmersiveFocusHandle_MouseMove;
        _immersiveFocusMoveHandle.MouseLeftButtonUp += ImmersiveFocusHandle_MouseLeftButtonUp;
        stack.Children.Add(_immersiveFocusMoveHandle);

        _immersiveFocusEyebrowText = new TextBlock
        {
            Text = "NAVBR",
            Foreground = new SolidColorBrush(Color.FromRgb(122, 174, 208)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 9d,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center
        };
        stack.Children.Add(_immersiveFocusEyebrowText);

        _immersiveFocusPrimaryText = new TextBlock
        {
            Text = "—",
            Margin = new Thickness(0d, 4d, 0d, 0d),
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 42d,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        stack.Children.Add(_immersiveFocusPrimaryText);

        _immersiveFocusSecondaryText = new TextBlock
        {
            Text = "—",
            Margin = new Thickness(0d, 3d, 0d, 0d),
            Foreground = new SolidColorBrush(Color.FromRgb(171, 196, 211)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 10d,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(_immersiveFocusSecondaryText);

        _immersiveFocusStopsText = new TextBlock
        {
            Text = string.Empty,
            Margin = new Thickness(0d, 8d, 0d, 0d),
            Foreground = new SolidColorBrush(Color.FromRgb(154, 184, 202)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 9.5d,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Left,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        stack.Children.Add(_immersiveFocusStopsText);

        return new Border
        {
            Width = 430d,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0d, 0d, 0d, 18d),
            Padding = new Thickness(15d, 11d, 15d, 11d),
            Background = new SolidColorBrush(Color.FromArgb(226, 5, 17, 27)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 60, 130, 176)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(14d),
            Child = stack,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
    }

    private Border BuildImmersiveAlertPanel()
    {
        _immersiveAlertText = new TextBlock
        {
            Text = string.Empty,
            Foreground = new SolidColorBrush(Color.FromRgb(255, 222, 158)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 11d,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        return new Border
        {
            MaxWidth = 560d,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0d, 86d, 0d, 0d),
            Padding = new Thickness(12d, 7d, 12d, 7d),
            Background = new SolidColorBrush(Color.FromArgb(226, 43, 25, 5)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, 255, 177, 73)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            Child = _immersiveAlertText,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
    }

    private Border BuildImmersiveSideIndicatorPanel()
    {
        _immersiveSideIndicatorText = new TextBlock
        {
            Text = string.Empty,
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 10d,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        return new Border
        {
            MaxWidth = 190d,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 16d, 0d),
            Padding = new Thickness(9d, 7d, 9d, 7d),
            Background = new SolidColorBrush(Color.FromArgb(218, 6, 17, 25)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(145, 83, 145, 184)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Child = _immersiveSideIndicatorText,
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
            _immersiveMultiplayerPanel is null ||
            _immersiveFocusPanel is null ||
            _immersiveAlertPanel is null ||
            _immersiveSideIndicatorPanel is null)
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
        if (_immersiveFuelCell is not null)
        {
            _immersiveFuelCell.Visibility = active && settings.DashboardShowFuel
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        if (_immersiveDelayCell is not null)
        {
            _immersiveDelayCell.Visibility = active
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        if (_immersiveSpeedCell is not null)
        {
            _immersiveSpeedCell.Visibility = active
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        if (_immersiveNextStopCell is not null)
        {
            _immersiveNextStopCell.Visibility = active
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        _immersiveMiniMapPanel.Visibility = active && settings.DashboardShowMinimap
            ? Visibility.Visible
            : Visibility.Collapsed;
        _immersiveMultiplayerPanel.Visibility = active && settings.DashboardShowMultiplayer
            ? Visibility.Visible
            : Visibility.Collapsed;
        var showFocusPanel = active && preset.Id is
            "transit-control" or
            "cockpit-digital" or
            "navigation-pro" or
            "driver-assistance" or
            "city-operations" or
            "classic-omsi-plus";
        _immersiveFocusPanel.Visibility = showFocusPanel
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateImmersiveFocusEditState();
        _immersiveAlertPanel.Visibility = Visibility.Collapsed;
        _immersiveSideIndicatorPanel.Visibility = Visibility.Collapsed;

        var opacity = Math.Clamp(settings.DashboardOpacity, 0.35d, 1d);
        _immersiveTopBar.Opacity = opacity;
        _immersiveMiniMapPanel.Opacity = opacity;
        _immersiveMultiplayerPanel.Opacity = opacity;
        _immersiveFocusPanel.Opacity = opacity;
        _immersiveAlertPanel.Opacity = opacity;
        _immersiveSideIndicatorPanel.Opacity = opacity;

        var resolutionScale = settings.DashboardAutoScale
            ? GetResolutionScaleFactor()
            : 1d;
        var effectiveScale = Math.Clamp(
            settings.DashboardScale * resolutionScale,
            0.60d,
            1.80d);

        _immersiveTopBar.LayoutTransform = new ScaleTransform(
            effectiveScale,
            effectiveScale);
        _immersiveFocusPanel.LayoutTransform = new ScaleTransform(
            effectiveScale,
            effectiveScale);

        var minimapScale = Math.Clamp(
            effectiveScale * settings.DashboardMinimapScale,
            0.55d,
            2.25d);
        _immersiveMiniMapPanel.LayoutTransform = new ScaleTransform(
            minimapScale,
            minimapScale);

        var multiplayerScale = Math.Clamp(
            effectiveScale * settings.DashboardMultiplayerScale,
            0.55d,
            2.25d);
        _immersiveMultiplayerPanel.LayoutTransform = new ScaleTransform(
            multiplayerScale,
            multiplayerScale);

        var alertsScale = Math.Clamp(
            effectiveScale * settings.DashboardAlertsScale,
            0.55d,
            2.25d);
        _immersiveAlertPanel.LayoutTransform = new ScaleTransform(
            alertsScale,
            alertsScale);

        var sideIndicatorsScale = Math.Clamp(
            effectiveScale * settings.DashboardSideIndicatorsScale,
            0.55d,
            2.25d);
        _immersiveSideIndicatorPanel.LayoutTransform = new ScaleTransform(
            sideIndicatorsScale,
            sideIndicatorsScale);

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
            _immersiveMultiplayerPanel is null ||
            _immersiveFocusPanel is null)
        {
            return;
        }

        var compact = ActualWidth > 1d && ActualWidth < 1280d;
        var narrow = ActualWidth > 1d && ActualWidth < 1120d;
        var preset = HudProfileCatalog.ResolvePreset(_hudSettings.DashboardPreset);
        var presetId = preset.Id;
        var widthFactor = Math.Clamp(
            _hudSettings.DashboardWidth / Math.Max(1d, preset.Width),
            0.70d,
            1.35d);

        _immersiveTopBar.HorizontalAlignment = HorizontalAlignment.Stretch;
        _immersiveTopBar.Width = double.NaN;
        _immersiveTopBar.Margin = compact
            ? new Thickness(9d, 8d, 9d, 0d)
            : new Thickness(14d, 12d, 14d, 0d);

        _immersiveMiniMapPanel.HorizontalAlignment = HorizontalAlignment.Left;
        _immersiveMiniMapPanel.VerticalAlignment = VerticalAlignment.Bottom;
        _immersiveMultiplayerPanel.HorizontalAlignment = HorizontalAlignment.Right;
        _immersiveMultiplayerPanel.VerticalAlignment = VerticalAlignment.Bottom;
        _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Center;
        _immersiveFocusPanel.VerticalAlignment = VerticalAlignment.Bottom;
        _immersiveFocusPanel.Width = compact ? 360d : 430d;
        _immersiveFocusPanel.Margin = new Thickness(0d, 0d, 0d, compact ? 10d : 18d);
        _immersiveFocusPanel.MinHeight = 0d;
        _immersiveTopBar.MinHeight = 0d;

        var mapWidth = compact ? 292d : 342d;
        var mapHeight = compact ? 196d : 226d;
        var multiplayerWidth = compact ? 292d : 342d;
        var edge = compact ? 10d : 18d;

        if (_immersiveFocusStopsText is not null)
        {
            _immersiveFocusStopsText.Visibility = Visibility.Collapsed;
            _immersiveFocusStopsText.Text = string.Empty;
        }

        if (_immersiveFuelCell is not null)
        {
            _immersiveFuelCell.Visibility = _hudSettings.DashboardShowFuel ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_immersiveDelayCell is not null) _immersiveDelayCell.Visibility = Visibility.Visible;
        if (_immersiveSpeedCell is not null) _immersiveSpeedCell.Visibility = Visibility.Visible;
        if (_immersiveNextStopCell is not null) _immersiveNextStopCell.Visibility = Visibility.Visible;

        if (_immersiveLineText is not null) _immersiveLineText.FontSize = 23d;
        if (_immersiveRouteText is not null) _immersiveRouteText.FontSize = 10d;
        if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 16d;
        if (_immersiveNextStopText is not null) _immersiveNextStopText.FontSize = 15d;
        if (_immersiveSpeedText is not null) _immersiveSpeedText.FontSize = 15d;

        switch (presetId)
        {
            case "transit-control":
                _immersiveFocusPanel.Width = compact ? 310d : 380d;
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveFocusPanel.Margin = new Thickness(0d, 0d, 0d, compact ? 10d : 18d);
                mapWidth = compact ? 320d : 390d;
                mapHeight = compact ? 210d : 258d;
                multiplayerWidth = compact ? 320d : 370d;
                if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 17d;
                if (_immersiveNextStopText is not null) _immersiveNextStopText.FontSize = 16d;
                break;

            case "cockpit-digital":
                if (_immersiveSpeedCell is not null) _immersiveSpeedCell.Visibility = Visibility.Collapsed;
                _immersiveFocusPanel.Width = compact ? 370d : 455d;
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveFocusPanel.Margin = new Thickness(0d, 0d, 0d, compact ? 12d : 20d);
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
                if (_immersiveNextStopCell is not null) _immersiveNextStopCell.Visibility = Visibility.Collapsed;
                _immersiveFocusPanel.Width = compact ? 320d : 390d;
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Right;
                _immersiveFocusPanel.Margin = new Thickness(0d, 0d, compact ? 12d : 20d, compact ? 12d : 20d);
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
                multiplayerWidth = compact ? 420d : 490d;
                if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 15d;
                break;

            case "minimal-driver":
                if (_immersiveDelayCell is not null) _immersiveDelayCell.Visibility = Visibility.Collapsed;
                if (_immersiveFuelCell is not null) _immersiveFuelCell.Visibility = Visibility.Collapsed;
                _immersiveTopBar.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveTopBar.Width = compact ? 560d : 660d;
                _immersiveTopBar.Margin = compact
                    ? new Thickness(0d, 8d, 0d, 0d)
                    : new Thickness(0d, 12d, 0d, 0d);
                mapWidth = compact ? 210d : 240d;
                mapHeight = compact ? 145d : 165d;
                multiplayerWidth = compact ? 230d : 260d;
                if (_immersiveLineText is not null) _immersiveLineText.FontSize = 21d;
                if (_immersiveSpeedText is not null) _immersiveSpeedText.FontSize = 23d;
                if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 14d;
                if (_immersiveNextStopText is not null) _immersiveNextStopText.FontSize = 16d;
                break;

            case "streamer-broadcast":
                if (_immersiveDelayCell is not null) _immersiveDelayCell.Visibility = Visibility.Collapsed;
                if (_immersiveFuelCell is not null) _immersiveFuelCell.Visibility = Visibility.Collapsed;
                _immersiveTopBar.HorizontalAlignment = HorizontalAlignment.Left;
                _immersiveTopBar.Width = compact ? 610d : 760d;
                _immersiveTopBar.Margin = compact
                    ? new Thickness(14d, 10d, 0d, 0d)
                    : new Thickness(28d, 18d, 0d, 0d);
                mapWidth = compact ? 250d : 300d;
                mapHeight = compact ? 170d : 195d;
                multiplayerWidth = compact ? 310d : 360d;
                edge = compact ? 14d : 28d;
                if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 15d;
                break;

            case "glass-night":
                _immersiveTopBar.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveTopBar.Width = compact ? 640d : 780d;
                mapWidth = compact ? 235d : 275d;
                mapHeight = compact ? 155d : 185d;
                multiplayerWidth = compact ? 255d : 295d;
                if (_immersiveLineText is not null) _immersiveLineText.FontSize = 22d;
                if (_immersiveSpeedText is not null) _immersiveSpeedText.FontSize = 20d;
                break;

            case "city-operations":
                _immersiveFocusPanel.Width = compact ? 330d : 400d;
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveFocusPanel.Margin = new Thickness(0d, 0d, 0d, compact ? 12d : 20d);
                mapWidth = compact ? 360d : 440d;
                mapHeight = compact ? 230d : 285d;
                multiplayerWidth = compact ? 360d : 430d;
                if (_immersiveDestinationText is not null) _immersiveDestinationText.FontSize = 17d;
                if (_immersiveNextStopText is not null) _immersiveNextStopText.FontSize = 16d;
                break;

            case "driver-assistance":
                if (_immersiveNextStopCell is not null) _immersiveNextStopCell.Visibility = Visibility.Collapsed;
                _immersiveFocusPanel.Width = compact ? 390d : 470d;
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveFocusPanel.Margin = new Thickness(0d, 0d, 0d, compact ? 12d : 22d);
                if (_immersiveFuelCell is not null) _immersiveFuelCell.Visibility = Visibility.Collapsed;
                _immersiveTopBar.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveTopBar.Width = compact ? 700d : 880d;
                mapWidth = compact ? 310d : 380d;
                mapHeight = compact ? 205d : 245d;
                multiplayerWidth = compact ? 250d : 285d;
                if (_immersiveLineText is not null) _immersiveLineText.FontSize = 22d;
                if (_immersiveNextStopText is not null) _immersiveNextStopText.FontSize = 19d;
                if (_immersiveSpeedText is not null) _immersiveSpeedText.FontSize = 22d;
                break;

            case "classic-omsi-plus":
                _immersiveFocusPanel.Width = compact ? 430d : 520d;
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveFocusPanel.Margin = new Thickness(0d, 0d, 0d, compact ? 10d : 18d);
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

        if (narrow && presetId is "transit-control" or "city-operations")
        {
            _immersiveFocusPanel.VerticalAlignment = VerticalAlignment.Top;
            _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Center;
            _immersiveFocusPanel.Margin = new Thickness(
                0d,
                compact ? 142d : 154d,
                0d,
                0d);
        }

        ApplyComposedFocusAnchor(
            HudProfileCatalog.ResolveAnchor(_hudSettings.DashboardAnchor),
            compact,
            mapHeight,
            multiplayerWidth,
            edge);

        if (!double.IsNaN(_immersiveTopBar.Width))
        {
            _immersiveTopBar.Width *= widthFactor;
        }
        _immersiveFocusPanel.Width *= widthFactor;
        _immersiveMiniMapPanel.Width = mapWidth * widthFactor;
        _immersiveMiniMapPanel.Height = mapHeight;
        _immersiveMultiplayerPanel.Width = multiplayerWidth * widthFactor;
        _immersiveMiniMapPanel.Margin = new Thickness(edge, 0d, 0d, edge);
        _immersiveMultiplayerPanel.Margin = new Thickness(0d, 0d, edge, edge);

        if (_hudSettings.DashboardHeight > 0d)
        {
            var requestedHeight = Math.Clamp(_hudSettings.DashboardHeight, 80d, 720d);
            if (_immersiveFocusPanel.Visibility == Visibility.Visible)
            {
                _immersiveFocusPanel.MinHeight = requestedHeight;
            }
            else
            {
                _immersiveTopBar.MinHeight = requestedHeight;
            }
        }

        ApplyComposedPresetPalette(presetId);
    }

    private void ApplyComposedFocusAnchor(
        string anchor,
        bool compact,
        double mapHeight,
        double multiplayerWidth,
        double edge)
    {
        if (_immersiveFocusPanel is null ||
            _immersiveFocusPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        if (anchor == "custom")
        {
            ApplyImmersiveFocusCustomPosition();
            return;
        }

        if (anchor == HudProfileCatalog.DefaultAnchor)
        {
            return;
        }

        var side = compact ? 12d : 20d;
        var top = compact ? 104d : 118d;
        var bottom = compact ? 12d : 20d;

        switch (anchor)
        {
            case "top-left":
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Left;
                _immersiveFocusPanel.VerticalAlignment = VerticalAlignment.Top;
                _immersiveFocusPanel.Margin = new Thickness(side, top, 0d, 0d);
                break;

            case "top-center":
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveFocusPanel.VerticalAlignment = VerticalAlignment.Top;
                _immersiveFocusPanel.Margin = new Thickness(0d, top, 0d, 0d);
                break;

            case "top-right":
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Right;
                _immersiveFocusPanel.VerticalAlignment = VerticalAlignment.Top;
                _immersiveFocusPanel.Margin = new Thickness(0d, top, side, 0d);
                break;

            case "bottom-left":
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Left;
                _immersiveFocusPanel.VerticalAlignment = VerticalAlignment.Bottom;
                _immersiveFocusPanel.Margin = new Thickness(
                    side,
                    0d,
                    0d,
                    _immersiveMiniMapPanel?.Visibility == Visibility.Visible
                        ? mapHeight + edge + 14d
                        : bottom);
                break;

            case "bottom-center":
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Center;
                _immersiveFocusPanel.VerticalAlignment = VerticalAlignment.Bottom;
                _immersiveFocusPanel.Margin = new Thickness(0d, 0d, 0d, bottom);
                break;

            case "bottom-right":
                _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Right;
                _immersiveFocusPanel.VerticalAlignment = VerticalAlignment.Bottom;
                _immersiveFocusPanel.Margin = new Thickness(
                    0d,
                    0d,
                    side,
                    _immersiveMultiplayerPanel?.Visibility == Visibility.Visible
                        ? Math.Max(bottom, 178d + edge)
                        : bottom);
                break;
        }
    }

    private void ImmersiveOperation_LayoutEditModeChanged(bool enabled)
    {
        UpdateImmersiveFocusEditState();
    }

    private void UpdateImmersiveFocusEditState()
    {
        if (_immersiveFocusPanel is null || _immersiveFocusMoveHandle is null)
        {
            return;
        }

        var enabled = _hudLayoutEditMode &&
                      _immersiveOperationActive &&
                      _immersiveFocusPanel.Visibility == Visibility.Visible;
        _immersiveFocusPanel.IsHitTestVisible = enabled;
        _immersiveFocusMoveHandle.Visibility = enabled
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!enabled && _immersiveFocusDragging)
        {
            EndImmersiveFocusDrag(save: true);
        }
    }

    private void ImmersiveFocusHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_hudLayoutEditMode ||
            _immersiveFocusPanel is null ||
            _immersiveFocusMoveHandle is null)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            _hudSettings = _hudSettings with
            {
                DashboardAnchor = HudProfileCatalog.DefaultAnchor
            };
            MultiplayerSettingsStore.Save(_hudSettings);
            ApplyImmersiveOperationSizing();
            e.Handled = true;
            return;
        }

        var topLeft = _immersiveFocusPanel.TranslatePoint(new Point(0d, 0d), OverlayRoot);
        _immersiveFocusDragging = true;
        _immersiveFocusDragStartMouse = e.GetPosition(OverlayRoot);
        _immersiveFocusDragStartPosition = topLeft;
        _hudSettings = _hudSettings with { DashboardAnchor = "custom" };

        SetImmersiveFocusPosition(topLeft.X, topLeft.Y);
        _immersiveFocusMoveHandle.CaptureMouse();
        e.Handled = true;
    }

    private void ImmersiveFocusHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_immersiveFocusDragging ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(OverlayRoot);
        SetImmersiveFocusPosition(
            _immersiveFocusDragStartPosition.X + current.X - _immersiveFocusDragStartMouse.X,
            _immersiveFocusDragStartPosition.Y + current.Y - _immersiveFocusDragStartMouse.Y);
        e.Handled = true;
    }

    private void ImmersiveFocusHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_immersiveFocusDragging)
        {
            return;
        }

        EndImmersiveFocusDrag(save: true);
        e.Handled = true;
    }

    private void EndImmersiveFocusDrag(bool save)
    {
        _immersiveFocusDragging = false;
        _immersiveFocusMoveHandle?.ReleaseMouseCapture();

        if (!save || _immersiveFocusPanel is null)
        {
            return;
        }

        var scale = GetImmersiveDashboardEffectiveScale();
        var width = Math.Max(
            1d,
            (_immersiveFocusPanel.ActualWidth > 1d
                ? _immersiveFocusPanel.ActualWidth
                : _immersiveFocusPanel.Width) * scale);
        var height = Math.Max(
            1d,
            (_immersiveFocusPanel.ActualHeight > 1d
                ? _immersiveFocusPanel.ActualHeight
                : 120d) * scale);
        var maxX = Math.Max(1d, ActualWidth - width - 16d);
        var maxY = Math.Max(1d, ActualHeight - height - 16d);

        _hudSettings = _hudSettings with
        {
            DashboardAnchor = "custom",
            DashboardX = Math.Clamp((_immersiveFocusPanel.Margin.Left - 8d) / maxX, 0d, 1d),
            DashboardY = Math.Clamp((_immersiveFocusPanel.Margin.Top - 8d) / maxY, 0d, 1d)
        };
        MultiplayerSettingsStore.Save(_hudSettings);
    }

    private void ApplyImmersiveFocusCustomPosition()
    {
        if (_immersiveFocusPanel is null ||
            ActualWidth <= 1d ||
            ActualHeight <= 1d)
        {
            return;
        }

        var scale = GetImmersiveDashboardEffectiveScale();
        var width = Math.Max(
            1d,
            (_immersiveFocusPanel.ActualWidth > 1d
                ? _immersiveFocusPanel.ActualWidth
                : _immersiveFocusPanel.Width) * scale);
        var height = Math.Max(
            1d,
            (_immersiveFocusPanel.ActualHeight > 1d
                ? _immersiveFocusPanel.ActualHeight
                : 120d) * scale);
        var maxX = Math.Max(1d, ActualWidth - width - 16d);
        var maxY = Math.Max(1d, ActualHeight - height - 16d);

        SetImmersiveFocusPosition(
            8d + Math.Clamp(_hudSettings.DashboardX, 0d, 1d) * maxX,
            8d + Math.Clamp(_hudSettings.DashboardY, 0d, 1d) * maxY);
    }

    private void SetImmersiveFocusPosition(double x, double y)
    {
        if (_immersiveFocusPanel is null)
        {
            return;
        }

        var scale = GetImmersiveDashboardEffectiveScale();
        var width = Math.Max(
            1d,
            (_immersiveFocusPanel.ActualWidth > 1d
                ? _immersiveFocusPanel.ActualWidth
                : _immersiveFocusPanel.Width) * scale);
        var height = Math.Max(
            1d,
            (_immersiveFocusPanel.ActualHeight > 1d
                ? _immersiveFocusPanel.ActualHeight
                : 120d) * scale);

        _immersiveFocusPanel.HorizontalAlignment = HorizontalAlignment.Left;
        _immersiveFocusPanel.VerticalAlignment = VerticalAlignment.Top;
        _immersiveFocusPanel.Margin = new Thickness(
            Math.Clamp(x, 8d, Math.Max(8d, ActualWidth - width - 8d)),
            Math.Clamp(y, 8d, Math.Max(8d, ActualHeight - height - 8d)),
            0d,
            0d);
    }

    private double GetImmersiveDashboardEffectiveScale()
    {
        var resolutionScale = _hudSettings.DashboardAutoScale
            ? GetResolutionScaleFactor()
            : 1d;
        return Math.Clamp(
            _hudSettings.DashboardScale * resolutionScale,
            0.60d,
            1.80d);
    }

    private void ApplyComposedPresetPalette(string presetId)
    {
        if (_immersiveTopBar is null ||
            _immersiveMiniMapPanel is null ||
            _immersiveMultiplayerPanel is null ||
            _immersiveFocusPanel is null ||
            _immersiveSideIndicatorPanel is null)
        {
            return;
        }

        var selectedTheme = HudProfileCatalog.ResolveTheme(_hudSettings.DashboardTheme).Id;
        var paletteId = selectedTheme switch
        {
            "current" => presetId,
            "navbr-modern" => "immersive-operation",
            "urban-glass" => "urban-glass",
            "route-night" => "route-night",
            "racing-clean" => "racing-clean",
            "bus-panel" => "bus-panel",
            "lcd" => "lcd",
            "amber-classic" => "amber-classic",
            "light" => "light",
            _ => selectedTheme
        };

        var palette = paletteId switch
        {
            "transit-control" => new ComposedHudPalette(
                Color.FromRgb(4, 18, 27), Color.FromRgb(40, 126, 161), Color.FromRgb(54, 211, 152), Color.FromRgb(238, 248, 251), 10d),
            "cockpit-digital" => new ComposedHudPalette(
                Color.FromRgb(4, 13, 20), Color.FromRgb(38, 116, 153), Color.FromRgb(52, 199, 255), Color.FromRgb(239, 249, 253), 16d),
            "navigation-pro" or "route-night" => new ComposedHudPalette(
                Color.FromRgb(6, 14, 24), Color.FromRgb(51, 102, 148), Color.FromRgb(255, 166, 59), Color.FromRgb(241, 247, 251), 12d),
            "multiplayer-focus" => new ComposedHudPalette(
                Color.FromRgb(9, 12, 25), Color.FromRgb(91, 77, 158), Color.FromRgb(130, 193, 255), Color.FromRgb(244, 242, 255), 12d),
            "minimal-driver" or "racing-clean" => new ComposedHudPalette(
                Color.FromRgb(5, 13, 18), Color.FromRgb(47, 79, 96), Color.FromRgb(225, 237, 243), Color.FromRgb(238, 246, 250), 8d),
            "streamer-broadcast" => new ComposedHudPalette(
                Color.FromRgb(6, 13, 22), Color.FromRgb(61, 105, 139), Color.FromRgb(92, 197, 255), Color.FromRgb(240, 247, 251), 14d),
            "glass-night" => new ComposedHudPalette(
                Color.FromRgb(3, 9, 17), Color.FromRgb(38, 82, 113), Color.FromRgb(91, 172, 229), Color.FromRgb(210, 230, 243), 16d),
            "urban-glass" => new ComposedHudPalette(
                Color.FromRgb(4, 15, 20), Color.FromRgb(69, 124, 132), Color.FromRgb(82, 230, 166), Color.FromRgb(233, 248, 246), 17d),
            "city-operations" => new ComposedHudPalette(
                Color.FromRgb(5, 20, 25), Color.FromRgb(42, 119, 111), Color.FromRgb(68, 224, 179), Color.FromRgb(235, 250, 247), 10d),
            "driver-assistance" => new ComposedHudPalette(
                Color.FromRgb(7, 15, 22), Color.FromRgb(74, 109, 137), Color.FromRgb(255, 194, 72), Color.FromRgb(246, 249, 251), 11d),
            "classic-omsi-plus" or "bus-panel" => new ComposedHudPalette(
                Color.FromRgb(17, 10, 3), Color.FromRgb(126, 80, 22), Color.FromRgb(255, 177, 49), Color.FromRgb(255, 209, 126), 4d),
            "lcd" => new ComposedHudPalette(
                Color.FromRgb(5, 16, 19), Color.FromRgb(93, 116, 121), Color.FromRgb(211, 231, 235), Color.FromRgb(223, 239, 241), 6d),
            "amber-classic" => new ComposedHudPalette(
                Color.FromRgb(18, 10, 2), Color.FromRgb(135, 78, 8), Color.FromRgb(255, 171, 31), Color.FromRgb(255, 205, 111), 5d),
            "light" => new ComposedHudPalette(
                Color.FromRgb(231, 238, 244), Color.FromRgb(140, 164, 183), Color.FromRgb(26, 111, 176), Color.FromRgb(19, 38, 54), 14d),
            _ => new ComposedHudPalette(
                Color.FromRgb(4, 15, 24), Color.FromRgb(54, 121, 166), Color.FromRgb(58, 169, 255), Color.FromRgb(235, 244, 250), 12d)
        };

        var requestedOpacity = Math.Clamp(_hudSettings.DashboardOpacity, 0.35d, 1d);
        if (paletteId == "glass-night")
        {
            requestedOpacity = Math.Min(requestedOpacity, 0.72d);
        }
        var opacity = (byte)Math.Clamp((int)Math.Round(requestedOpacity * 255d), 0, 255);
        var background = new SolidColorBrush(Color.FromArgb(opacity, palette.Background.R, palette.Background.G, palette.Background.B));
        var border = new SolidColorBrush(palette.Border);
        var accent = new SolidColorBrush(palette.Accent);
        var text = new SolidColorBrush(palette.Text);

        foreach (var panel in new[] { _immersiveTopBar, _immersiveMiniMapPanel, _immersiveMultiplayerPanel, _immersiveFocusPanel, _immersiveSideIndicatorPanel })
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
        if (_immersiveFocusEyebrowText is not null) _immersiveFocusEyebrowText.Foreground = accent;
        if (_immersiveFocusPrimaryText is not null) _immersiveFocusPrimaryText.Foreground = text;
        if (_immersiveFocusSecondaryText is not null) _immersiveFocusSecondaryText.Foreground = new SolidColorBrush(Color.FromArgb(220, palette.Text.R, palette.Text.G, palette.Text.B));
        if (_immersiveSideIndicatorText is not null) _immersiveSideIndicatorText.Foreground = text;

        var monospaced = paletteId is "classic-omsi-plus" or "bus-panel" or "lcd" or "amber-classic";
        var font = monospaced
            ? new FontFamily("Consolas")
            : new FontFamily("Bahnschrift");

        foreach (var label in new[]
        {
            _immersiveLineText, _immersiveRouteText, _immersiveDestinationText,
            _immersiveNextStopText, _immersiveSpeedText, _immersiveDelayText,
            _immersiveFuelText, _immersiveMapTitleText, _immersiveStreetText,
            _immersiveSessionText, _immersivePlayersText, _immersiveNearbyPlayersText,
            _immersiveChatText, _immersiveVoiceText,
            _immersiveFocusEyebrowText, _immersiveFocusPrimaryText, _immersiveFocusSecondaryText,
            _immersiveFocusStopsText, _immersiveSideIndicatorText
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

            var presetId = HudProfileCatalog.ResolvePreset(_hudSettings.DashboardPreset).Id;
            if (presetId == "minimal-driver")
            {
                var normalOperation = status.Color == Color.FromRgb(115, 222, 166);
                _immersiveVehicleStatusText.Visibility = normalOperation
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
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

        RenderContextualPresetVisibility(telemetry);
        RenderComposedAlertsAndIndicators(telemetry);
        RenderComposedFocusPanel(telemetry);
    }

    private void RenderContextualPresetVisibility(VehicleTelemetry? telemetry)
    {
        if (_immersiveMiniMapPanel is null ||
            !_immersiveOperationActive)
        {
            return;
        }

        var presetId = HudProfileCatalog.ResolvePreset(_hudSettings.DashboardPreset).Id;
        if (presetId != "minimal-driver")
        {
            _immersiveMiniMapPanel.Visibility = _hudSettings.DashboardShowMinimap
                ? Visibility.Visible
                : Visibility.Collapsed;
            return;
        }

        if (!_hudSettings.DashboardShowMinimap)
        {
            _immersiveMiniMapPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var navigation = BuildImmersiveNavigationSnapshot(telemetry);
        var needsMap = navigation.RouteAvailable && !navigation.IsOnRoute;
        _immersiveMiniMapPanel.Visibility = needsMap
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RenderComposedAlertsAndIndicators(VehicleTelemetry? telemetry)
    {
        if (_immersiveAlertPanel is null ||
            _immersiveAlertText is null ||
            _immersiveSideIndicatorPanel is null ||
            _immersiveSideIndicatorText is null)
        {
            return;
        }

        var alerts = new List<string>();
        var severe = false;
        if (telemetry is not null)
        {
            if (telemetry.Doors != VehicleDoorFlags.None && telemetry.SpeedKph > 1d)
            {
                alerts.Add(ImmersiveText(
                    "PORTAS ABERTAS EM MOVIMENTO",
                    "DOORS OPEN WHILE MOVING",
                    "PUERTAS ABIERTAS EN MOVIMIENTO",
                    "TÜREN WÄHREND DER FAHRT OFFEN",
                    "PORTES OUVERTES EN MOUVEMENT"));
                severe = true;
            }

            if (telemetry.ParkingBrakeActive && telemetry.SpeedKph > 3d)
            {
                alerts.Add(ImmersiveText(
                    "FREIO DE ESTACIONAMENTO ATIVO",
                    "PARKING BRAKE ACTIVE",
                    "FRENO DE ESTACIONAMIENTO ACTIVO",
                    "FESTSTELLBREMSE AKTIV",
                    "FREIN DE PARC ACTIF"));
                severe = true;
            }

            if (telemetry.ReverseGear)
            {
                alerts.Add(ImmersiveText(
                    "RÉ ATIVA",
                    "REVERSE ACTIVE",
                    "REVERSA ACTIVA",
                    "RÜCKWÄRTSGANG AKTIV",
                    "MARCHE ARRIÈRE ACTIVE"));
            }

            var navigation = BuildImmersiveNavigationSnapshot(telemetry);
            if (navigation.RouteAvailable && !navigation.IsOnRoute)
            {
                alerts.Add(
                    $"{ImmersiveText("FORA DA ROTA", "OFF ROUTE", "FUERA DE RUTA", "ROUTE VERLASSEN", "HORS ITINÉRAIRE")} • " +
                    $"{FormatNavigationDistance(navigation.OffRouteDistanceMeters)}");
            }
        }

        var alertsEnabled = _immersiveOperationActive &&
                            _hudSettings.DashboardShowAlerts &&
                            alerts.Count > 0;
        _immersiveAlertPanel.Visibility = alertsEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (alertsEnabled)
        {
            _immersiveAlertText.Text = string.Join("   •   ", alerts.Take(2));
            _immersiveAlertText.Foreground = new SolidColorBrush(
                severe ? Color.FromRgb(255, 141, 112) : Color.FromRgb(255, 222, 158));
            _immersiveAlertPanel.BorderBrush = new SolidColorBrush(
                severe ? Color.FromArgb(225, 255, 112, 88) : Color.FromArgb(210, 255, 177, 73));
        }

        var indicators = new List<string>();
        if (telemetry is not null)
        {
            if (telemetry.Doors != VehicleDoorFlags.None)
            {
                indicators.Add(ImmersiveText("PORTAS", "DOORS", "PUERTAS", "TÜREN", "PORTES"));
            }

            switch (telemetry.TurnSignal)
            {
                case TurnSignalState.Left:
                    indicators.Add("◀");
                    break;
                case TurnSignalState.Right:
                    indicators.Add("▶");
                    break;
                case TurnSignalState.Hazard:
                    indicators.Add("⚠");
                    break;
            }

            if (telemetry.Lights.HasFlag(VehicleLightFlags.LowBeam) ||
                telemetry.Lights.HasFlag(VehicleLightFlags.HighBeam))
            {
                indicators.Add(ImmersiveText("LUZ", "LIGHTS", "LUCES", "LICHT", "FEUX"));
            }
            if (telemetry.ParkingBrakeActive)
            {
                indicators.Add("P");
            }
            if (telemetry.ReverseGear)
            {
                indicators.Add("R");
            }
            if (telemetry.WipersActive)
            {
                indicators.Add(ImmersiveText("LIMP", "WIP", "LIMP", "WISCH", "ESS"));
            }
            if (telemetry.StopRequested)
            {
                indicators.Add(ImmersiveText("PARADA", "STOP", "PARADA", "HALT", "ARRÊT"));
            }
        }

        var sideEnabled = _immersiveOperationActive &&
                          _hudSettings.DashboardShowSideIndicators &&
                          indicators.Count > 0;
        _immersiveSideIndicatorPanel.Visibility = sideEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (sideEnabled)
        {
            _immersiveSideIndicatorText.Text = string.Join(Environment.NewLine, indicators);
        }
    }

    private void RenderComposedFocusPanel(VehicleTelemetry? telemetry)
    {
        if (_immersiveFocusPanel is null ||
            _immersiveFocusEyebrowText is null ||
            _immersiveFocusPrimaryText is null ||
            _immersiveFocusSecondaryText is null ||
            _immersiveFocusPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        var presetId = HudProfileCatalog.ResolvePreset(_hudSettings.DashboardPreset).Id;
        var navigation = BuildImmersiveNavigationSnapshot(telemetry);
        var eta = ObserveImmersiveEta(navigation);
        if (_immersiveFocusStopsText is not null)
        {
            _immersiveFocusStopsText.Visibility = Visibility.Collapsed;
            _immersiveFocusStopsText.Text = string.Empty;
        }

        switch (presetId)
        {
            case "transit-control":
                _immersiveFocusEyebrowText.Text = ImmersiveText(
                    "PRÓXIMAS PARADAS", "UPCOMING STOPS", "PRÓXIMAS PARADAS", "NÄCHSTE HALTE", "PROCHAINS ARRÊTS");
                _immersiveFocusPrimaryText.FontSize = 20d;
                _immersiveFocusPrimaryText.Text = string.IsNullOrWhiteSpace(telemetry?.NextStopName)
                    ? ImmersiveText("Próxima parada —", "Next stop —", "Próxima parada —", "Nächster Halt —", "Prochain arrêt —")
                    : telemetry.NextStopName;
                _immersiveFocusSecondaryText.Text = BuildFocusServiceText(telemetry);
                RenderOrderedStopsIntoFocus(telemetry, 4);
                break;

            case "cockpit-digital":
                _immersiveFocusEyebrowText.Text = ImmersiveText(
                    "COCKPIT DIGITAL", "DIGITAL COCKPIT", "CABINA DIGITAL", "DIGITALES COCKPIT", "COCKPIT NUMÉRIQUE");
                _immersiveFocusPrimaryText.FontSize = 46d;
                _immersiveFocusPrimaryText.Text = telemetry is null
                    ? "— km/h"
                    : $"{Math.Clamp(telemetry.SpeedKph, 0d, 999d):F0} km/h";
                _immersiveFocusSecondaryText.Text = BuildFocusServiceText(telemetry);
                break;

            case "navigation-pro":
                _immersiveFocusEyebrowText.Text = ImmersiveText(
                    "NAVEGAÇÃO", "NAVIGATION", "NAVEGACIÓN", "NAVIGATION", "NAVIGATION");
                _immersiveFocusPrimaryText.FontSize = 22d;
                _immersiveFocusPrimaryText.Text = BuildNavigationPrimaryText(navigation, telemetry);

                var navigationRows = new List<string>();
                if (navigation.RouteAvailable)
                {
                    var routeState = navigation.IsOnRoute
                        ? $"{navigation.RouteProgressPercent:0}% • {FormatNavigationDistance(navigation.DistanceRemainingMeters)}"
                        : $"{ImmersiveText("FORA DA ROTA", "OFF ROUTE", "FUERA DE RUTA", "ROUTE VERLASSEN", "HORS ITINÉRAIRE")} • {FormatNavigationDistance(navigation.OffRouteDistanceMeters)}";
                    navigationRows.Add(routeState);

                    if (!string.IsNullOrWhiteSpace(navigation.NextStopName))
                    {
                        var stopDistance = navigation.DistanceToNextStopMeters is double stopMeters
                            ? $" • {FormatNavigationDistance(stopMeters)}"
                            : string.Empty;
                        var stopEta = eta.ToNextStop is TimeSpan nextEta
                            ? $" • ETA {FormatImmersiveEta(nextEta)}"
                            : string.Empty;
                        navigationRows.Add($"{navigation.NextStopName}{stopDistance}{stopEta}");
                    }

                    if (eta.ToRouteEnd is TimeSpan routeEta)
                    {
                        navigationRows.Add($"{ImmersiveText("FIM DA ROTA", "ROUTE END", "FIN DE RUTA", "ROUTENENDE", "FIN DE LIGNE")} • ETA {FormatImmersiveEta(routeEta)}");
                    }
                }
                else
                {
                    navigationRows.Add(BuildFocusServiceText(telemetry));
                }

                if (!string.IsNullOrWhiteSpace(telemetry?.CurrentStreetName))
                {
                    navigationRows.Insert(0, telemetry.CurrentStreetName);
                }

                _immersiveFocusSecondaryText.Text = string.Join(Environment.NewLine, navigationRows);
                RenderOrderedStopsIntoFocus(telemetry, 3);
                break;

            case "classic-omsi-plus":
                _immersiveFocusEyebrowText.Text = ImmersiveText(
                    "DISPLAY OMSI", "OMSI DISPLAY", "DISPLAY OMSI", "OMSI-ANZEIGE", "AFFICHEUR OMSI");
                _immersiveFocusPrimaryText.FontSize = 24d;
                _immersiveFocusPrimaryText.FontFamily = new FontFamily("Consolas");
                _immersiveFocusSecondaryText.FontFamily = new FontFamily("Consolas");
                var classicLine = string.IsNullOrWhiteSpace(telemetry?.Line) ? "—" : telemetry.Line;
                var classicDestination = string.IsNullOrWhiteSpace(telemetry?.DestinationName)
                    ? ImmersiveText("DESTINO —", "DESTINATION —", "DESTINO —", "ZIEL —", "DESTINATION —")
                    : telemetry.DestinationName;
                _immersiveFocusPrimaryText.Text = $"{classicLine}  {classicDestination}";
                var classicNext = string.IsNullOrWhiteSpace(telemetry?.NextStopName)
                    ? ImmersiveText("PRÓXIMA —", "NEXT —", "PRÓXIMA —", "NÄCHSTER —", "PROCHAIN —")
                    : telemetry.NextStopName;
                _immersiveFocusSecondaryText.Text =
                    $"{ImmersiveText("PRÓXIMA", "NEXT", "PRÓXIMA", "NÄCHSTER", "PROCHAIN")}: {classicNext}";
                break;

            case "city-operations":
                _immersiveFocusEyebrowText.Text = ImmersiveText(
                    "OPERAÇÃO", "OPERATIONS", "OPERACIÓN", "BETRIEB", "EXPLOITATION");
                _immersiveFocusPrimaryText.FontSize = 19d;
                var operationsStatus = BuildImmersiveVehicleStatus(telemetry);
                var operationsRows = new List<string>();

                if (_hudSettings.DashboardShowPedals)
                {
                    var throttle = telemetry?.ThrottlePercent is double throttleValue && double.IsFinite(throttleValue)
                        ? $"{Math.Clamp(throttleValue, 0d, 100d):F0}%"
                        : "—";
                    var brake = telemetry?.BrakePercent is double brakeValue && double.IsFinite(brakeValue)
                        ? $"{Math.Clamp(brakeValue, 0d, 100d):F0}%"
                        : "—";
                    _immersiveFocusPrimaryText.Text =
                        $"{ImmersiveText("ACEL", "THR", "ACEL", "GAS", "ACC")}: {throttle}   •   " +
                        $"{ImmersiveText("FREIO", "BRK", "FRENO", "BREMSE", "FREIN")}: {brake}";
                    if (_hudSettings.DashboardShowStatus)
                    {
                        operationsRows.Add(operationsStatus.Text);
                    }
                }
                else if (_hudSettings.DashboardShowStatus)
                {
                    _immersiveFocusPrimaryText.Text = operationsStatus.Text;
                    _immersiveFocusPrimaryText.Foreground = new SolidColorBrush(operationsStatus.Color);
                }
                else
                {
                    _immersiveFocusPrimaryText.Text = BuildFocusServiceText(telemetry);
                }

                operationsRows.Add(BuildFocusServiceText(telemetry));
                _immersiveFocusSecondaryText.Text = string.Join(Environment.NewLine, operationsRows.Distinct());
                break;

            case "driver-assistance":
                _immersiveFocusEyebrowText.Text = ImmersiveText(
                    "ASSISTÊNCIA", "DRIVER ASSISTANCE", "ASISTENCIA", "FAHRASSISTENZ", "ASSISTANCE");
                _immersiveFocusPrimaryText.FontSize = 20d;
                var status = BuildImmersiveVehicleStatus(telemetry);
                if (_hudSettings.DashboardShowStatus)
                {
                    _immersiveFocusPrimaryText.Text = status.Text;
                    _immersiveFocusPrimaryText.Foreground = new SolidColorBrush(status.Color);
                }
                else
                {
                    _immersiveFocusPrimaryText.Text = BuildNavigationPrimaryText(navigation, telemetry);
                }

                var nextStop = string.IsNullOrWhiteSpace(telemetry?.NextStopName)
                    ? ImmersiveText("Próxima parada —", "Next stop —", "Próxima parada —", "Nächster Halt —", "Prochain arrêt —")
                    : telemetry.NextStopName;
                var speed = telemetry is null ? "— km/h" : $"{Math.Clamp(telemetry.SpeedKph, 0d, 999d):F0} km/h";
                var assistanceRows = new List<string> { $"{nextStop}   •   {speed}" };
                if (_hudSettings.DashboardShowStatus &&
                    navigation.RouteAvailable &&
                    (!navigation.IsOnRoute || navigation.Maneuver != NavBRManeuverKind.None))
                {
                    assistanceRows.Add(BuildNavigationPrimaryText(navigation, telemetry));
                }
                _immersiveFocusSecondaryText.Text = string.Join(Environment.NewLine, assistanceRows);
                break;
        }
    }

    private NavBRNavigationSnapshot BuildImmersiveNavigationSnapshot(VehicleTelemetry? telemetry)
    {
        if (telemetry is null || _activeMap is null || _mapLayout is null)
        {
            return NavBRNavigationSnapshot.Unavailable(telemetry);
        }

        EnsureRouteTrace(
            _activeMap,
            _mapLayout,
            telemetry.Line,
            telemetry.Route,
            telemetry.DestinationName);

        return NavBRNavigationEngine.Evaluate(
            telemetry,
            _mapLayout,
            _routeTracePoints,
            _busStops);
    }

    private NavBRNavigationEtaEstimate ObserveImmersiveEta(NavBRNavigationSnapshot navigation)
    {
        var now = DateTimeOffset.UtcNow;
        if (!navigation.RouteAvailable || !navigation.IsOnRoute)
        {
            _immersiveEtaEstimator.Reset();
            _immersiveEtaEstimate = NavBRNavigationEtaEstimate.Unavailable;
            _immersiveEtaObservedAtUtc = now;
            return _immersiveEtaEstimate;
        }

        if (now - _immersiveEtaObservedAtUtc < TimeSpan.FromMilliseconds(650d))
        {
            return _immersiveEtaEstimate;
        }

        _immersiveEtaObservedAtUtc = now;
        _immersiveEtaEstimate = _immersiveEtaEstimator.Observe(navigation, now);
        return _immersiveEtaEstimate;
    }

    private static string BuildNavigationPrimaryText(
        NavBRNavigationSnapshot navigation,
        VehicleTelemetry? telemetry)
    {
        if (!navigation.RouteAvailable)
        {
            return string.IsNullOrWhiteSpace(telemetry?.NextStopName)
                ? ImmersiveText("Rota não resolvida", "Route unavailable", "Ruta no disponible", "Route nicht verfügbar", "Itinéraire indisponible")
                : telemetry.NextStopName;
        }

        if (!navigation.IsOnRoute || navigation.Maneuver == NavBRManeuverKind.RejoinRoute)
        {
            return $"↺ {ImmersiveText("RETORNE À ROTA", "REJOIN ROUTE", "VOLVER A LA RUTA", "ZUR ROUTE", "REJOINDRE L’ITINÉRAIRE")} • {FormatNavigationDistance(navigation.OffRouteDistanceMeters)}";
        }

        if (navigation.Maneuver == NavBRManeuverKind.None ||
            navigation.DistanceToManeuverMeters is not double maneuverDistance)
        {
            return string.IsNullOrWhiteSpace(navigation.NextStopName)
                ? ImmersiveText("Siga em frente", "Continue ahead", "Continúe recto", "Geradeaus weiter", "Continuez tout droit")
                : navigation.NextStopName;
        }

        var arrow = navigation.Maneuver switch
        {
            NavBRManeuverKind.SlightLeft => "↖",
            NavBRManeuverKind.Left => "←",
            NavBRManeuverKind.SharpLeft => "↙",
            NavBRManeuverKind.SlightRight => "↗",
            NavBRManeuverKind.Right => "→",
            NavBRManeuverKind.SharpRight => "↘",
            _ => "↑"
        };
        var direction = navigation.Maneuver switch
        {
            NavBRManeuverKind.SlightLeft => ImmersiveText("ESQUERDA SUAVE", "SLIGHT LEFT", "IZQUIERDA SUAVE", "LEICHT LINKS", "LÉGÈREMENT À GAUCHE"),
            NavBRManeuverKind.Left => ImmersiveText("VIRE À ESQUERDA", "TURN LEFT", "GIRE A LA IZQUIERDA", "LINKS ABBIEGEN", "TOURNEZ À GAUCHE"),
            NavBRManeuverKind.SharpLeft => ImmersiveText("ESQUERDA FECHADA", "SHARP LEFT", "IZQUIERDA CERRADA", "SCHARF LINKS", "VIRAGE SERRÉ À GAUCHE"),
            NavBRManeuverKind.SlightRight => ImmersiveText("DIREITA SUAVE", "SLIGHT RIGHT", "DERECHA SUAVE", "LEICHT RECHTS", "LÉGÈREMENT À DROITE"),
            NavBRManeuverKind.Right => ImmersiveText("VIRE À DIREITA", "TURN RIGHT", "GIRE A LA DERECHA", "RECHTS ABBIEGEN", "TOURNEZ À DROITE"),
            NavBRManeuverKind.SharpRight => ImmersiveText("DIREITA FECHADA", "SHARP RIGHT", "DERECHA CERRADA", "SCHARF RECHTS", "VIRAGE SERRÉ À DROITE"),
            _ => ImmersiveText("SIGA", "CONTINUE", "SIGA", "WEITER", "CONTINUEZ")
        };

        return $"{arrow} {direction} • {FormatNavigationDistance(maneuverDistance)}";
    }

    private static string FormatImmersiveEta(TimeSpan eta)
    {
        if (eta.TotalHours >= 1d)
        {
            return $"{(int)eta.TotalHours}h {eta.Minutes:00}m";
        }

        if (eta.TotalMinutes >= 1d)
        {
            return $"{Math.Max(1, (int)Math.Round(eta.TotalMinutes))} min";
        }

        return $"{Math.Max(1, (int)Math.Round(eta.TotalSeconds))} s";
    }

    private void RenderOrderedStopsIntoFocus(VehicleTelemetry? telemetry, int maxStops)
    {
        if (_immersiveFocusStopsText is null ||
            telemetry is null ||
            _activeMap is null ||
            string.IsNullOrWhiteSpace(telemetry.NextStopName))
        {
            return;
        }

        var routeKey = string.Join(
            "|",
            _activeMap.DirectoryPath,
            telemetry.Line ?? string.Empty,
            telemetry.Route ?? string.Empty,
            telemetry.DestinationName ?? string.Empty);

        if (!string.Equals(routeKey, _immersiveOrderedStopsKey, StringComparison.OrdinalIgnoreCase))
        {
            _immersiveOrderedStopsKey = routeKey;
            _immersiveOrderedStops = OmsiOrderedRouteStopReader.TryRead(
                _activeMap,
                telemetry.Route,
                telemetry.Line,
                telemetry.DestinationName);
        }

        if (_immersiveOrderedStops is not { RouteResolved: true } ordered ||
            ordered.StopNames.Count == 0)
        {
            return;
        }

        var nextKey = OmsiOrderedRouteStopReader.Normalize(telemetry.NextStopName);
        if (nextKey.Length == 0)
        {
            return;
        }

        var nextIndex = -1;
        for (var index = 0; index < ordered.StopNames.Count; index++)
        {
            if (string.Equals(
                    OmsiOrderedRouteStopReader.Normalize(ordered.StopNames[index]),
                    nextKey,
                    StringComparison.Ordinal))
            {
                nextIndex = index;
                break;
            }
        }

        if (nextIndex < 0)
        {
            return;
        }

        var rows = ordered.StopNames
            .Skip(nextIndex)
            .Take(Math.Max(1, maxStops))
            .Select((name, offset) => offset == 0 ? $"● {name}" : $"○ {name}")
            .ToArray();

        if (rows.Length == 0)
        {
            return;
        }

        _immersiveFocusStopsText.Text = string.Join(Environment.NewLine, rows);
        _immersiveFocusStopsText.Visibility = Visibility.Visible;
    }

    private static string BuildFocusServiceText(VehicleTelemetry? telemetry)
    {
        if (telemetry is null)
        {
            return ImmersiveText(
                "Aguardando telemetria",
                "Waiting for telemetry",
                "Esperando telemetría",
                "Warte auf Telemetrie",
                "En attente de télémétrie");
        }

        var line = string.IsNullOrWhiteSpace(telemetry.Line) ? "—" : telemetry.Line;
        var destination = string.IsNullOrWhiteSpace(telemetry.DestinationName)
            ? ImmersiveText("Destino não informado", "Destination unavailable", "Destino no disponible", "Ziel nicht verfügbar", "Destination indisponible")
            : telemetry.DestinationName;
        return $"{line}  •  {destination}";
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

        var presetId = HudProfileCatalog.ResolvePreset(_hudSettings.DashboardPreset).Id;
        var playerLimit = presetId switch
        {
            "multiplayer-focus" => 6,
            "city-operations" => 4,
            "streamer-broadcast" => 3,
            _ => 3
        };

        var nearby = _remotePlayers.Values
            .Where(frame => IsSameImmersiveMap(local, frame.Telemetry))
            .Select(frame => new
            {
                Frame = frame,
                Distance = ImmersiveDistanceMeters(local, frame.Telemetry)
            })
            .Where(item => double.IsFinite(item.Distance))
            .OrderBy(item => item.Distance)
            .Take(playerLimit)
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

        var presetId = HudProfileCatalog.ResolvePreset(_hudSettings.DashboardPreset).Id;
        var chatLimit = presetId switch
        {
            "multiplayer-focus" => 4,
            "city-operations" => 3,
            "streamer-broadcast" => 2,
            _ => 2
        };

        var recent = _chatMessages
            .TakeLast(chatLimit)
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
        LayoutEditModeChanged -= ImmersiveOperation_LayoutEditModeChanged;
        SizeChanged -= ImmersiveOperation_SizeChanged;
        _immersiveOrderedStopsKey = null;
        _immersiveOrderedStops = null;
        _immersiveEtaEstimator.Reset();
        _immersiveEtaEstimate = NavBRNavigationEtaEstimate.Unavailable;
        _immersiveEtaObservedAtUtc = DateTimeOffset.MinValue;

        if (_immersiveOperationTimer is not null)
        {
            _immersiveOperationTimer.Stop();
            _immersiveOperationTimer.Tick -= ImmersiveOperationTimer_Tick;
            _immersiveOperationTimer = null;
        }

        Closed -= ImmersiveOperation_Closed;
    }
}
