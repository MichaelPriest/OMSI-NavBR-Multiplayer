using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private Border? _inGameInputShield;
    private Border? _inGamePanel;
    private TextBlock? _inGameSessionText;
    private TextBlock? _inGameCompanyText;
    private TextBlock? _inGameOperationsText;
    private TextBlock? _inGameDispatchText;
    private Button? _inGameAssistanceButton;
    private Button? _inGameIncidentButton;
    private Button? _inGameResolvedButton;
    private Button? _inGameDispatchAcknowledgeButton;
    private Button? _inGameDispatchResolveButton;
    private string? _inGameDispatchReportId;
    private bool _inGamePanelOpen;

    public event Action? InGamePanelOpened;
    public event Action? InGameAssistanceRequested;
    public event Action? InGameIncidentRequested;
    public event Action? InGameOperationalResolvedRequested;
    public event Action<string>? InGameDispatchAcknowledgeRequested;
    public event Action<string>? InGameDispatchResolveRequested;

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

        var headerTitle = new TextBlock
        {
            Text = InGameText(
                "NAVBR IN-GAME",
                "NAVBR IN-GAME",
                "NAVBR EN JUEGO",
                "NAVBR IM SPIEL",
                "NAVBR EN JEU"),
            Foreground = new SolidColorBrush(Color.FromRgb(105, 230, 192)),
            FontSize = 13d,
            FontWeight = FontWeights.Bold
        };

        var shortcut = new TextBlock
        {
            Text = "Ctrl+Alt+N",
            Foreground = new SolidColorBrush(Color.FromArgb(155, 255, 255, 255)),
            FontSize = 9d,
            VerticalAlignment = VerticalAlignment.Center
        };

        var closeButton = BuildInGameButton(
            "×",
            new SolidColorBrush(Color.FromArgb(120, 52, 66, 78)),
            () => CloseInGamePanel());
        closeButton.Width = 34d;
        closeButton.Margin = new Thickness(10d, 0d, 0d, 0d);

        var header = new DockPanel();
        DockPanel.SetDock(closeButton, Dock.Right);
        DockPanel.SetDock(shortcut, Dock.Right);
        header.Children.Add(closeButton);
        header.Children.Add(shortcut);
        header.Children.Add(headerTitle);

        _inGameSessionText = BuildInGameStatusText();
        _inGameCompanyText = BuildInGameStatusText();
        _inGameOperationsText = BuildInGameStatusText();
        _inGameDispatchText = BuildInGameStatusText();

        var actionGrid = new UniformGrid
        {
            Columns = 2,
            Margin = new Thickness(0d, 12d, 0d, 0d)
        };

        var chatButton = BuildInGameButton(
            InGameText("💬 CHAT", "💬 CHAT", "💬 CHAT", "💬 CHAT", "💬 CHAT"),
            new SolidColorBrush(Color.FromRgb(19, 79, 112)),
            () =>
            {
                CloseInGamePanel(restoreFocus: false);
                OpenChatInput();
            });

        var roleplayButton = BuildInGameButton(
            InGameText(
                "♙ PERSONAGEM / RP",
                "♙ CHARACTER / RP",
                "♙ PERSONAJE / RP",
                "♙ CHARAKTER / RP",
                "♙ PERSONNAGE / RP"),
            new SolidColorBrush(Color.FromRgb(31, 83, 116)),
            () =>
            {
                CloseInGamePanel();
                RoleplayButtonRequested?.Invoke();
            });

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
        actionGrid.Children.Add(roleplayButton);
        actionGrid.Children.Add(_inGameAssistanceButton);
        actionGrid.Children.Add(_inGameIncidentButton);
        actionGrid.Children.Add(_inGameResolvedButton);
        actionGrid.Children.Add(hideButton);
        actionGrid.Children.Add(_inGameDispatchAcknowledgeButton);
        actionGrid.Children.Add(_inGameDispatchResolveButton);

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(new TextBlock
        {
            Text = InGameText(
                "Controles rápidos sem sair da cabine",
                "Quick controls without leaving the cab",
                "Controles rápidos sin salir de la cabina",
                "Schnellsteuerung ohne die Kabine zu verlassen",
                "Commandes rapides sans quitter la cabine"),
            Foreground = new SolidColorBrush(Color.FromArgb(170, 216, 224, 232)),
            FontSize = 10d,
            Margin = new Thickness(0d, 3d, 0d, 10d)
        });
        body.Children.Add(_inGameSessionText);
        body.Children.Add(_inGameCompanyText);
        body.Children.Add(_inGameOperationsText);
        body.Children.Add(_inGameDispatchText);
        body.Children.Add(actionGrid);

        _inGamePanel = new Border
        {
            Width = 500d,
            Padding = new Thickness(16d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.FromArgb(246, 7, 18, 27)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(220, 55, 139, 174)),
            BorderThickness = new Thickness(1.5d),
            CornerRadius = new CornerRadius(15d),
            Child = body,
            Visibility = Visibility.Collapsed,
            Focusable = true
        };
        _inGamePanel.PreviewKeyDown += InGamePanel_PreviewKeyDown;
        Panel.SetZIndex(_inGamePanel, 1300);
        OverlayRoot.Children.Add(_inGamePanel);

        UpdateInGamePanelState(
            connected: false,
            roomId: null,
            displayName: null,
            operationalReport: null,
            companyLabel: null,
            canManageDispatch: false,
            dispatchReport: null);
    }

    public void UpdateInGamePanelState(
        bool connected,
        string? roomId,
        string? displayName,
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
                : InGameText(
                    "SESSÃO • offline",
                    "SESSION • offline",
                    "SESIÓN • sin conexión",
                    "SITZUNG • offline",
                    "SESSION • hors ligne");
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
            Height = 38d,
            Margin = new Thickness(4d),
            Padding = new Thickness(9d, 5d, 9d, 5d),
            Background = background,
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 130, 170, 194)),
            BorderThickness = new Thickness(1d),
            FontSize = 9.5d,
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
