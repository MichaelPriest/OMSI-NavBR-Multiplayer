using System.Diagnostics;
using System.Net.Http;

namespace NavBR.Client.Multiplayer;

internal enum SessionNetworkQualityLevel
{
    Unknown,
    Good,
    Degraded,
    Poor
}

internal sealed record SessionNetworkQualitySnapshot(
    SessionNetworkQualityLevel Level,
    double? RoundTripMs,
    double? JitterMs,
    double LossPercent,
    int Samples,
    DateTimeOffset UpdatedAtUtc)
{
    public static SessionNetworkQualitySnapshot Empty { get; } = new(
        SessionNetworkQualityLevel.Unknown,
        null,
        null,
        0d,
        0,
        DateTimeOffset.UtcNow);
}

internal static class SessionNetworkQualityFeed
{
    private static readonly object Sync = new();
    private static SessionNetworkQualitySnapshot _snapshot = SessionNetworkQualitySnapshot.Empty;

    public static SessionNetworkQualitySnapshot Snapshot()
    {
        lock (Sync)
        {
            return _snapshot;
        }
    }

    public static void Update(SessionNetworkQualitySnapshot snapshot)
    {
        lock (Sync)
        {
            _snapshot = snapshot;
        }
    }

    public static void Reset() => Update(SessionNetworkQualitySnapshot.Empty);
}

internal sealed class SessionNetworkQualityMonitor : IAsyncDisposable
{
    private readonly object _sampleSync = new();
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };
    private readonly Queue<ProbeSample> _samples = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private Uri? _probeUri;

    public event Action<SessionNetworkQualitySnapshot>? QualityChanged;

    public SessionNetworkQualitySnapshot Current { get; private set; } = SessionNetworkQualitySnapshot.Empty;

    public void Start(string serverUrl)
    {
        Stop();
        _probeUri = BuildProbeUri(serverUrl);
        if (_probeUri is null)
        {
            Publish(SessionNetworkQualitySnapshot.Empty);
            return;
        }

        _loopCts = new CancellationTokenSource();
        _loopTask = RunAsync(_loopCts.Token);
    }

    public void Stop()
    {
        var cts = _loopCts;
        _loopCts = null;
        _probeUri = null;
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        lock (_sampleSync)
        {
            _samples.Clear();
        }
        Publish(SessionNetworkQualitySnapshot.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        var task = _loopTask;
        Stop();
        if (task is not null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }
        _http.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (!cancellationToken.IsCancellationRequested)
        {
            await ProbeOnceAsync(cancellationToken);
            if (!await timer.WaitForNextTickAsync(cancellationToken))
            {
                break;
            }
        }
    }

    private async Task ProbeOnceAsync(CancellationToken cancellationToken)
    {
        var uri = _probeUri;
        if (uri is null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var success = false;
        double? roundTripMs = null;
        try
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            success = response.IsSuccessStatusCode;
            if (success)
            {
                roundTripMs = stopwatch.Elapsed.TotalMilliseconds;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Count request timeout as a lost probe.
        }
        catch (HttpRequestException)
        {
            // Count transport failure as a lost probe.
        }
        finally
        {
            stopwatch.Stop();
        }

        SessionNetworkQualitySnapshot snapshot;
        lock (_sampleSync)
        {
            _samples.Enqueue(new ProbeSample(success, roundTripMs));
            while (_samples.Count > 20)
            {
                _samples.Dequeue();
            }
            snapshot = CalculateSnapshotUnsafe();
        }

        Publish(snapshot);
    }

    private SessionNetworkQualitySnapshot CalculateSnapshotUnsafe()
    {
        var samples = _samples.ToArray();
        if (samples.Length == 0)
        {
            return SessionNetworkQualitySnapshot.Empty;
        }

        var successful = samples
            .Where(sample => sample.Success && sample.RoundTripMs.HasValue)
            .Select(sample => sample.RoundTripMs!.Value)
            .ToArray();
        var loss = 100d * samples.Count(sample => !sample.Success) / samples.Length;
        var roundTrip = successful.Length == 0 ? null : successful.Average();
        double? jitter = null;
        if (successful.Length >= 2)
        {
            jitter = successful
                .Zip(successful.Skip(1), (left, right) => Math.Abs(right - left))
                .Average();
        }

        var level = ResolveLevel(roundTrip, jitter, loss, samples.Length);
        return new SessionNetworkQualitySnapshot(
            level,
            roundTrip,
            jitter,
            loss,
            samples.Length,
            DateTimeOffset.UtcNow);
    }

    private static SessionNetworkQualityLevel ResolveLevel(
        double? roundTripMs,
        double? jitterMs,
        double lossPercent,
        int samples)
    {
        if (samples < 2 || roundTripMs is null)
        {
            return SessionNetworkQualityLevel.Unknown;
        }

        if (lossPercent >= 20d || roundTripMs >= 500d || jitterMs >= 180d)
        {
            return SessionNetworkQualityLevel.Poor;
        }

        if (lossPercent >= 5d || roundTripMs >= 180d || jitterMs >= 70d)
        {
            return SessionNetworkQualityLevel.Degraded;
        }

        return SessionNetworkQualityLevel.Good;
    }

    private void Publish(SessionNetworkQualitySnapshot snapshot)
    {
        Current = snapshot;
        SessionNetworkQualityFeed.Update(snapshot);
        QualityChanged?.Invoke(snapshot);
    }

    private static Uri? BuildProbeUri(string? serverUrl)
    {
        var value = (serverUrl ?? string.Empty).Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        var builder = new UriBuilder(uri)
        {
            Path = "/api/ping",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri;
    }

    private sealed record ProbeSample(bool Success, double? RoundTripMs);
}
