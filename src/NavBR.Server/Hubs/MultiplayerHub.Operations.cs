using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using NavBR.Shared.Multiplayer;

namespace NavBR.Server.Hubs;

public sealed partial class MultiplayerHub
{
    private const int MaxOperationalReportsPerRoom = 64;
    private const int MaxOperationalReportMessageLength = 180;

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, OperationalReport>> OperationalReportsByRoom =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<OperationalReport>> GetOperationalReports()
    {
        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            return Task.FromResult<IReadOnlyList<OperationalReport>>(Array.Empty<OperationalReport>());
        }

        PruneOperationalReports(presence.RoomId);
        var reports = GetOperationalRoomStore(presence.RoomId)
            .Values
            .OrderByDescending(report => report.Status != OperationalReportStatus.Resolved)
            .ThenByDescending(report => report.Severity)
            .ThenByDescending(report => report.UpdatedAtUtc)
            .ToArray();
        return Task.FromResult<IReadOnlyList<OperationalReport>>(reports);
    }

    public async Task<OperationalReport> SubmitOperationalReport(OperationalReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before submitting an operational report.");
        }

        if (!Enum.IsDefined(request.Kind) || !Enum.IsDefined(request.Severity))
        {
            throw new HubException("Invalid operational report.");
        }

        var message = NormalizeOptional(
            request.Message,
            MaxOperationalReportMessageLength,
            "operational report message");
        var reports = GetOperationalRoomStore(presence.RoomId);
        PruneOperationalReports(presence.RoomId);

        var existing = reports.Values
            .Where(report =>
                report.Status != OperationalReportStatus.Resolved &&
                string.Equals(report.PlayerId, presence.PlayerId, StringComparison.OrdinalIgnoreCase) &&
                report.Kind == request.Kind)
            .OrderByDescending(report => report.UpdatedAtUtc)
            .FirstOrDefault();

        var now = DateTimeOffset.UtcNow;
        OperationalReport safeReport;
        if (existing is not null)
        {
            safeReport = existing with
            {
                DisplayName = presence.DisplayName,
                Severity = request.Severity,
                Status = OperationalReportStatus.Open,
                Message = message,
                UpdatedAtUtc = now,
                AcknowledgedByPlayerId = null
            };
        }
        else
        {
            if (reports.Count >= MaxOperationalReportsPerRoom)
            {
                throw new HubException("Operational report limit reached for this room.");
            }

            safeReport = new OperationalReport(
                Guid.NewGuid().ToString("N"),
                presence.RoomId,
                presence.PlayerId,
                presence.DisplayName,
                request.Kind,
                request.Severity,
                OperationalReportStatus.Open,
                message,
                now,
                now);
        }

        reports[safeReport.ReportId] = safeReport;
        await Clients.Group(presence.RoomId).SendAsync("operationalReportChanged", safeReport);
        return safeReport;
    }

    public async Task<OperationalReport?> AcknowledgeOperationalReport(string reportId)
    {
        return await ChangeOperationalReportStatusAsync(
            reportId,
            OperationalReportStatus.Acknowledged,
            authorityRequired: true);
    }

    public async Task<OperationalReport?> ResolveOperationalReport(string reportId)
    {
        return await ChangeOperationalReportStatusAsync(
            reportId,
            OperationalReportStatus.Resolved,
            authorityRequired: false);
    }

    public async Task<IReadOnlyList<OperationalReport>> ResolveMyOperationalReports()
    {
        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before resolving operational reports.");
        }

        var reports = GetOperationalRoomStore(presence.RoomId);
        var now = DateTimeOffset.UtcNow;
        var changed = new List<OperationalReport>();
        foreach (var pair in reports.ToArray())
        {
            var report = pair.Value;
            if (!string.Equals(report.PlayerId, presence.PlayerId, StringComparison.OrdinalIgnoreCase) ||
                report.Status == OperationalReportStatus.Resolved)
            {
                continue;
            }

            var resolved = report with
            {
                Status = OperationalReportStatus.Resolved,
                UpdatedAtUtc = now
            };
            reports[report.ReportId] = resolved;
            changed.Add(resolved);
            await Clients.Group(presence.RoomId).SendAsync("operationalReportChanged", resolved);
        }

        return changed;
    }

    private async Task<OperationalReport?> ChangeOperationalReportStatusAsync(
        string reportId,
        OperationalReportStatus status,
        bool authorityRequired)
    {
        if (!registry.TryGet(Context.ConnectionId, out var presence) || presence is null)
        {
            throw new HubException("Join a room before changing an operational report.");
        }

        if (authorityRequired && !registry.IsTrafficAuthority(Context.ConnectionId))
        {
            throw new HubException("Only the current session authority can acknowledge operational reports.");
        }

        var normalizedId = NormalizeRequired(reportId, 64, "operational report id");
        var reports = GetOperationalRoomStore(presence.RoomId);
        if (!reports.TryGetValue(normalizedId, out var current))
        {
            return null;
        }

        var canResolve = registry.IsTrafficAuthority(Context.ConnectionId) ||
                         string.Equals(current.PlayerId, presence.PlayerId, StringComparison.OrdinalIgnoreCase);
        if (status == OperationalReportStatus.Resolved && !canResolve)
        {
            throw new HubException("Only the report owner or session authority can resolve this report.");
        }

        var updated = current with
        {
            Status = status,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            AcknowledgedByPlayerId = status == OperationalReportStatus.Acknowledged
                ? presence.PlayerId
                : current.AcknowledgedByPlayerId
        };
        reports[normalizedId] = updated;
        await Clients.Group(presence.RoomId).SendAsync("operationalReportChanged", updated);
        return updated;
    }

    private static ConcurrentDictionary<string, OperationalReport> GetOperationalRoomStore(string roomId) =>
        OperationalReportsByRoom.GetOrAdd(
            roomId,
            _ => new ConcurrentDictionary<string, OperationalReport>(StringComparer.OrdinalIgnoreCase));

    private static void PruneOperationalReports(string roomId)
    {
        if (!OperationalReportsByRoom.TryGetValue(roomId, out var reports))
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(20);
        foreach (var pair in reports.ToArray())
        {
            if (pair.Value.Status == OperationalReportStatus.Resolved &&
                pair.Value.UpdatedAtUtc < cutoff)
            {
                reports.TryRemove(pair.Key, out _);
            }
        }

        if (reports.IsEmpty)
        {
            OperationalReportsByRoom.TryRemove(roomId, out _);
        }
    }
}
