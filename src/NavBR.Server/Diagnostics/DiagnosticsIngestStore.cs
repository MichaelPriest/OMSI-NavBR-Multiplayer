using System.Text.Json;
using NavBR.Shared.Diagnostics;

namespace NavBR.Server.Diagnostics;

public sealed class DiagnosticsIngestStore
{
    private const int MaxEventsPerBatch = 50;
    private const int MaxClientVersionLength = 80;
    private const int MaxSessionIdLength = 64;
    private const int MaxCategoryLength = 80;
    private const int MaxSeverityLength = 20;
    private const int MaxMessageLength = 1200;
    private const int MaxOmsiVersionLength = 80;
    private const int MaxMapNameLength = 160;
    private const int MaxVehicleIdLength = 256;

    private static readonly HashSet<string> AllowedSeverities = new(StringComparer.OrdinalIgnoreCase)
    {
        "info",
        "warning",
        "error"
    };

    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly string _directory;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public DiagnosticsIngestStore()
    {
        _directory = Environment.GetEnvironmentVariable("NAVBR_DIAGNOSTICS_DIRECTORY")?.Trim()
            ?? Path.Combine(AppContext.BaseDirectory, "data", "diagnostics");
    }

    public static bool TryValidate(DiagnosticEventBatch? batch, out string error)
    {
        error = string.Empty;
        if (batch is null)
        {
            error = "missing-batch";
            return false;
        }

        if (string.IsNullOrWhiteSpace(batch.ClientVersion) ||
            batch.ClientVersion.Length > MaxClientVersionLength)
        {
            error = "invalid-client-version";
            return false;
        }

        if (string.IsNullOrWhiteSpace(batch.SessionId) ||
            batch.SessionId.Length > MaxSessionIdLength ||
            !batch.SessionId.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'))
        {
            error = "invalid-session-id";
            return false;
        }

        if (batch.Events is null || batch.Events.Count is < 1 or > MaxEventsPerBatch)
        {
            error = "invalid-event-count";
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var item in batch.Events)
        {
            if (item is null)
            {
                error = "invalid-event";
                return false;
            }

            if (item.TimestampUtc < now.AddDays(-14) || item.TimestampUtc > now.AddMinutes(15))
            {
                error = "invalid-event-time";
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.Category) || item.Category.Length > MaxCategoryLength ||
                string.IsNullOrWhiteSpace(item.Severity) || item.Severity.Length > MaxSeverityLength ||
                !AllowedSeverities.Contains(item.Severity) ||
                item.Message.Length > MaxMessageLength ||
                item.OmsiVersion?.Length > MaxOmsiVersionLength ||
                item.MapName?.Length > MaxMapNameLength ||
                item.VehicleId?.Length > MaxVehicleIdLength)
            {
                error = "invalid-event-fields";
                return false;
            }
        }

        return true;
    }

    public async Task AppendAsync(DiagnosticEventBatch batch, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var filePath = Path.Combine(
            _directory,
            $"navbr-diagnostics-{DateTimeOffset.UtcNow:yyyy-MM-dd}.jsonl");

        var envelope = new StoredDiagnosticBatch(
            ReceivedUtc: DateTimeOffset.UtcNow,
            ClientVersion: batch.ClientVersion,
            SessionId: batch.SessionId,
            Events: batch.Events);
        var line = JsonSerializer.Serialize(envelope, _jsonOptions) + Environment.NewLine;

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(filePath, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private sealed record StoredDiagnosticBatch(
        DateTimeOffset ReceivedUtc,
        string ClientVersion,
        string SessionId,
        IReadOnlyList<DiagnosticEvent> Events);
}
