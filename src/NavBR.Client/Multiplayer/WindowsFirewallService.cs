using System.Diagnostics;
using System.Security.Principal;

namespace NavBR.Client.Multiplayer;

internal static class WindowsFirewallService
{
    private const string RuleName = "OMSI NavBR Multiplayer - TCP 27730";

    public static async Task<bool> IsInboundRulePresentAsync(int port, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var command =
            $"$r = Get-NetFirewallRule -DisplayName '{EscapePowerShell(RuleName)}' -ErrorAction SilentlyContinue; " +
            $"if ($r) {{ $p = $r | Get-NetFirewallPortFilter -ErrorAction SilentlyContinue | Where-Object {{ $_.Protocol -eq 'TCP' -and $_.LocalPort -eq '{port}' }}; if ($p) {{ exit 0 }} }}; exit 1";

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{command.Replace("\"", "`\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> EnsureInboundRuleAsync(int port, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }

        var escapedExe = EscapePowerShell(executable);
        var escapedRule = EscapePowerShell(RuleName);
        var command =
            $"Get-NetFirewallRule -DisplayName '{escapedRule}' -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue; " +
            $"New-NetFirewallRule -DisplayName '{escapedRule}' -Direction Inbound -Action Allow -Protocol TCP -LocalPort {port} -Profile Private -Program '{escapedExe}' | Out-Null";

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "`\"")}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0 && await IsInboundRulePresentAsync(port, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsRunningAsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
