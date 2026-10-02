using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NavBR.Client.WinUI;

internal sealed class NativeHostClient
{
    private const string PipeName = "OMSI.NavBR.Multiplayer.NativeShell.v1";
    private Process? _ownedHost;

    public bool OwnsRuntimeHost =>
        _ownedHost is { HasExited: false };

    public async Task EnsureRuntimeHostAsync(
        CancellationToken cancellationToken = default)
    {
        if (await PingAsync(cancellationToken))
        {
            return;
        }

        var hostPath = ResolveRuntimeHostPath();
        if (hostPath is null)
        {
            throw new FileNotFoundException(
                "NavBR x86 Runtime Host was not found. Expected RuntimeHost\\OMSI.NavBR.Multiplayer.exe beside the WinUI app.");
        }

        _ownedHost = Process.Start(new ProcessStartInfo
        {
            FileName = hostPath,
            Arguments = "--native-host",
            WorkingDirectory = Path.GetDirectoryName(hostPath)!,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (_ownedHost is null)
        {
            throw new InvalidOperationException(
                "The NavBR x86 Runtime Host could not be started.");
        }

        var started = Stopwatch.StartNew();
        while (started.Elapsed < TimeSpan.FromSeconds(15))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_ownedHost.HasExited)
            {
                throw new InvalidOperationException(
                    $"NavBR Runtime Host exited during startup with code {_ownedHost.ExitCode}.");
            }

            if (await PingAsync(cancellationToken))
            {
                return;
            }

            await Task.Delay(200, cancellationToken);
        }

        throw new TimeoutException(
            "Timed out waiting for the NavBR Runtime Host IPC bridge.");
    }

    public async Task<bool> PingAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var document = await SendAsync(
                new { kind = "ping" },
                TimeSpan.FromMilliseconds(500),
                cancellationToken);
            return document.RootElement.TryGetProperty("ok", out var ok) &&
                   ok.GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    public async Task<JsonDocument> GetStateAsync(
        string? scope = null,
        CancellationToken cancellationToken = default) =>
        await SendAsync(
            string.IsNullOrWhiteSpace(scope)
                ? new { kind = "state", scope = (string?)null }
                : new { kind = "state", scope },
            TimeSpan.FromSeconds(2),
            cancellationToken);

    public async Task SendCommandAsync(
        string command,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        using var result = await SendAsync(
            new
            {
                kind = "command",
                command,
                payload
            },
            TimeSpan.FromSeconds(5),
            cancellationToken);

        if (!result.RootElement.TryGetProperty("ok", out var ok) ||
            !ok.GetBoolean())
        {
            var error = result.RootElement.TryGetProperty("error", out var errorNode)
                ? errorNode.GetString()
                : "unknown_error";
            throw new InvalidOperationException(
                $"Runtime Host command failed: {error}");
        }
    }

    public async Task ShutdownOwnedHostAsync()
    {
        if (!OwnsRuntimeHost)
        {
            return;
        }

        try
        {
            using var result = await SendAsync(
                new { kind = "shutdown" },
                TimeSpan.FromSeconds(1),
                CancellationToken.None);
        }
        catch
        {
        }

        try
        {
            if (_ownedHost is not null &&
                !_ownedHost.HasExited)
            {
                try
                {
                    await _ownedHost.WaitForExitAsync()
                        .WaitAsync(TimeSpan.FromSeconds(8));
                }
                catch (TimeoutException)
                {
                    // WPF shutdown can legitimately spend a couple seconds
                    // flushing diagnostics and disposing background services.
                    // If the host we started still refuses to exit, terminate
                    // only that owned process so WinUI never leaves an orphan.
                    if (!_ownedHost.HasExited)
                    {
                        _ownedHost.Kill(entireProcessTree: true);
                        await _ownedHost.WaitForExitAsync()
                            .WaitAsync(TimeSpan.FromSeconds(2));
                    }
                }
            }
        }
        catch
        {
            if (_ownedHost is { HasExited: false })
            {
                try
                {
                    _ownedHost.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            }
        }
        finally
        {
            _ownedHost?.Dispose();
            _ownedHost = null;
        }
    }

    private static async Task<JsonDocument> SendAsync(
        object request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutCts.CancelAfter(timeout);

        await using var pipe = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await pipe.ConnectAsync(
            Math.Max(100, (int)timeout.TotalMilliseconds),
            timeoutCts.Token);

        using var reader = new StreamReader(
            pipe,
            new UTF8Encoding(false),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 16 * 1024,
            leaveOpen: true);
        using var writer = new StreamWriter(
            pipe,
            new UTF8Encoding(false),
            bufferSize: 16 * 1024,
            leaveOpen: true)
        {
            AutoFlush = true
        };

        await writer.WriteLineAsync(
            JsonSerializer.Serialize(request).AsMemory(),
            timeoutCts.Token);

        var response = await reader.ReadLineAsync(timeoutCts.Token);
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new IOException(
                "Runtime Host returned an empty IPC response.");
        }

        return JsonDocument.Parse(response);
    }

    private static string? ResolveRuntimeHostPath()
    {
        var configured = Environment.GetEnvironmentVariable(
            "NAVBR_RUNTIME_HOST");
        if (!string.IsNullOrWhiteSpace(configured) &&
            File.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(
                baseDirectory,
                "RuntimeHost",
                "OMSI.NavBR.Multiplayer.exe"),
            Path.Combine(
                baseDirectory,
                "OMSI.NavBR.Multiplayer.RuntimeHost.exe"),
            Path.Combine(
                baseDirectory,
                "OMSI.NavBR.Multiplayer.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
