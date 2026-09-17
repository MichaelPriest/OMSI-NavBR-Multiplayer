using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public sealed partial class MultiplayerClientService
{
    private readonly ConcurrentDictionary<string, OperationalReport> _operationalReports =
        new(StringComparer.OrdinalIgnoreCase);
    private HubConnection? _operationsHandlerConnection;
    private IDisposable? _operationsSubscription;

    public event Action<OperationalReport>? OperationalReportChanged;

    public IReadOnlyList<OperationalReport> CurrentOperationalReports =>
        _operationalReports.Values
            .OrderByDescending(report => report.Status != OperationalReportStatus.Resolved)
            .ThenByDescending(report => report.Severity)
            .ThenByDescending(report => report.UpdatedAtUtc)
            .ToArray();

    public async Task<IReadOnlyList<OperationalReport>> RefreshOperationalReportsAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = _connection;
        if (connection is null || connection.State != HubConnectionState.Connected)
        {
            _operationalReports.Clear();
            return Array.Empty<OperationalReport>();
        }

        EnsureOperationsHandler(connection);
        var reports = await connection.InvokeAsync<IReadOnlyList<OperationalReport>>(
            "GetOperationalReports",
            cancellationToken);
        _operationalReports.Clear();
        foreach (var report in reports)
        {
            _operationalReports[report.ReportId] = report;
        }
        return CurrentOperationalReports;
    }

    public async Task<OperationalReport> SubmitOperationalReportAsync(
        OperationalReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var connection = RequireConnectedConnection();
        EnsureOperationsHandler(connection);
        var report = await connection.InvokeAsync<OperationalReport>(
            "SubmitOperationalReport",
            request,
            cancellationToken);
        ApplyOperationalReport(report);
        return report;
    }

    public async Task<OperationalReport?> AcknowledgeOperationalReportAsync(
        string reportId,
        CancellationToken cancellationToken = default)
    {
        var connection = RequireConnectedConnection();
        EnsureOperationsHandler(connection);
        var report = await connection.InvokeAsync<OperationalReport?>(
            "AcknowledgeOperationalReport",
            reportId,
            cancellationToken);
        if (report is not null)
        {
            ApplyOperationalReport(report);
        }
        return report;
    }

    public async Task<OperationalReport?> ResolveOperationalReportAsync(
        string reportId,
        CancellationToken cancellationToken = default)
    {
        var connection = RequireConnectedConnection();
        EnsureOperationsHandler(connection);
        var report = await connection.InvokeAsync<OperationalReport?>(
            "ResolveOperationalReport",
            reportId,
            cancellationToken);
        if (report is not null)
        {
            ApplyOperationalReport(report);
        }
        return report;
    }

    public async Task<IReadOnlyList<OperationalReport>> ResolveMyOperationalReportsAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = RequireConnectedConnection();
        EnsureOperationsHandler(connection);
        var reports = await connection.InvokeAsync<IReadOnlyList<OperationalReport>>(
            "ResolveMyOperationalReports",
            cancellationToken);
        foreach (var report in reports)
        {
            ApplyOperationalReport(report);
        }
        return reports;
    }

    private void EnsureOperationsHandler(HubConnection connection)
    {
        if (ReferenceEquals(_operationsHandlerConnection, connection))
        {
            return;
        }

        _operationsSubscription?.Dispose();
        _operationsSubscription = connection.On<OperationalReport>(
            "operationalReportChanged",
            ApplyOperationalReport);
        _operationsHandlerConnection = connection;
    }

    private void ApplyOperationalReport(OperationalReport report)
    {
        _operationalReports[report.ReportId] = report;
        OperationalReportChanged?.Invoke(report);
    }
}
