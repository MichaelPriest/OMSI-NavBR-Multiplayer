using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private Border? _inGameInputShield;
    private Border? _inGamePanel;
    private TextBlock? _inGameSessionText;
    private TextBlock? _inGameRuntimeText;
    private TextBlock? _inGameCompanyText;
    private TextBlock? _inGameOperationsText;
    private TextBlock? _inGameDispatchText;
    private ComboBox? _inGameRoleplayCombo;
    private ComboBox? _inGameHudModeCombo;
    private ComboBox? _inGameHudSingleWidgetCombo;
    private ComboBox? _inGamePerformanceCombo;
    private CheckBox? _inGameGroundGuidanceToggle;
    private TextBlock? _inGameGroundGuidanceStatusText;
    private CheckBox? _inGameFreeRoamToggle;
    private CheckBox? _inGameMapRouteToggle;
    private CheckBox? _inGameMapRejoinToggle;
    private CheckBox? _inGameMapStopsToggle;
    private CheckBox? _inGameMapPlayersToggle;
    private CheckBox? _inGameMapTrafficToggle;
    private CheckBox? _inGameMapCongestionToggle;
    private CheckBox? _inGameHudMasterToggle;
    private CheckBox? _inGameTelematrixToggle;
    private Slider? _inGameHudZoomSlider;
    private Slider? _inGameMapOpacitySlider;
    private CheckBox? _inGameHudDashboardToggle;
    private CheckBox? _inGameHudMinimapToggle;
    private CheckBox? _inGameHudMultiplayerToggle;
    private CheckBox? _inGameHudAlertsToggle;
    private CheckBox? _inGameHudStatusToggle;
    private CheckBox? _inGameVoiceToggle;
    private CheckBox? _inGamePhysicalVehiclesToggle;
    private bool _inGameVoiceEnabled;
    private bool _inGamePhysicalVehiclesEnabled;
    private Button? _inGameRoleplayButton;
    private Button? _inGameConnectButton;
    private Button? _inGameAssistanceButton;
    private Button? _inGameIncidentButton;
    private Button? _inGameResolvedButton;
    private Button? _inGameDispatchAcknowledgeButton;
    private Button? _inGameDispatchResolveButton;
    private string? _inGameDispatchReportId;
    private string? _inGameConnectionNotice;
    private bool _inGamePanelOpen;
    private bool _inGameControlsLoading;

    private sealed record InGameChoice(string Id, string Label);

    public event Action? InGamePanelOpened;
    public event Action? InGameConnectRequested;
    public event Action? InGameAssistanceRequested;
    public event Action? InGameIncidentRequested;
    public event Action? InGameOperationalResolvedRequested;
    public event Action<string>? InGameDispatchAcknowledgeRequested;
    public event Action<string>? InGameDispatchResolveRequested;
    public event Action<string>? InGameRoleplaySelectionRequested;
    public event Action<bool>? InGameRoleplayFreeRoamChanged;
    public event Action<string>? InGamePerformanceProfileChanged;
    public event Action<bool>? InGameVoiceEnabledChanged;
    public event Action<bool>? InGamePhysicalVehiclesChanged;

    public bool IsInGamePanelOpen => _inGamePanelOpen;

    internal void InitializeInGamePanel()
    {
        if (_inGamePanel is not null)
        {
            return;
        }

        _inGameInputShield = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(18, 0, 0, 0)),
            Visibility = Visibility.Collapsed
        };
        Panel.SetZIndex(_inGameInputShield, 1290);
        OverlayRoot.Children.Add(_inGameInputShield);

        var headerTitle = new StackPanel
        {
            Orientation = Orientation.Vertical
        };
        headerTitle.Children.Add(new TextBlock
        {
            Text = "NAVBR",
            Foreground = new SolidColorBrush(Color.FromRgb(116, 235, 204)),
            FontSize = 18d,
            FontWeight = FontWeights.Black,
            FontFamily = new FontFamily("Bahnschrift")
        });
        headerTitle.Children.Add(new TextBlock
        {
            Text = InGameText(
                "CENTRAL DE CONTROLE",
                "CONTROL CENTER",
                "CENTRO DE CONTROL",
                "KONTROLLZENTRALE",
                "CENTRE DE CONTRÔLE"),
            Foreground = new SolidColorBrush(Color.FromArgb(175, 205, 220, 232)),
            FontSize = 9.5d,
            FontWeight = FontWeights.SemiBold
        });

        var closeButton = BuildInGameButton(
            "×",
            new SolidColorBrush(Color.FromArgb(120, 52, 66, 78)),
            () => CloseInGamePanel());
        closeButton.Width = 34d;
        closeButton.Margin = new Thickness(10d, 0d, 0d, 0d);

        var header = new DockPanel
        {
            Margin = new Thickness(2d, 0d, 2d, 12d),
            LastChildFill = true
        };
        DockPanel.SetDock(closeButton, Dock.Right);
        header.Children.Add(closeButton);
        header.Children.Add(headerTitle);

        _inGameSessionText = BuildInGameStatusText();
        _inGameRuntimeText = BuildInGameStatusText();
        _inGameCompanyText = BuildInGameStatusText();
        _inGameOperationsText = BuildInGameStatusText();
        _inGameDispatchText = BuildInGameStatusText();

        var actionGrid = new UniformGrid
        {
            Columns = 3,
            Margin = new Thickness(0d, 2d, 0d, 0d)
        };

        var chatButton = BuildInGameButton(
            InGameText("💬 CHAT", "💬 CHAT", "💬 CHAT", "💬 CHAT", "💬 CHAT"),
            new SolidColorBrush(Color.FromRgb(19, 79, 112)),
            () =>
            {
                CloseInGamePanel(restoreFocus: false);
                OpenChatInput();
            });

        _inGameRoleplayButton = BuildInGameButton(
            InGameText(
                "♙ PERSONAGEM / RP",
                "♙ CHARACTER / RP",
                "♙ PERSONAJE / RP",
                "♙ CHARAKTER / RP",
                "♙ PERSONNAGE / RP"),
            new SolidColorBrush(Color.FromRgb(31, 83, 116)),
            () =>
            {
                if (_roleplayHudActive)
                {
                    RoleplayButtonRequested?.Invoke();
                    return;
                }

                if (_inGameRoleplayCombo?.SelectedItem is RoleplayCharacterOption option)
                {
                    InGameRoleplaySelectionRequested?.Invoke(option.Id);
                    return;
                }

                RoleplayButtonRequested?.Invoke();
            });

        _inGameConnectButton = BuildInGameButton(
            InGameText(
                "🌐 CONECTAR ONLINE",
                "🌐 CONNECT ONLINE",
                "🌐 CONECTAR ONLINE",
                "🌐 ONLINE VERBINDEN",
                "🌐 CONNEXION EN LIGNE"),
            new SolidColorBrush(Color.FromRgb(22, 98, 118)),
            () => InGameConnectRequested?.Invoke());

        _inGameAssistanceButton = BuildInGameButton(
            InGameText(
                "PEDIR APOIO CCO",
                "REQUEST DISPATCH SUPPORT",
                "PEDIR APOYO CCO",
                "LEITSTELLENHILFE",
                "DEMANDER AIDE PCC"),
            new SolidColorBrush(Color.FromRgb(21, 84, 119)),
            () => InGameAssistanceRequested?.Invoke());

        _inGameIncidentButton = BuildInGameButton(
            InGameText(
                "REPORTAR INCIDENTE",
                "REPORT INCIDENT",
                "REPORTAR INCIDENTE",
                "VORFALL MELDEN",
                "SIGNALER INCIDENT"),
            new SolidColorBrush(Color.FromRgb(119, 66, 24)),
            () => InGameIncidentRequested?.Invoke());

        _inGameResolvedButton = BuildInGameButton(
            InGameText(
                "SITUAÇÃO NORMALIZADA",
                "MARK RESOLVED",
                "SITUACIÓN NORMALIZADA",
                "ALS GELÖST MARKIEREN",
                "SITUATION NORMALISÉE"),
            new SolidColorBrush(Color.FromRgb(26, 91, 65)),
            () => InGameOperationalResolvedRequested?.Invoke());

        _inGameDispatchAcknowledgeButton = BuildInGameButton(
            InGameText(
                "CCO • RECONHECER",
                "DISPATCH • ACKNOWLEDGE",
                "CCO • RECONOCER",
                "LEITSTELLE • BESTÄTIGEN",
                "PCC • PRENDRE EN CHARGE"),
            new SolidColorBrush(Color.FromRgb(65, 92, 135)),
            () =>
            {
                if (!string.IsNullOrWhiteSpace(_inGameDispatchReportId))
                {
                    InGameDispatchAcknowledgeRequested?.Invoke(_inGameDispatchReportId);
                }
            });
        _inGameDispatchAcknowledgeButton.Visibility = Visibility.Collapsed;

        _inGameDispatchResolveButton = BuildInGameButton(
            InGameText(
                "CCO • RESOLVER",
                "DISPATCH • RESOLVE",
                "CCO • RESOLVER",
                "LEITSTELLE • LÖSEN",
                "PCC • RÉSOUDRE"),
            new SolidColorBrush(Color.FromRgb(37, 100, 72)),
            () =>
            {
                if (!string.IsNullOrWhiteSpace(_inGameDispatchReportId))
                {
                    InGameDispatchResolveRequested?.Invoke(_inGameDispatchReportId);
                }
            });
        _inGameDispatchResolveButton.Visibility = Visibility.Collapsed;

        var hideButton = BuildInGameButton(
            InGameText(
                "VOLTAR AO JOGO",
                "BACK TO GAME",
                "VOLVER AL JUEGO",
                "ZURÜCK ZUM SPIEL",
                "RETOUR AU JEU"),
            new SolidColorBrush(Color.FromRgb(52, 66, 78)),
            () => CloseInGamePanel());

        actionGrid.Children.Add(chatButton);
        actionGrid.Children.Add(_inGameRoleplayButton);
        actionGrid.Children.Add(_inGameConnectButton);
        actionGrid.Children.Add(_inGameAssistanceButton);
        actionGrid.Children.Add(_inGameIncidentButton);
        actionGrid.Children.Add(_inGameResolvedButton);
        actionGrid.Children.Add(hideButton);
        actionGrid.Children.Add(_inGameDispatchAcknowledgeButton);
        actionGrid.Children.Add(_inGameDispatchResolveButton);

        _inGameRoleplayCombo = new ComboBox
        {
            Margin = new Thickness(0d, 10d, 0d, 0d),
            Height = 36d,
            Padding = new Thickness(8d, 4d, 8d, 4d),
            DisplayMemberPath = nameof(RoleplayCharacterOption.DisplayLabel),
            Visibility = Visibility.Collapsed
        };

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(BuildInGameStatusOverview());
        body.Children.Add(BuildInGameFeatureControls());
        body.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "AÇÕES RÁPIDAS",
                "QUICK ACTIONS",
                "ACCIONES RÁPIDAS",
                "SCHNELLAKTIONEN",
                "ACTIONS RAPIDES"),
            InGameText(
                "Chat, operação, conexão e retorno ao jogo",
                "Chat, operations, connection and return to game",
                "Chat, operaciones, conexión y regreso al juego",
                "Chat, Betrieb, Verbindung und zurück zum Spiel",
                "Chat, opérations, connexion et retour au jeu"),
            actionGrid));

        var panelScroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 690d
        };

        _inGamePanel = new Border
        {
            Width = 820d,
            MaxHeight = 780d,
            Padding = new Thickness(18d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new LinearGradientBrush(
                Color.FromArgb(250, 7, 17, 25),
                Color.FromArgb(250, 12, 27, 39),
                90d),
            BorderBrush = new SolidColorBrush(Color.FromArgb(215, 60, 139, 170)),
            BorderThickness = new Thickness(1.25d),
            CornerRadius = new CornerRadius(20d),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 28d,
                ShadowDepth = 8d,
                Opacity = 0.46d
            },
            Child = panelScroll,
            Visibility = Visibility.Collapsed,
            Focusable = true
        };
        _inGamePanel.PreviewKeyDown += InGamePanel_PreviewKeyDown;
        Panel.SetZIndex(_inGamePanel, 1300);
        OverlayRoot.Children.Add(_inGamePanel);

        RefreshInGameFeatureControls();

        UpdateInGamePanelState(
            connected: false,
            roomId: null,
            displayName: null,
            runtimeStatus: null,
            runtimeHealthy: false,
            operationalReport: null,
            companyLabel: null,
            canManageDispatch: false,
            dispatchReport: null);
    }

    internal void SetInGameConnectionNotice(string? message)
    {
        _inGameConnectionNotice =
            string.IsNullOrWhiteSpace(message)
                ? null
                : message.Trim();
    }

    public void UpdateInGamePanelState(
        bool connected,
        string? roomId,
        string? displayName,
        string? runtimeStatus,
        bool runtimeHealthy,
        OperationalReport? operationalReport,
        string? companyLabel,
        bool canManageDispatch,
        OperationalReport? dispatchReport)
    {
        if (_inGamePanel is null)
        {
            return;
        }

        if (_inGameSessionText is not null)
        {
            _inGameSessionText.Text = connected
                ? InGameText(
                    $"SESSÃO • {roomId ?? "-"} • {displayName ?? "Driver"}",
                    $"SESSION • {roomId ?? "-"} • {displayName ?? "Driver"}",
                    $"SESIÓN • {roomId ?? "-"} • {displayName ?? "Driver"}",
                    $"SITZUNG • {roomId ?? "-"} • {displayName ?? "Driver"}",
                    $"SESSION • {roomId ?? "-"} • {displayName ?? "Driver"}")
                : !string.IsNullOrWhiteSpace(_inGameConnectionNotice)
                    ? InGameText(
                        $"SESSÃO • {_inGameConnectionNotice}",
                        $"SESSION • {_inGameConnectionNotice}",
                        $"SESIÓN • {_inGameConnectionNotice}",
                        $"SITZUNG • {_inGameConnectionNotice}",
                        $"SESSION • {_inGameConnectionNotice}")
                    : InGameText(
                        "SESSÃO • offline",
                        "SESSION • offline",
                        "SESIÓN • sin conexión",
                        "SITZUNG • offline",
                        "SESSION • hors ligne");

            _inGameSessionText.Foreground = connected
                ? new SolidColorBrush(Color.FromRgb(82, 215, 145))
                : !string.IsNullOrWhiteSpace(_inGameConnectionNotice)
                    ? new SolidColorBrush(Color.FromRgb(239, 112, 93))
                    : new SolidColorBrush(Color.FromRgb(237, 184, 75));
        }

        if (_inGameRuntimeText is not null)
        {
            _inGameRuntimeText.Text = string.IsNullOrWhiteSpace(runtimeStatus)
                ? InGameText(
                    "RUNTIME • aguardando simulador",
                    "RUNTIME • waiting for simulator",
                    "RUNTIME • esperando simulador",
                    "RUNTIME • warte auf Simulator",
                    "RUNTIME • attente du simulateur")
                : runtimeStatus;
            _inGameRuntimeText.Foreground = runtimeHealthy
                ? new SolidColorBrush(Color.FromRgb(82, 215, 145))
                : new SolidColorBrush(Color.FromRgb(237, 184, 75));
        }

        if (_inGameCompanyText is not null)
        {
            _inGameCompanyText.Text = string.IsNullOrWhiteSpace(companyLabel)
                ? InGameText(
                    "EMPRESA • sem crachá ativo",
                    "COMPANY • no active badge",
                    "EMPRESA • sin credencial activa",
                    "UNTERNEHMEN • kein aktiver Ausweis",
                    "ENTREPRISE • aucun badge actif")
                : InGameText(
                    $"EMPRESA • {companyLabel}",
                    $"COMPANY • {companyLabel}",
                    $"EMPRESA • {companyLabel}",
                    $"UNTERNEHMEN • {companyLabel}",
                    $"ENTREPRISE • {companyLabel}");
        }

        if (_inGameOperationsText is not null)
        {
            _inGameOperationsText.Text = BuildInGameOperationalStatus(
                connected,
                operationalReport);
            _inGameOperationsText.Foreground = operationalReport?.Kind == OperationalReportKind.Incident &&
                                               operationalReport.Status != OperationalReportStatus.Resolved
                ? new SolidColorBrush(Color.FromRgb(239, 112, 93))
                : operationalReport is not null &&
                  operationalReport.Status != OperationalReportStatus.Resolved
                    ? new SolidColorBrush(Color.FromRgb(237, 184, 75))
                    : new SolidColorBrush(Color.FromRgb(82, 215, 145));
        }

        if (_inGameConnectButton is not null)
        {
            _inGameConnectButton.Content = connected
                ? InGameText(
                    "✓ ONLINE CONECTADO",
                    "✓ ONLINE CONNECTED",
                    "✓ ONLINE CONECTADO",
                    "✓ ONLINE VERBUNDEN",
                    "✓ EN LIGNE CONNECTÉ")
                : InGameText(
                    "🌐 CONECTAR ONLINE",
                    "🌐 CONNECT ONLINE",
                    "🌐 CONECTAR ONLINE",
                    "🌐 ONLINE VERBINDEN",
                    "🌐 CONNEXION EN LIGNE");
            _inGameConnectButton.IsEnabled = !connected;
        }

        if (_inGameAssistanceButton is not null)
        {
            _inGameAssistanceButton.IsEnabled = connected;
        }

        if (_inGameIncidentButton is not null)
        {
            _inGameIncidentButton.IsEnabled = connected;
        }

        if (_inGameResolvedButton is not null)
        {
            _inGameResolvedButton.IsEnabled =
                connected &&
                operationalReport is not null &&
                operationalReport.Status != OperationalReportStatus.Resolved;
        }

        _inGameDispatchReportId = canManageDispatch &&
                                  dispatchReport is not null &&
                                  dispatchReport.Status != OperationalReportStatus.Resolved
            ? dispatchReport.ReportId
            : null;

        if (_inGameDispatchText is not null)
        {
            _inGameDispatchText.Visibility = canManageDispatch
                ? Visibility.Visible
                : Visibility.Collapsed;
            _inGameDispatchText.Text = !canManageDispatch
                ? string.Empty
                : dispatchReport is null ||
                  dispatchReport.Status == OperationalReportStatus.Resolved
                    ? InGameText(
                        "CCO OPERADOR • nenhum chamado pendente",
                        "DISPATCH OPERATOR • no pending requests",
                        "OPERADOR CCO • sin solicitudes pendientes",
                        "LEITSTELLE • keine offenen Meldungen",
                        "OPÉRATEUR PCC • aucune demande en attente")
                    : InGameText(
                        $"CCO OPERADOR • {dispatchReport.DisplayName} • {dispatchReport.Kind} • {dispatchReport.Status}",
                        $"DISPATCH OPERATOR • {dispatchReport.DisplayName} • {dispatchReport.Kind} • {dispatchReport.Status}",
                        $"OPERADOR CCO • {dispatchReport.DisplayName} • {dispatchReport.Kind} • {dispatchReport.Status}",
                        $"LEITSTELLE • {dispatchReport.DisplayName} • {dispatchReport.Kind} • {dispatchReport.Status}",
                        $"OPÉRATEUR PCC • {dispatchReport.DisplayName} • {dispatchReport.Kind} • {dispatchReport.Status}");
        }

        if (_inGameDispatchAcknowledgeButton is not null)
        {
            _inGameDispatchAcknowledgeButton.Visibility = canManageDispatch
                ? Visibility.Visible
                : Visibility.Collapsed;
            _inGameDispatchAcknowledgeButton.IsEnabled =
                !string.IsNullOrWhiteSpace(_inGameDispatchReportId) &&
                dispatchReport?.Status == OperationalReportStatus.Open;
        }

        if (_inGameDispatchResolveButton is not null)
        {
            _inGameDispatchResolveButton.Visibility = canManageDispatch
                ? Visibility.Visible
                : Visibility.Collapsed;
            _inGameDispatchResolveButton.IsEnabled =
                !string.IsNullOrWhiteSpace(_inGameDispatchReportId);
        }
    }

    internal void UpdateInGameRoleplayOptions(
        IReadOnlyList<RoleplayCharacterOption> options,
        string? selectedId,
        bool active)
    {
        if (_inGameRoleplayCombo is null || _inGameRoleplayButton is null)
        {
            return;
        }

        _inGameRoleplayCombo.ItemsSource = options;
        _inGameRoleplayCombo.Visibility = options.Count > 0 && !active
            ? Visibility.Visible
            : Visibility.Collapsed;

        var selected = options.FirstOrDefault(option =>
            string.Equals(option.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        _inGameRoleplayCombo.SelectedItem = selected;

        _inGameRoleplayButton.Content = active
            ? InGameText(
                "♙ VOLTAR AO ÔNIBUS",
                "♙ RETURN TO BUS",
                "♙ VOLVER AL AUTOBÚS",
                "♙ ZUM BUS ZURÜCK",
                "♙ RETOUR AU BUS")
            : selected is not null
                ? InGameText(
                    $"♙ ATIVAR {selected.DisplayName}",
                    $"♙ ACTIVATE {selected.DisplayName}",
                    $"♙ ACTIVAR {selected.DisplayName}",
                    $"♙ {selected.DisplayName} AKTIVIEREN",
                    $"♙ ACTIVER {selected.DisplayName}")
                : InGameText(
                    "♙ PERSONAGEM / RP",
                    "♙ CHARACTER / RP",
                    "♙ PERSONAJE / RP",
                    "♙ CHARAKTER / RP",
                    "♙ PERSONNAGE / RP");
    }

    internal void ToggleInGamePanel()
    {
        if (_inGamePanelOpen)
        {
            CloseInGamePanel();
            return;
        }

        OpenInGamePanel();
    }

    internal void CloseInGamePanel(bool restoreFocus = true)
    {
        if (!_inGamePanelOpen)
        {
            return;
        }

        _inGamePanelOpen = false;
        if (_inGamePanel is not null)
        {
            _inGamePanel.Visibility = Visibility.Collapsed;
        }

        if (_inGameInputShield is not null)
        {
            _inGameInputShield.Visibility = Visibility.Collapsed;
        }

        if (!_chatInteractive && !_hudLayoutEditMode)
        {
            SetInteractive(false);
        }

        if (restoreFocus)
        {
            _ = Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                RestoreOmsiFocus);
        }
    }

    private void OpenInGamePanel()
    {
        if (_inGamePanel is null || !IsOmsiForeground())
        {
            return;
        }

        if (_chatInteractive)
        {
            CloseChatInput();
        }

        if (TelematrixConfigPanel.Visibility == Visibility.Visible)
        {
            CloseTelematrixConfig(save: false);
        }

        InGamePanelOpened?.Invoke();
        RefreshInGameFeatureControls();

        _inGamePanelOpen = true;
        if (_inGameInputShield is not null)
        {
            _inGameInputShield.Visibility = Visibility.Visible;
        }

        _inGamePanel.Visibility = Visibility.Visible;
        SetInteractive(true);
        _inGamePanel.Focus();
    }

    private void InGamePanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        CloseInGamePanel();
    }

    private UIElement BuildInGameFeatureControls()
    {
        var navigation = new StackPanel();
        var mapButtons = new UniformGrid
        {
            Columns = 3,
            Margin = new Thickness(0d, 2d, 0d, 4d)
        };
        mapButtons.Children.Add(BuildInGameButton(
            InGameText(
                "MAPA COMPLETO",
                "FULL MAP",
                "MAPA COMPLETO",
                "VOLLBILD-KARTE",
                "CARTE COMPLÈTE"),
            new SolidColorBrush(Color.FromRgb(18, 102, 130)),
            () => OpenFullMapOverlay()));
        mapButtons.Children.Add(BuildInGameButton(
            "MAP −",
            new SolidColorBrush(Color.FromRgb(31, 58, 76)),
            () => AdjustFullMapZoom(-0.20d)));
        mapButtons.Children.Add(BuildInGameButton(
            "MAP +",
            new SolidColorBrush(Color.FromRgb(31, 58, 76)),
            () => AdjustFullMapZoom(0.20d)));

        _inGameGroundGuidanceToggle = BuildInGameCheckBox(
            InGameText(
                "Setas 3D no chão (rota)",
                "3D ground route arrows",
                "Flechas 3D en el suelo",
                "3D-Routenpfeile am Boden",
                "Flèches 3D au sol"),
            value => SaveInGameSettings(settings => settings with
            {
                GroundRouteGuidanceEnabled = value
            }));
        _inGameMapRouteToggle = BuildInGameCheckBox(
            InGameText("Rota", "Route", "Ruta", "Route", "Itinéraire"),
            value => SaveInGameSettings(settings => settings with
            {
                MapShowRoute = value
            }));
        _inGameMapRejoinToggle = BuildInGameCheckBox(
            InGameText(
                "Retorno à rota",
                "Rejoin",
                "Retorno",
                "Rückkehr",
                "Rejoindre"),
            value => SaveInGameSettings(settings => settings with
            {
                MapShowRejoin = value
            }));
        _inGameMapStopsToggle = BuildInGameCheckBox(
            InGameText(
                "Paradas",
                "Stops",
                "Paradas",
                "Haltestellen",
                "Arrêts"),
            value => SaveInGameSettings(settings => settings with
            {
                MapShowStops = value
            }));
        _inGameMapPlayersToggle = BuildInGameCheckBox(
            InGameText(
                "Jogadores",
                "Players",
                "Jugadores",
                "Spieler",
                "Joueurs"),
            value => SaveInGameSettings(settings => settings with
            {
                MapShowPlayers = value
            }));
        _inGameMapTrafficToggle = BuildInGameCheckBox(
            InGameText(
                "IA / tráfego",
                "AI / traffic",
                "IA / tráfico",
                "KI / Verkehr",
                "IA / trafic"),
            value => SaveInGameSettings(settings => settings with
            {
                MapShowTraffic = value
            }));
        _inGameMapCongestionToggle = BuildInGameCheckBox(
            InGameText(
                "Congestionamento",
                "Congestion",
                "Congestión",
                "Verkehrslage",
                "Congestion"),
            value => SaveInGameSettings(settings => settings with
            {
                MapShowCongestion = value
            }));

        var mapLayers = new WrapPanel
        {
            Margin = new Thickness(0d, 3d, 0d, 3d)
        };
        mapLayers.Children.Add(_inGameGroundGuidanceToggle);
        mapLayers.Children.Add(_inGameMapRouteToggle);
        mapLayers.Children.Add(_inGameMapRejoinToggle);
        mapLayers.Children.Add(_inGameMapStopsToggle);
        mapLayers.Children.Add(_inGameMapPlayersToggle);
        mapLayers.Children.Add(_inGameMapTrafficToggle);
        mapLayers.Children.Add(_inGameMapCongestionToggle);

        _inGameGroundGuidanceStatusText = new TextBlock
        {
            Text = $"SETAS 3D • {GroundRouteGuidanceStatus}",
            Foreground = new SolidColorBrush(Color.FromRgb(116, 235, 204)),
            FontSize = 9.5d,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(6d, 5d, 6d, 2d),
            TextWrapping = TextWrapping.Wrap
        };

        navigation.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "MAPA E NAVEGAÇÃO",
                "MAP & NAVIGATION",
                "MAPA Y NAVEGACIÓN",
                "KARTE & NAVIGATION",
                "CARTE & NAVIGATION"),
            InGameText(
                "Controle de rota, camadas e visão completa",
                "Route, layers and full-map controls",
                "Ruta, capas y mapa completo",
                "Route, Ebenen und Vollbildkarte",
                "Itinéraire, calques et carte complète"),
            mapButtons));
        navigation.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "CAMADAS ATIVAS",
                "ACTIVE LAYERS",
                "CAPAS ACTIVAS",
                "AKTIVE EBENEN",
                "CALQUES ACTIFS"),
            InGameText(
                "Escolha o que aparece no GPS e no mapa",
                "Choose what appears on GPS and map",
                "Elija lo que aparece en GPS y mapa",
                "Wählen Sie GPS- und Kartenebenen",
                "Choisissez les éléments GPS et carte"),
            mapLayers));
        navigation.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "ORIENTAÇÃO 3D",
                "3D GUIDANCE",
                "GUÍA 3D",
                "3D-FÜHRUNG",
                "GUIDAGE 3D"),
            InGameText(
                "Diagnóstico em tempo real das setas no chão",
                "Live diagnostics for ground-route arrows",
                "Diagnóstico en vivo de las flechas",
                "Live-Diagnose der Bodenpfeile",
                "Diagnostic en direct des flèches au sol"),
            _inGameGroundGuidanceStatusText));

        var hud = new StackPanel();
        _inGameHudMasterToggle = BuildInGameCheckBox(
            InGameText(
                "HUD ativo",
                "HUD enabled",
                "HUD activo",
                "HUD aktiv",
                "HUD actif"),
            value => SaveInGameSettings(settings => settings with
            {
                HudEnabled = value
            }));
        _inGameTelematrixToggle = BuildInGameCheckBox(
            "TeleMatrix",
            value => SaveInGameSettings(settings => settings with
            {
                TelematrixWidgetEnabled = value
            }));

        var hudGlobal = new WrapPanel();
        hudGlobal.Children.Add(_inGameHudMasterToggle);
        hudGlobal.Children.Add(_inGameTelematrixToggle);

        _inGameHudZoomSlider = BuildInGameSliderRow(
            InGameText(
                "Zoom GPS",
                "GPS zoom",
                "Zoom GPS",
                "GPS-Zoom",
                "Zoom GPS"),
            0.65d,
            10d,
            0.05d,
            value => SaveInGameSettings(settings => settings with
            {
                HudZoom = value
            }));
        _inGameMapOpacitySlider = BuildInGameSliderRow(
            InGameText(
                "Opacidade do mapa",
                "Map opacity",
                "Opacidad del mapa",
                "Karten-Deckkraft",
                "Opacité carte"),
            0.30d,
            0.90d,
            0.02d,
            value => SaveInGameSettings(settings => settings with
            {
                HudMapOpacity = value
            }));

        var visualTuning = new StackPanel();
        visualTuning.Children.Add(BuildInGameSliderLabel(
            InGameText(
                "Zoom do GPS",
                "GPS zoom",
                "Zoom GPS",
                "GPS-Zoom",
                "Zoom GPS")));
        visualTuning.Children.Add(_inGameHudZoomSlider);
        visualTuning.Children.Add(BuildInGameSliderLabel(
            InGameText(
                "Opacidade do mapa",
                "Map opacity",
                "Opacidad del mapa",
                "Karten-Deckkraft",
                "Opacité carte")));
        visualTuning.Children.Add(_inGameMapOpacitySlider);

        _inGameHudModeCombo = BuildInGameCombo();
        _inGameHudModeCombo.ItemsSource = new[]
        {
            new InGameChoice(
                "all",
                InGameText(
                    "Todos / padrão",
                    "All / default",
                    "Todos / predeterminado",
                    "Alle / Standard",
                    "Tous / défaut")),
            new InGameChoice(
                "single",
                InGameText(
                    "Somente um HUD",
                    "Single HUD",
                    "Un solo HUD",
                    "Ein HUD",
                    "Un seul HUD")),
            new InGameChoice(
                "selected",
                InGameText(
                    "HUDs selecionados",
                    "Selected HUDs",
                    "HUD seleccionados",
                    "Ausgewählte HUDs",
                    "HUD sélectionnés"))
        };
        _inGameHudModeCombo.DisplayMemberPath = nameof(InGameChoice.Label);
        _inGameHudModeCombo.SelectedValuePath = nameof(InGameChoice.Id);
        _inGameHudModeCombo.SelectionChanged += (_, _) =>
        {
            if (_inGameControlsLoading ||
                _inGameHudModeCombo.SelectedValue is not string mode)
            {
                return;
            }

            SaveInGameSettings(settings => settings with
            {
                HudSelectionMode = mode
            });
        };

        _inGameHudSingleWidgetCombo = BuildInGameCombo();
        _inGameHudSingleWidgetCombo.ItemsSource = new[]
        {
            new InGameChoice(
                "dashboard",
                InGameText(
                    "Painel principal",
                    "Main dashboard",
                    "Panel principal",
                    "Hauptpanel",
                    "Panneau principal")),
            new InGameChoice(
                "minimap",
                InGameText(
                    "Mapa / GPS",
                    "Map / GPS",
                    "Mapa / GPS",
                    "Karte / GPS",
                    "Carte / GPS")),
            new InGameChoice("multiplayer", "Multiplayer"),
            new InGameChoice(
                "alerts",
                InGameText(
                    "Alertas",
                    "Alerts",
                    "Alertas",
                    "Warnungen",
                    "Alertes")),
            new InGameChoice(
                "status",
                InGameText(
                    "Status do veículo",
                    "Vehicle status",
                    "Estado del vehículo",
                    "Fahrzeugstatus",
                    "État véhicule"))
        };
        _inGameHudSingleWidgetCombo.DisplayMemberPath = nameof(InGameChoice.Label);
        _inGameHudSingleWidgetCombo.SelectedValuePath = nameof(InGameChoice.Id);
        _inGameHudSingleWidgetCombo.SelectionChanged += (_, _) =>
        {
            if (_inGameControlsLoading ||
                _inGameHudSingleWidgetCombo.SelectedValue is not string widget)
            {
                return;
            }

            SaveInGameSettings(settings => settings with
            {
                HudSingleWidget = widget
            });
        };

        var hudSelectors = new Grid();
        hudSelectors.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1d, GridUnitType.Star)
        });
        hudSelectors.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1d, GridUnitType.Star)
        });
        Grid.SetColumn(_inGameHudModeCombo, 0);
        Grid.SetColumn(_inGameHudSingleWidgetCombo, 1);
        hudSelectors.Children.Add(_inGameHudModeCombo);
        hudSelectors.Children.Add(_inGameHudSingleWidgetCombo);

        _inGameHudDashboardToggle = BuildInGameCheckBox(
            InGameText("Painel", "Dashboard", "Panel", "Panel", "Panneau"),
            value => SaveInGameSettings(settings => settings with
            {
                DashboardEnabled = value
            }));
        _inGameHudMinimapToggle = BuildInGameCheckBox(
            InGameText("Mapa", "Map", "Mapa", "Karte", "Carte"),
            value => SaveInGameSettings(settings => settings with
            {
                DashboardShowMinimap = value
            }));
        _inGameHudMultiplayerToggle = BuildInGameCheckBox(
            "Multiplayer",
            value => SaveInGameSettings(settings => settings with
            {
                DashboardShowMultiplayer = value
            }));
        _inGameHudAlertsToggle = BuildInGameCheckBox(
            InGameText("Alertas", "Alerts", "Alertas", "Warnungen", "Alertes"),
            value => SaveInGameSettings(settings => settings with
            {
                DashboardShowAlerts = value
            }));
        _inGameHudStatusToggle = BuildInGameCheckBox(
            InGameText("Status", "Status", "Estado", "Status", "État"),
            value => SaveInGameSettings(settings => settings with
            {
                DashboardShowSideIndicators = value
            }));

        var hudModules = new WrapPanel();
        hudModules.Children.Add(_inGameHudDashboardToggle);
        hudModules.Children.Add(_inGameHudMinimapToggle);
        hudModules.Children.Add(_inGameHudMultiplayerToggle);
        hudModules.Children.Add(_inGameHudAlertsToggle);
        hudModules.Children.Add(_inGameHudStatusToggle);

        hud.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "HUD PRINCIPAL",
                "MAIN HUD",
                "HUD PRINCIPAL",
                "HAUPT-HUD",
                "HUD PRINCIPAL"),
            InGameText(
                "Ative o HUD e os módulos principais",
                "Enable HUD and primary modules",
                "Active HUD y módulos principales",
                "HUD und Hauptmodule aktivieren",
                "Activez le HUD et les modules"),
            hudGlobal));
        hud.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "APARÊNCIA",
                "APPEARANCE",
                "APARIENCIA",
                "DARSTELLUNG",
                "APPARENCE"),
            InGameText(
                "Ajustes rápidos de escala e transparência",
                "Quick scale and transparency controls",
                "Ajustes rápidos de escala y transparencia",
                "Schnelle Skalierungs- und Transparenzsteuerung",
                "Réglages rapides d’échelle et transparence"),
            visualTuning));
        hud.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "COMPOSIÇÃO",
                "COMPOSITION",
                "COMPOSICIÓN",
                "ZUSAMMENSTELLUNG",
                "COMPOSITION"),
            InGameText(
                "Escolha um HUD, todos ou somente os selecionados",
                "Choose one HUD, all, or only selected modules",
                "Elija un HUD, todos o los seleccionados",
                "Ein HUD, alle oder ausgewählte Module",
                "Un HUD, tous ou seulement les modules choisis"),
            hudSelectors));
        hud.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "MÓDULOS",
                "MODULES",
                "MÓDULOS",
                "MODULE",
                "MODULES"),
            InGameText(
                "Componentes que podem aparecer simultaneamente",
                "Components that may appear simultaneously",
                "Componentes que pueden mostrarse juntos",
                "Komponenten, die gleichzeitig angezeigt werden",
                "Composants pouvant être affichés ensemble"),
            hudModules));

        var onlineRp = new StackPanel();
        _inGameVoiceToggle = BuildInGameCheckBox(
            InGameText("Voz", "Voice", "Voz", "Sprache", "Voix"),
            value =>
            {
                _inGameVoiceEnabled = value;
                InGameVoiceEnabledChanged?.Invoke(value);
            });
        _inGamePhysicalVehiclesToggle = BuildInGameCheckBox(
            InGameText(
                "Ônibus físicos dos jogadores",
                "Physical player buses",
                "Autobuses físicos de jugadores",
                "Physische Spielerbusse",
                "Bus physiques des joueurs"),
            value =>
            {
                _inGamePhysicalVehiclesEnabled = value;
                InGamePhysicalVehiclesChanged?.Invoke(value);
            });

        var onlineControls = new WrapPanel();
        onlineControls.Children.Add(_inGameVoiceToggle);
        onlineControls.Children.Add(_inGamePhysicalVehiclesToggle);

        _inGameFreeRoamToggle = BuildInGameCheckBox(
            InGameText(
                "Modo livre (sem limite de distância do ônibus)",
                "Free roam (no bus distance limit)",
                "Modo libre (sin límite de distancia)",
                "Freies Bewegen (ohne Bus-Distanzlimit)",
                "Mode libre (sans limite de distance)"),
            value =>
            {
                SaveInGameSettings(settings => settings with
                {
                    RoleplayFreeRoamEnabled = value
                });
                InGameRoleplayFreeRoamChanged?.Invoke(value);
            });

        var roleplayControls = new StackPanel();
        if (_inGameRoleplayCombo is not null)
        {
            roleplayControls.Children.Add(_inGameRoleplayCombo);
        }
        roleplayControls.Children.Add(_inGameFreeRoamToggle);

        onlineRp.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "MULTIPLAYER",
                "MULTIPLAYER",
                "MULTIPLAYER",
                "MULTIPLAYER",
                "MULTIJOUEUR"),
            InGameText(
                "Voz e presença física dos demais motoristas",
                "Voice and physical presence of other drivers",
                "Voz y presencia física de otros conductores",
                "Sprache und physische Präsenz anderer Fahrer",
                "Voix et présence physique des autres conducteurs"),
            onlineControls));
        onlineRp.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "PERSONAGEM / RP",
                "CHARACTER / RP",
                "PERSONAJE / RP",
                "CHARAKTER / RP",
                "PERSONNAGE / RP"),
            InGameText(
                "Seleção do personagem e movimentação livre",
                "Character selection and free-roam controls",
                "Selección de personaje y modo libre",
                "Charakterauswahl und freie Bewegung",
                "Sélection du personnage et déplacement libre"),
            roleplayControls));

        var system = new StackPanel();
        _inGamePerformanceCombo = BuildInGameCombo();
        _inGamePerformanceCombo.ItemsSource = new[]
        {
            new InGameChoice(
                "auto",
                InGameText(
                    "Automático",
                    "Automatic",
                    "Automático",
                    "Automatisch",
                    "Automatique")),
            new InGameChoice(
                "stability",
                InGameText(
                    "Estabilidade",
                    "Stability",
                    "Estabilidad",
                    "Stabilität",
                    "Stabilité")),
            new InGameChoice("multiplayer", "Multiplayer"),
            new InGameChoice(
                "quality",
                InGameText(
                    "Qualidade",
                    "Quality",
                    "Calidad",
                    "Qualität",
                    "Qualité")),
            new InGameChoice(
                "diagnostics",
                InGameText(
                    "Diagnóstico",
                    "Diagnostics",
                    "Diagnóstico",
                    "Diagnose",
                    "Diagnostic"))
        };
        _inGamePerformanceCombo.DisplayMemberPath = nameof(InGameChoice.Label);
        _inGamePerformanceCombo.SelectedValuePath = nameof(InGameChoice.Id);
        _inGamePerformanceCombo.SelectionChanged += (_, _) =>
        {
            if (_inGameControlsLoading ||
                _inGamePerformanceCombo.SelectedValue is not string profile)
            {
                return;
            }

            SaveInGameSettings(settings => settings with
            {
                PerformanceProfile = profile
            });
            InGamePerformanceProfileChanged?.Invoke(profile);
        };

        system.Children.Add(BuildInGameSurfaceCard(
            InGameText(
                "OTIMIZADOR",
                "OPTIMIZER",
                "OPTIMIZADOR",
                "OPTIMIERER",
                "OPTIMISEUR"),
            InGameText(
                "Perfil de desempenho aplicado ao runtime do NavBR",
                "Performance profile applied to the NavBR runtime",
                "Perfil de rendimiento aplicado al runtime de NavBR",
                "Leistungsprofil für die NavBR-Laufzeit",
                "Profil de performance appliqué au runtime NavBR"),
            _inGamePerformanceCombo));

        var tabs = new TabControl
        {
            Margin = new Thickness(0d, 12d, 0d, 8d),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0d),
            Foreground = Brushes.White
        };
        tabs.Items.Add(BuildInGameTab(
            InGameText(
                "Navegação",
                "Navigation",
                "Navegación",
                "Navigation",
                "Navigation"),
            navigation));
        tabs.Items.Add(BuildInGameTab("HUD", hud));
        tabs.Items.Add(BuildInGameTab(
            InGameText(
                "Online / RP",
                "Online / RP",
                "Online / RP",
                "Online / RP",
                "En ligne / RP"),
            onlineRp));
        tabs.Items.Add(BuildInGameTab(
            InGameText(
                "Sistema",
                "System",
                "Sistema",
                "System",
                "Système"),
            system));

        return tabs;
    }

    private UIElement BuildInGameStatusOverview()
    {
        var grid = new UniformGrid
        {
            Columns = 2,
            Margin = new Thickness(0d, 0d, 0d, 6d)
        };

        grid.Children.Add(BuildInGameStatusCard(
            InGameText(
                "SESSÃO",
                "SESSION",
                "SESIÓN",
                "SITZUNG",
                "SESSION"),
            _inGameSessionText!));
        grid.Children.Add(BuildInGameStatusCard(
            "RUNTIME",
            _inGameRuntimeText!));
        grid.Children.Add(BuildInGameStatusCard(
            InGameText(
                "EMPRESA",
                "COMPANY",
                "EMPRESA",
                "UNTERNEHMEN",
                "ENTREPRISE"),
            _inGameCompanyText!));
        grid.Children.Add(BuildInGameStatusCard(
            "CCO",
            _inGameOperationsText!));

        var root = new StackPanel();
        root.Children.Add(grid);
        root.Children.Add(_inGameDispatchText!);
        if (_inGameDispatchText is not null)
        {
            _inGameDispatchText.Margin =
                new Thickness(8d, 2d, 8d, 5d);
        }

        return root;
    }

    private static Border BuildInGameStatusCard(
        string label,
        TextBlock value)
    {
        value.Margin = new Thickness(0d, 4d, 0d, 0d);
        value.FontSize = 10d;

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(
                Color.FromArgb(150, 178, 198, 214)),
            FontSize = 8d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(value);

        return new Border
        {
            Margin = new Thickness(4d),
            Padding = new Thickness(12d, 9d, 12d, 9d),
            Background = new SolidColorBrush(
                Color.FromArgb(145, 14, 30, 42)),
            BorderBrush = new SolidColorBrush(
                Color.FromArgb(80, 116, 235, 204)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(10d),
            Child = stack
        };
    }

    private static Border BuildInGameSurfaceCard(
        string title,
        string subtitle,
        UIElement content)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(Color.FromRgb(116, 235, 204)),
            FontSize = 10.5d,
            FontWeight = FontWeights.Bold
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = new SolidColorBrush(
                Color.FromArgb(155, 190, 207, 220)),
            FontSize = 8.8d,
            Margin = new Thickness(0d, 2d, 0d, 7d),
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(content);

        return new Border
        {
            Margin = new Thickness(4d, 4d, 4d, 7d),
            Padding = new Thickness(12d),
            Background = new SolidColorBrush(
                Color.FromArgb(158, 11, 27, 39)),
            BorderBrush = new SolidColorBrush(
                Color.FromArgb(80, 84, 138, 164)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(12d),
            Child = stack
        };
    }

    private static TabItem BuildInGameTab(
        string title,
        UIElement content)
    {
        var header = new Border
        {
            Padding = new Thickness(13d, 8d, 13d, 8d),
            Margin = new Thickness(1d, 0d, 1d, 0d),
            Background = new SolidColorBrush(
                Color.FromArgb(175, 20, 42, 56)),
            CornerRadius = new CornerRadius(8d, 8d, 0d, 0d),
            Child = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 9.5d,
                FontWeight = FontWeights.SemiBold
            }
        };

        return new TabItem
        {
            Header = header,
            Content = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Disabled,
                MaxHeight = 420d
            },
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Foreground = Brushes.White,
            Padding = new Thickness(0d)
        };
    }

    private static TextBlock BuildInGameSliderLabel(
        string text) =>
        new()
        {
            Text = text,
            Foreground = new SolidColorBrush(
                Color.FromArgb(180, 215, 225, 234)),
            FontSize = 8.8d,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(7d, 3d, 7d, 0d)
        };

    private static TextBlock BuildInGameSectionTitle(string text) =>
        new()
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(116, 235, 204)),
            FontSize = 10d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(5d, 10d, 5d, 5d)
        };

    private static ComboBox BuildInGameCombo() =>
        new()
        {
            Height = 36d,
            Margin = new Thickness(4d),
            Padding = new Thickness(10d, 4d, 10d, 4d),
            Background = new SolidColorBrush(Color.FromRgb(18, 38, 52)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(
                Color.FromArgb(120, 84, 138, 164)),
            BorderThickness = new Thickness(1d)
        };

    private Slider BuildInGameSliderRow(
        string tooltip,
        double minimum,
        double maximum,
        double tickFrequency,
        Action<double> changed)
    {
        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            TickFrequency = tickFrequency,
            IsSnapToTickEnabled = false,
            Margin = new Thickness(7d, 2d, 10d, 4d),
            ToolTip = tooltip,
            Width = 330d
        };
        slider.ValueChanged += (_, _) =>
        {
            if (!_inGameControlsLoading)
            {
                changed(slider.Value);
            }
        };
        return slider;
    }

    private CheckBox BuildInGameCheckBox(
        string text,
        Action<bool> changed)
    {
        var check = new CheckBox
        {
            Content = text,
            Foreground = Brushes.White,
            Margin = new Thickness(7d, 5d, 12d, 5d),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 9.7d,
            FontWeight = FontWeights.Medium
        };
        check.Click += (_, _) =>
        {
            if (!_inGameControlsLoading)
            {
                changed(check.IsChecked == true);
            }
        };
        return check;
    }

    private void SaveInGameSettings(
        Func<MultiplayerSettings, MultiplayerSettings> update)
    {
        if (_inGameControlsLoading)
        {
            return;
        }

        var current = MultiplayerSettingsStore.Load();
        var updated = update(current);
        MultiplayerSettingsStore.Save(updated);
        _hudSettings = updated;
        ApplyModularWidgetSettings(updated);
        ApplyTelematrixSettings(updated);
        RenderEnhancedMiniMap();
        RenderFullMapOverlay();
        RenderGroundRouteGuidance();
        RefreshInGameFeatureControls();
    }

    private void RefreshInGameFeatureControls()
    {
        _inGameControlsLoading = true;
        try
        {
            var settings = MultiplayerSettingsStore.Load();
            if (_inGameVoiceToggle is not null) _inGameVoiceToggle.IsChecked = _inGameVoiceEnabled;
            if (_inGamePhysicalVehiclesToggle is not null) _inGamePhysicalVehiclesToggle.IsChecked = _inGamePhysicalVehiclesEnabled;
            if (_inGameGroundGuidanceToggle is not null) _inGameGroundGuidanceToggle.IsChecked = settings.GroundRouteGuidanceEnabled;
            if (_inGameGroundGuidanceStatusText is not null)
            {
                _inGameGroundGuidanceStatusText.Text =
                    $"SETAS 3D • {GroundRouteGuidanceStatus}";
            }
            if (_inGameFreeRoamToggle is not null) _inGameFreeRoamToggle.IsChecked = settings.RoleplayFreeRoamEnabled;
            if (_inGameMapRouteToggle is not null) _inGameMapRouteToggle.IsChecked = settings.MapShowRoute;
            if (_inGameMapRejoinToggle is not null) _inGameMapRejoinToggle.IsChecked = settings.MapShowRejoin;
            if (_inGameMapStopsToggle is not null) _inGameMapStopsToggle.IsChecked = settings.MapShowStops;
            if (_inGameMapPlayersToggle is not null) _inGameMapPlayersToggle.IsChecked = settings.MapShowPlayers;
            if (_inGameMapTrafficToggle is not null) _inGameMapTrafficToggle.IsChecked = settings.MapShowTraffic;
            if (_inGameMapCongestionToggle is not null) _inGameMapCongestionToggle.IsChecked = settings.MapShowCongestion;
            if (_inGameHudMasterToggle is not null) _inGameHudMasterToggle.IsChecked = settings.HudEnabled;
            if (_inGameTelematrixToggle is not null) _inGameTelematrixToggle.IsChecked = settings.TelematrixWidgetEnabled;
            if (_inGameHudZoomSlider is not null) _inGameHudZoomSlider.Value = settings.HudZoom;
            if (_inGameMapOpacitySlider is not null) _inGameMapOpacitySlider.Value = settings.HudMapOpacity;
            if (_inGameHudDashboardToggle is not null) _inGameHudDashboardToggle.IsChecked = settings.DashboardEnabled;
            if (_inGameHudMinimapToggle is not null) _inGameHudMinimapToggle.IsChecked = settings.DashboardShowMinimap;
            if (_inGameHudMultiplayerToggle is not null) _inGameHudMultiplayerToggle.IsChecked = settings.DashboardShowMultiplayer;
            if (_inGameHudAlertsToggle is not null) _inGameHudAlertsToggle.IsChecked = settings.DashboardShowAlerts;
            if (_inGameHudStatusToggle is not null) _inGameHudStatusToggle.IsChecked = settings.DashboardShowSideIndicators;
            if (_inGameHudModeCombo is not null) _inGameHudModeCombo.SelectedValue = settings.HudSelectionMode;
            if (_inGameHudSingleWidgetCombo is not null)
            {
                _inGameHudSingleWidgetCombo.SelectedValue = settings.HudSingleWidget;
                _inGameHudSingleWidgetCombo.IsEnabled =
                    string.Equals(settings.HudSelectionMode, "single", StringComparison.OrdinalIgnoreCase);
            }
            if (_inGamePerformanceCombo is not null) _inGamePerformanceCombo.SelectedValue = settings.PerformanceProfile;
        }
        finally
        {
            _inGameControlsLoading = false;
        }
    }

    internal void UpdateInGameOnlineFeatureState(
        bool voiceEnabled,
        bool physicalVehiclesEnabled)
    {
        _inGameVoiceEnabled = voiceEnabled;
        _inGamePhysicalVehiclesEnabled = physicalVehiclesEnabled;
        if (_inGamePanelOpen)
        {
            RefreshInGameFeatureControls();
        }
    }

    internal void UpdateInGameGroundGuidanceStatus(string status)
    {
        if (_inGameGroundGuidanceStatusText is null)
        {
            return;
        }

        _inGameGroundGuidanceStatusText.Text =
            $"SETAS 3D • {status}";
    }

    private static TextBlock BuildInGameStatusText() =>
        new()
        {
            Foreground = new SolidColorBrush(Color.FromArgb(210, 216, 224, 232)),
            FontSize = 10d,
            Margin = new Thickness(0d, 3d, 0d, 0d),
            TextWrapping = TextWrapping.Wrap
        };

    private static Button BuildInGameButton(
        string text,
        Brush background,
        Action action)
    {
        var button = new Button
        {
            Content = text,
            Height = 40d,
            Margin = new Thickness(4d),
            Padding = new Thickness(10d, 6d, 10d, 6d),
            Background = background,
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(95, 143, 184, 204)),
            BorderThickness = new Thickness(1d),
            FontSize = 9.4d,
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static string BuildInGameOperationalStatus(
        bool connected,
        OperationalReport? report)
    {
        if (!connected)
        {
            return InGameText(
                "CCO • entre em uma sala para usar suporte operacional",
                "DISPATCH • join a room to use operational support",
                "CCO • entra en una sala para usar soporte operativo",
                "LEITSTELLE • Raum beitreten für Betriebshilfe",
                "PCC • rejoignez une salle pour l’assistance");
        }

        if (report is null || report.Status == OperationalReportStatus.Resolved)
        {
            return InGameText(
                "CCO • situação normal • nenhum chamado ativo",
                "DISPATCH • normal operation • no active request",
                "CCO • operación normal • sin solicitud activa",
                "LEITSTELLE • Normalbetrieb • keine aktive Meldung",
                "PCC • exploitation normale • aucune demande active");
        }

        var kind = report.Kind == OperationalReportKind.Incident
            ? InGameText("INCIDENTE", "INCIDENT", "INCIDENTE", "VORFALL", "INCIDENT")
            : InGameText("APOIO", "SUPPORT", "APOYO", "HILFE", "ASSISTANCE");
        var status = report.Status == OperationalReportStatus.Acknowledged
            ? InGameText(
                "reconhecido pelo CCO",
                "acknowledged by dispatch",
                "reconocido por CCO",
                "von Leitstelle bestätigt",
                "pris en charge par le PCC")
            : InGameText(
                "aguardando CCO",
                "waiting for dispatch",
                "esperando CCO",
                "wartet auf Leitstelle",
                "en attente du PCC");
        return $"CCO • {kind} • {status}";
    }

    private static string InGameText(
        string pt,
        string en,
        string es,
        string de,
        string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
