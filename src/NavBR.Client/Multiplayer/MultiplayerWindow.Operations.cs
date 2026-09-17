using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Operations;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private Border? _operationsCard;
    private TextBlock? _operationsStateText;
    private DispatcherTimer? _operationsRefreshTimer;
    private bool _operationsRefreshBusy;

    [ModuleInitializer]
    internal static void InitializeOperationsBootstrap()
    {
        EventManager.RegisterClassHandler(
            typeof(MultiplayerWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OperationsWindowLoaded));
    }

    private static void OperationsWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MultiplayerWindow window)
        {
            return;
        }

        _ = window.Dispatcher.BeginInvoke(
            new Action(window.InstallOperationsCard),
            DispatcherPriority.ApplicationIdle);
    }

    private void InstallOperationsCard()
    {
        if (_operationsCard is not null || StatusDetailText.Parent is not StackPanel sessionPanel)
        {
            return;
        }

        _operationsStateText = new TextBlock
        {
            Foreground = OperationsBrush(151, 177, 194),
            FontSize = 9.4d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 5d, 0d, 9d)
        };

        var buttons = new WrapPanel();
        buttons.Children.Add(BuildOperationsButton(
            OperationsText("PEDIR APOIO", "REQUEST SUPPORT", "PEDIR APOYO", "HILFE ANFORDERN", "DEMANDER DE L’AIDE"),
            OperationsBrush(21, 84, 119),
            async () => await SubmitQuickOperationalReportAsync(
                OperationalReportKind.Assistance,
                OperationalReportSeverity.Attention)));
        buttons.Children.Add(BuildOperationsButton(
            OperationsText("REPORTAR INCIDENTE", "REPORT INCIDENT", "REPORTAR INCIDENTE", "VORFALL MELDEN", "SIGNALER UN INCIDENT"),
            OperationsBrush(119, 66, 24),
            async () => await SubmitQuickOperationalReportAsync(
                OperationalReportKind.Incident,
                OperationalReportSeverity.Critical)));
        buttons.Children.Add(BuildOperationsButton(
            OperationsText("NORMALIZADO", "RESOLVED", "NORMALIZADO", "NORMALISIERT", "RÉSOLU"),
            OperationsBrush(26, 91, 65),
            ResolveMyOperationalReportsAsync));

        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = OperationsText(
                "APOIO CCO",
                "DISPATCH SUPPORT",
                "APOYO CCO",
                "LEITSTELLENHILFE",
                "ASSISTANCE PCC"),
            Foreground = OperationsBrush(112, 166, 199),
            FontSize = 8.6d,
            FontWeight = FontWeights.Bold
        });
        body.Children.Add(_operationsStateText);
        body.Children.Add(buttons);

        _operationsCard = new Border
        {
            Background = OperationsBrush(7, 24, 35),
            BorderBrush = OperationsBrush(30, 58, 76),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(9d),
            Padding = new Thickness(9d),
            Margin = new Thickness(0d, 6d, 0d, 0d),
            Child = body,
            Visibility = Visibility.Collapsed
        };
        sessionPanel.Children.Add(_operationsCard);

        _client.OperationalReportChanged += OperationsClient_ReportChanged;
        DispatcherOperationalFeed.ConfigureActions(
            () => _client.IsTrafficAuthority,
            reportId => _client.AcknowledgeOperationalReportAsync(reportId),
            reportId => _client.ResolveOperationalReportAsync(reportId));

        _operationsRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5d)
        };
        _operationsRefreshTimer.Tick += OperationsRefreshTimer_Tick;
        _operationsRefreshTimer.Start();
        Closed += OperationsWindowClosed;
        RenderOperationsState();
    }

    private Button BuildOperationsButton(string text, SolidColorBrush background, Func<Task> action)
    {
        var button = new Button
        {
            Content = text,
            Height = 30d,
            Margin = new Thickness(0d, 0d, 6d, 5d),
            Padding = new Thickness(9d, 4d, 9d, 4d),
            Background = background,
            Foreground = Brushes.White,
            BorderBrush = OperationsBrush(57, 91, 111),
            BorderThickness = new Thickness(1d),
            FontSize = 8.5d,
            FontWeight = FontWeights.Bold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += async (_, _) => await action();
        return button;
    }

    private async Task SubmitQuickOperationalReportAsync(
        OperationalReportKind kind,
        OperationalReportSeverity severity)
    {
        if (!_client.IsConnected)
        {
            StatusDetailText.Text = OperationsText(
                "Entre em uma sala antes de enviar um chamado ao CCO.",
                "Join a room before sending a dispatch request.",
                "Únete a una sala antes de enviar una solicitud al CCO.",
                "Treten Sie einem Raum bei, bevor Sie eine Meldung an die Leitstelle senden.",
                "Rejoignez une salle avant d’envoyer une demande au PCC.");
            return;
        }

        try
        {
            var message = kind == OperationalReportKind.Incident
                ? OperationsText(
                    "Incidente informado pelo motorista.",
                    "Incident reported by the driver.",
                    "Incidente informado por el conductor.",
                    "Vom Fahrer gemeldeter Vorfall.",
                    "Incident signalé par le conducteur.")
                : OperationsText(
                    "Motorista solicita apoio do CCO.",
                    "Driver requests dispatch support.",
                    "El conductor solicita apoyo del CCO.",
                    "Fahrer fordert Unterstützung der Leitstelle an.",
                    "Le conducteur demande l’assistance du PCC.");
            var report = await _client.SubmitOperationalReportAsync(
                new OperationalReportRequest(kind, severity, message));
            DispatcherOperationalFeed.Update(report);
            RenderOperationsState();
        }
        catch (Exception ex)
        {
            StatusDetailText.Text = ex.Message;
        }
    }

    private async Task ResolveMyOperationalReportsAsync()
    {
        if (!_client.IsConnected)
        {
            return;
        }

        try
        {
            var reports = await _client.ResolveMyOperationalReportsAsync();
            foreach (var report in reports)
            {
                DispatcherOperationalFeed.Update(report);
            }
            RenderOperationsState();
        }
        catch (Exception ex)
        {
            StatusDetailText.Text = ex.Message;
        }
    }

    private async void OperationsRefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_operationsRefreshBusy)
        {
            return;
        }

        if (!_client.IsConnected)
        {
            DispatcherOperationalFeed.Clear();
            RenderOperationsState();
            return;
        }

        _operationsRefreshBusy = true;
        try
        {
            var reports = await _client.RefreshOperationalReportsAsync();
            DispatcherOperationalFeed.Replace(reports);
        }
        catch
        {
            // Operational reports are optional and must not break multiplayer.
        }
        finally
        {
            _operationsRefreshBusy = false;
            RenderOperationsState();
        }
    }

    private void OperationsClient_ReportChanged(OperationalReport report)
    {
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            DispatcherOperationalFeed.Update(report);
            RenderOperationsState();
        }));
    }

    private void RenderOperationsState()
    {
        if (_operationsCard is null || _operationsStateText is null)
        {
            return;
        }

        _operationsCard.Visibility = _client.IsConnected ? Visibility.Visible : Visibility.Collapsed;
        if (!_client.IsConnected)
        {
            return;
        }

        var own = _client.CurrentOperationalReports
            .Where(report => string.Equals(report.PlayerId, _settings.PlayerId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(report => report.Status != OperationalReportStatus.Resolved)
            .ThenByDescending(report => report.UpdatedAtUtc)
            .FirstOrDefault();

        if (own is null || own.Status == OperationalReportStatus.Resolved)
        {
            _operationsStateText.Text = OperationsText(
                "Situação normal • nenhum chamado ativo.",
                "Normal operation • no active request.",
                "Operación normal • sin solicitud activa.",
                "Normalbetrieb • keine aktive Meldung.",
                "Exploitation normale • aucune demande active.");
            _operationsStateText.Foreground = OperationsBrush(82, 215, 145);
            return;
        }

        var kind = own.Kind == OperationalReportKind.Incident
            ? OperationsText("INCIDENTE", "INCIDENT", "INCIDENTE", "VORFALL", "INCIDENT")
            : OperationsText("APOIO", "SUPPORT", "APOYO", "HILFE", "ASSISTANCE");
        var status = own.Status == OperationalReportStatus.Acknowledged
            ? OperationsText("reconhecido pelo CCO", "acknowledged by dispatch", "reconocido por CCO", "von Leitstelle bestätigt", "pris en charge par le PCC")
            : OperationsText("aguardando CCO", "waiting for dispatch", "esperando CCO", "wartet auf Leitstelle", "en attente du PCC");
        _operationsStateText.Text = $"{kind} • {status}";
        _operationsStateText.Foreground = own.Kind == OperationalReportKind.Incident
            ? OperationsBrush(239, 112, 93)
            : OperationsBrush(237, 184, 75);
    }

    private void OperationsWindowClosed(object? sender, EventArgs e)
    {
        _operationsRefreshTimer?.Stop();
        _operationsRefreshTimer = null;
        _client.OperationalReportChanged -= OperationsClient_ReportChanged;
        DispatcherOperationalFeed.ClearActions();
        DispatcherOperationalFeed.Clear();
        Closed -= OperationsWindowClosed;
    }

    private static SolidColorBrush OperationsBrush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    private static string OperationsText(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
