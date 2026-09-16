namespace NavBR.Shared.Diagnostics;

public sealed record DiagnosticEventBatch(
    string ClientVersion,
    string SessionId,
    IReadOnlyList<DiagnosticEvent> Events);

public sealed record DiagnosticEvent(
    DateTimeOffset TimestampUtc,
    string Category,
    string Severity,
    string Message,
    string? OmsiVersion = null,
    string? MapName = null,
    string? VehicleId = null,
    bool PhysicalVehiclesEnabled = false);
