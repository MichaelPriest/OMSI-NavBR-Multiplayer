using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class OperationsPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private readonly ObservableCollection<NativeDriverRow> _drivers = new();
    private readonly ObservableCollection<NativeReportRow> _reports = new();

    public OperationsPage()
    {
        InitializeComponent();
        DriversList.ItemsSource = _drivers;
        ReportsList.ItemsSource = _reports;
    }

    public void ApplyState(JsonElement state)
    {
        var operations = JsonState.Property(state, "operations");
        var connected = JsonState.Bool(operations, "connected");
        var roomId = JsonState.String(operations, "roomId");

        RoomText.Text = connected
            ? roomId ?? "ONLINE"
            : "OFFLINE";
        SubtitleText.Text = connected
            ? $"CCO conectado à sala {roomId ?? "NavBR"}"
            : "Entre em uma sala Multiplayer para ativar o CCO.";

        var badge = JsonState.Property(operations, "operatorBadge");
        var badgeNumber = JsonState.String(badge, "employeeNumber");
        OperatorBadgeText.Text = badgeNumber is null
            ? "—"
            : $"#{badgeNumber}";
        OperatorVerificationText.Text = badgeNumber is null
            ? "Sem crachá empresarial"
            : JsonState.Bool(operations, "operatorBadgeVerified")
                ? $"{JsonState.String(badge, "companyShortName") ?? "EMPRESA"} · VERIFICADO"
                : $"{JsonState.String(badge, "companyShortName") ?? "EMPRESA"} · NÃO VERIFICADO";

        var local = JsonState.Property(operations, "localOperation");
        LocalOperationText.Text = JsonState.IsObject(local)
            ? $"{JsonState.String(local, "mapName") ?? "—"} · {JsonState.String(local, "vehicleName") ?? "ônibus"}\n" +
              $"Linha {JsonState.String(local, "line") ?? "—"} / rota {JsonState.String(local, "route") ?? "—"} · " +
              $"{JsonState.String(local, "destination") ?? "sem destino"}"
            : "Sem telemetria local.";

        _drivers.Clear();
        foreach (var driver in JsonState.Array(operations, "drivers"))
        {
            var driverBadge = JsonState.Property(driver, "companyBadge");
            var employee = JsonState.String(driverBadge, "employeeNumber");
            var badgeText = employee is null
                ? "Sem crachá"
                : $"#{employee} · {JsonState.String(driverBadge, "companyShortName") ?? "EMPRESA"} · " +
                  (JsonState.Bool(driver, "companyBadgeVerified") ? "VERIFICADO" : "NÃO VERIFICADO");

            _drivers.Add(new NativeDriverRow(
                JsonState.String(driver, "displayName") ?? "Jogador",
                badgeText,
                $"{JsonState.String(driver, "line") ?? "—"} / {JsonState.String(driver, "route") ?? "—"}",
                JsonState.String(driver, "vehicleName") ?? "—",
                $"{JsonState.Double(driver, "speedKph") ?? 0d:0} km/h"));
        }

        _reports.Clear();
        foreach (var report in JsonState.Array(operations, "reports"))
        {
            _reports.Add(new NativeReportRow(
                JsonState.String(report, "reportId") ?? string.Empty,
                JsonState.String(report, "displayName") ?? "Jogador",
                JsonState.String(report, "severity") ?? "Info",
                JsonState.String(report, "status") ?? "Open",
                JsonState.String(report, "message") ?? "Ocorrência operacional"));
        }

        var canManage = JsonState.Bool(operations, "canManageReports");
        AcknowledgeButton.IsEnabled = canManage;
        ResolveButton.IsEnabled = canManage;
    }

    private async Task RunAsync(string command, object payload)
    {
        try
        {
            if (CommandHandler is null)
            {
                return;
            }

            await CommandHandler(command, payload);
            NoticeBar.Message = "CCO atualizado.";
            NoticeBar.Severity = InfoBarSeverity.Success;
            NoticeBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            NoticeBar.Message = ex.Message;
            NoticeBar.Severity = InfoBarSeverity.Error;
            NoticeBar.IsOpen = true;
        }
    }

    private async void Acknowledge_Click(object sender, RoutedEventArgs e)
    {
        if (ReportsList.SelectedItem is not NativeReportRow report)
        {
            return;
        }

        await RunAsync(
            "acknowledgeOperationalReport",
            new { reportId = report.ReportId });
    }

    private async void Resolve_Click(object sender, RoutedEventArgs e)
    {
        if (ReportsList.SelectedItem is not NativeReportRow report)
        {
            return;
        }

        await RunAsync(
            "resolveOperationalReport",
            new { reportId = report.ReportId });
    }
}

public sealed record NativeDriverRow(
    string DisplayName,
    string Badge,
    string Service,
    string Vehicle,
    string Speed);

public sealed record NativeReportRow(
    string ReportId,
    string DisplayName,
    string Severity,
    string Status,
    string Message);
