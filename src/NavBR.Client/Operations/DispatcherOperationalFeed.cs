using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Operations;

internal static class DispatcherOperationalFeed
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, OperationalReport> Reports =
        new(StringComparer.OrdinalIgnoreCase);
    private static Func<string, Task<OperationalReport?>>? _acknowledge;
    private static Func<string, Task<OperationalReport?>>? _resolve;

    public static bool CanManageReports => _acknowledge is not null && _resolve is not null;

    public static void ConfigureActions(
        Func<string, Task<OperationalReport?>> acknowledge,
        Func<string, Task<OperationalReport?>> resolve)
    {
        _acknowledge = acknowledge;
        _resolve = resolve;
    }

    public static void ClearActions()
    {
        _acknowledge = null;
        _resolve = null;
    }

    public static async Task<OperationalReport?> AcknowledgeAsync(string reportId)
    {
        var action = _acknowledge;
        return action is null ? null : await action(reportId);
    }

    public static async Task<OperationalReport?> ResolveAsync(string reportId)
    {
        var action = _resolve;
        return action is null ? null : await action(reportId);
    }

    public static void Replace(IEnumerable<OperationalReport> reports)
    {
        lock (Sync)
        {
            Reports.Clear();
            foreach (var report in reports)
            {
                Reports[report.ReportId] = report;
            }
        }
    }

    public static void Update(OperationalReport report)
    {
        lock (Sync)
        {
            Reports[report.ReportId] = report;
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Reports.Clear();
        }
    }

    public static IReadOnlyList<OperationalReport> Snapshot()
    {
        lock (Sync)
        {
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(20);
            foreach (var id in Reports
                         .Where(pair =>
                             pair.Value.Status == OperationalReportStatus.Resolved &&
                             pair.Value.UpdatedAtUtc < cutoff)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                Reports.Remove(id);
            }

            return Reports.Values
                .OrderByDescending(report => report.Status != OperationalReportStatus.Resolved)
                .ThenByDescending(report => report.Severity)
                .ThenByDescending(report => report.UpdatedAtUtc)
                .ToArray();
        }
    }

    public static OperationalReport? LatestForPlayer(string playerId)
    {
        lock (Sync)
        {
            return Reports.Values
                .Where(report => string.Equals(report.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(report => report.Status != OperationalReportStatus.Resolved)
                .ThenByDescending(report => report.UpdatedAtUtc)
                .FirstOrDefault();
        }
    }
}
