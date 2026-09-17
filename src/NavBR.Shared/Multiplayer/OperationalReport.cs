namespace NavBR.Shared.Multiplayer;

public enum OperationalReportKind
{
    Assistance = 0,
    Incident = 1
}

public enum OperationalReportSeverity
{
    Info = 0,
    Attention = 1,
    Critical = 2
}

public enum OperationalReportStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2
}

public sealed record OperationalReportRequest(
    OperationalReportKind Kind,
    OperationalReportSeverity Severity,
    string? Message = null);

public sealed record OperationalReport(
    string ReportId,
    string RoomId,
    string PlayerId,
    string DisplayName,
    OperationalReportKind Kind,
    OperationalReportSeverity Severity,
    OperationalReportStatus Status,
    string? Message,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? AcknowledgedByPlayerId = null);
