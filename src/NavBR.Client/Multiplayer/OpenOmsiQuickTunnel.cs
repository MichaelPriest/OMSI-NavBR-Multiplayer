using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace NavBR.Client.Multiplayer;

internal sealed class OpenOmsiQuickTunnel : IAsyncDisposable
{
    private const string Version = "2026.9.3";
    private const string WindowsAmd64Sha256 =
        "f096265ec2fcbe9bb6e2d64268db167ced3fcbb83d894bdb9e2fcdb26f2ea7e2";
    private const string Windows386Sha256 =
        "9b95ddc2eba67b86ed3dc4cc2a15881960563031b52ce564376af41fb91ad402";
    private static readonly Regex PublicUrlRegex =
        new(@"https://[a-z0-9-]+\.trycloudflare\.com", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly Process _process;
    private readonly TaskCompletionSource<string> _url =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private OpenOmsiQuickTunnel(Process process)
    {
        _process = process;
        _process.ErrorDataReceived += ProcessOnErrorDataReceived;
        _process.BeginErrorReadLine();
    }

    public bool IsAlive => !_process.HasExited;
    public string? PublicUrl =>
        _url.Task.IsCompletedSuccessfully ? _url.Task.Result : null;

    public static async Task<OpenOmsiQuickTunnel?> StartAsync(
        int localWebSocketPort,
        CancellationToken cancellationToken = default)
    {
        var executable = await EnsureCloudflaredAsync(cancellationToken);
        if (executable is null)
        {
            return null;
        }

        var start = new ProcessStartInfo
        {
            FileName = executable,
            Arguments =
                $"tunnel --no-autoupdate --url http://127.0.0.1:{localWebSocketPort}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        var process = Process.Start(start);
        return process is null ? null : new OpenOmsiQuickTunnel(process);
    }

    public async Task<string?> WaitForUrlAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await _url.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private void ProcessOnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
        {
            return;
        }

        var match = PublicUrlRegex.Match(e.Data);
        if (!match.Success ||
            match.Value.Contains("api.trycloudflare.com", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _url.TrySetResult(match.Value);
    }

    private static async Task<string?> EnsureCloudflaredAsync(
        CancellationToken cancellationToken)
    {
        var explicitPath = Environment.GetEnvironmentVariable("OMSI_CLOUDFLARED");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return explicitPath;
        }

        var besideApp = Path.Combine(AppContext.BaseDirectory, "cloudflared.exe");
        if (File.Exists(besideApp))
        {
            return besideApp;
        }

        var toolsCopy = Path.Combine(AppContext.BaseDirectory, "tools", "cloudflared.exe");
        if (File.Exists(toolsCopy))
        {
            return toolsCopy;
        }

        var fromPath = FindOnPath("cloudflared.exe");
        if (fromPath is not null)
        {
            return fromPath;
        }

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NavBR",
            "bin");
        Directory.CreateDirectory(root);

        var executable = Path.Combine(root, "cloudflared.exe");
        var marker = Path.Combine(root, "cloudflared.verified");
        if (File.Exists(executable) && File.Exists(marker))
        {
            return executable;
        }

        var amd64 = Environment.Is64BitOperatingSystem;
        var asset = amd64
            ? "cloudflared-windows-amd64.exe"
            : "cloudflared-windows-386.exe";
        var expectedHash = amd64
            ? WindowsAmd64Sha256
            : Windows386Sha256;
        var url =
            $"https://github.com/cloudflare/cloudflared/releases/download/{Version}/{asset}";

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        var data = await http.GetByteArrayAsync(url, cancellationToken);
        var actualHash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var temp = executable + ".part";
        await File.WriteAllBytesAsync(temp, data, cancellationToken);
        File.Move(temp, executable, overwrite: true);
        await File.WriteAllTextAsync(
            marker,
            $"{Version} {asset} {expectedHash}",
            cancellationToken);
        return executable;
    }

    private static string? FindOnPath(string fileName)
    {
        foreach (var raw in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(raw.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        catch
        {
        }

        _process.Dispose();
    }
}
