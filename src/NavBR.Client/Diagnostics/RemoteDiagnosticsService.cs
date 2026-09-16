using System.IO;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using NavBR.Shared.Diagnostics;

namespace NavBR.Client.Diagnostics;

internal static partial class RemoteDiagnosticsService
{
    private const int MaxQueuedEvents = 500;
    private const int MaxBatchEvents = 50;
    private const string CollectorProtocol = "navbr-alpha11-test2";
    private static readonly TimeSpan DiscoveryCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly Uri DiscoveryUri = new(
        "https://michaelpriest.github.io/OMSI-NavBR-Multiplayer/diagnostics.json");
    private static readonly object QueueSync = new();
    private static readonly SemaphoreSlim FlushGate = new(1, 1);
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(6)
    };
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");
    private static readonly string QueuePath = Path.Combine(DirectoryPath, "diagnostics-queue.jsonl");
    private static readonly string SessionId = Guid.NewGuid().ToString("N");
    private static readonly string ClientVersion =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    private static DiagnosticContext _context = new(null, null, null, false);
    private static DiagnosticsDiscovery? _discovery;
    private static DateTimeOffset _discoveryCheckedUtc;
    private static int _flushScheduled;

    public static void Initialize()
    {
        if (DiagnosticsConsentStore.IsEnabled)
        {
            ScheduleFlush();
        }
    }

    public static void UpdateContext(
        string? omsiVersion,
        string? mapName,
        string? vehicleId,
        bool physicalVehiclesEnabled)
    {
        _context = new DiagnosticContext(
            Sanitize(omsiVersion, 80),
            Sanitize(mapName, 160),
            Sanitize(vehicleId, 256),
            physicalVehiclesEnabled);
    }

    public static void Record(string category, string severity, string? message)
    {
        if (!DiagnosticsConsentStore.IsEnabled)
        {
            return;
        }

        var context = _context;
        var diagnosticEvent = new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            Sanitize(category, 80) ?? "unknown",
            Sanitize(severity, 20) ?? "info",
            Sanitize(message, 1200) ?? string.Empty,
            context.OmsiVersion,
            context.MapName,
            context.VehicleId,
            context.PhysicalVehiclesEnabled);

        Append(new QueuedDiagnosticEvent(ClientVersion, SessionId, diagnosticEvent));
        ScheduleFlush();
    }

    public static void OnConsentChanged(bool enabled)
    {
        if (!enabled)
        {
            PurgeQueuedEvents();
            return;
        }

        Record("diagnostics-consent", "info", "enabled");
        ScheduleFlush();
    }

    public static async Task TryFlushAsync(CancellationToken cancellationToken = default)
    {
        if (!DiagnosticsConsentStore.IsEnabled ||
            !await FlushGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var endpoint = await ResolveEndpointAsync(cancellationToken).ConfigureAwait(false);
            if (endpoint is null)
            {
                return;
            }

            for (var pass = 0; pass < 4; pass++)
            {
                var queued = ReadQueue();
                if (queued.Count == 0)
                {
                    return;
                }

                var first = queued[0];
                var batchItems = queued
                    .TakeWhile(item =>
                        string.Equals(item.ClientVersion, first.ClientVersion, StringComparison.Ordinal) &&
                        string.Equals(item.SessionId, first.SessionId, StringComparison.Ordinal))
                    .Take(MaxBatchEvents)
                    .ToArray();

                if (batchItems.Length == 0)
                {
                    return;
                }

                var batch = new DiagnosticEventBatch(
                    first.ClientVersion,
                    first.SessionId,
                    batchItems.Select(item => item.Event).ToArray());

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = JsonContent.Create(batch)
                };
                request.Headers.TryAddWithoutValidation("X-NavBR-Collector", CollectorProtocol);

                using var response = await Http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                RemoveFirst(batchItems.Length);
            }
        }
        catch
        {
            // Automatic diagnostics must never interfere with gameplay or UI.
        }
        finally
        {
            FlushGate.Release();
        }
    }

    public static void PurgeQueuedEvents()
    {
        try
        {
            lock (QueueSync)
            {
                if (File.Exists(QueuePath))
                {
                    File.Delete(QueuePath);
                }
            }
        }
        catch
        {
            // Privacy action is best effort and must not crash the client.
        }
    }

    private static void ScheduleFlush()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                await TryFlushAsync().ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref _flushScheduled, 0);
            }
        });
    }

    private static void Append(QueuedDiagnosticEvent item)
    {
        try
        {
            lock (QueueSync)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(
                    QueuePath,
                    JsonSerializer.Serialize(item) + Environment.NewLine);
                TrimQueueUnsafe();
            }
        }
        catch
        {
            // Diagnostics must stay non-fatal.
        }
    }

    private static List<QueuedDiagnosticEvent> ReadQueue()
    {
        lock (QueueSync)
        {
            if (!File.Exists(QueuePath))
            {
                return new List<QueuedDiagnosticEvent>();
            }

            var result = new List<QueuedDiagnosticEvent>();
            foreach (var line in File.ReadLines(QueuePath).TakeLast(MaxQueuedEvents))
            {
                try
                {
                    var item = JsonSerializer.Deserialize<QueuedDiagnosticEvent>(line);
                    if (item is not null)
                    {
                        result.Add(item);
                    }
                }
                catch
                {
                    // Ignore malformed local queue lines.
                }
            }

            return result;
        }
    }

    private static void RemoveFirst(int count)
    {
        lock (QueueSync)
        {
            if (!File.Exists(QueuePath))
            {
                return;
            }

            var lines = File.ReadAllLines(QueuePath);
            var remaining = lines.Skip(Math.Min(count, lines.Length)).TakeLast(MaxQueuedEvents).ToArray();
            File.WriteAllLines(QueuePath, remaining);
        }
    }

    private static void TrimQueueUnsafe()
    {
        var lines = File.ReadAllLines(QueuePath);
        if (lines.Length <= MaxQueuedEvents)
        {
            return;
        }

        File.WriteAllLines(QueuePath, lines.TakeLast(MaxQueuedEvents));
    }

    private static async Task<Uri?> ResolveEndpointAsync(CancellationToken cancellationToken)
    {
        var environmentEndpoint = Environment.GetEnvironmentVariable("NAVBR_DIAGNOSTICS_ENDPOINT");
        if (Uri.TryCreate(environmentEndpoint, UriKind.Absolute, out var overrideUri) &&
            IsHttpEndpoint(overrideUri))
        {
            return overrideUri;
        }

        if (_discovery is not null &&
            DateTimeOffset.UtcNow - _discoveryCheckedUtc < DiscoveryCacheDuration)
        {
            return DiscoveryToUri(_discovery);
        }

        _discoveryCheckedUtc = DateTimeOffset.UtcNow;
        try
        {
            _discovery = await Http.GetFromJsonAsync<DiagnosticsDiscovery>(
                DiscoveryUri,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _discovery = new DiagnosticsDiscovery(false, null);
        }

        return DiscoveryToUri(_discovery);
    }

    private static Uri? DiscoveryToUri(DiagnosticsDiscovery? discovery)
    {
        if (discovery?.Enabled != true ||
            !Uri.TryCreate(discovery.Endpoint, UriKind.Absolute, out var endpoint) ||
            !IsHttpEndpoint(endpoint))
        {
            return null;
        }

        return endpoint;
    }

    private static bool IsHttpEndpoint(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback);

    private static string? Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        sanitized = UserProfilePathRegex().Replace(sanitized, @"C:\Users\<redacted>");
        sanitized = sanitized.Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase);
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }

    [GeneratedRegex(@"(?i)C:\\Users\\[^\\\s]+")]
    private static partial Regex UserProfilePathRegex();

    private sealed record QueuedDiagnosticEvent(
        string ClientVersion,
        string SessionId,
        DiagnosticEvent Event);

    private sealed record DiagnosticsDiscovery(bool Enabled, string? Endpoint);

    private sealed record DiagnosticContext(
        string? OmsiVersion,
        string? MapName,
        string? VehicleId,
        bool PhysicalVehiclesEnabled);
}
