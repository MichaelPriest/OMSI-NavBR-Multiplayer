using System.Collections.Concurrent;

namespace NavBR.OmsiPluginExperimental;

/// <summary>
/// Keeps file I/O off OMSI's callback/render thread. Logging is diagnostic-only
/// and may be dropped under extreme backlog rather than stalling the simulator.
/// </summary>
internal static class PluginLogWriter
{
    private const int MaxQueuedLines = 512;
    private static readonly ConcurrentQueue<string> Pending = new();
    private static readonly SemaphoreSlim Signal = new(0);
    private static readonly object LifetimeSync = new();

    private static CancellationTokenSource? _cts;
    private static Task? _worker;
    private static int _queuedLines;

    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");

    private static string LogPath => Path.Combine(
        LogDirectory,
        "navbr-plugin.log");

    public static void Start()
    {
        lock (LifetimeSync)
        {
            if (_worker is not null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => RunAsync(_cts.Token));
        }
    }

    public static void Enqueue(string message)
    {
        if (Volatile.Read(ref _worker) is null)
        {
            return;
        }

        var queued = Interlocked.Increment(ref _queuedLines);
        if (queued > MaxQueuedLines)
        {
            Interlocked.Decrement(ref _queuedLines);
            return;
        }

        Pending.Enqueue(
            $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}");
        try
        {
            Signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    public static void Stop()
    {
        Task? worker;
        CancellationTokenSource? cts;
        lock (LifetimeSync)
        {
            worker = _worker;
            cts = _cts;
            _worker = null;
            _cts = null;
        }

        if (worker is null || cts is null)
        {
            return;
        }

        cts.Cancel();
        try
        {
            Signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }

        try
        {
            worker.Wait(TimeSpan.FromMilliseconds(250));
        }
        catch
        {
        }

        cts.Dispose();
    }

    private static async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested || !Pending.IsEmpty)
        {
            try
            {
                await Signal.WaitAsync(
                    TimeSpan.FromSeconds(1),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            FlushBatch();
        }

        FlushBatch();
    }

    private static void FlushBatch()
    {
        if (Pending.IsEmpty)
        {
            return;
        }

        try
        {
            var lines = new List<string>(64);
            while (lines.Count < 64 && Pending.TryDequeue(out var line))
            {
                Interlocked.Decrement(ref _queuedLines);
                lines.Add(line);
            }

            if (lines.Count == 0)
            {
                return;
            }

            Directory.CreateDirectory(LogDirectory);
            File.AppendAllLines(LogPath, lines);
        }
        catch
        {
            // Diagnostics must never affect OMSI stability or frame pacing.
            while (Pending.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _queuedLines);
            }
        }
    }
}
