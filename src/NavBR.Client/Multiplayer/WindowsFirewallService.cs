using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace NavBR.Client.Multiplayer;

internal sealed record FirewallRuleApplyResult(
    bool Success,
    bool Cancelled,
    string? ErrorMessage = null);

internal static class WindowsFirewallService
{
    private const string RuleName = "OMSI NavBR Multiplayer - TCP 27730";

    public static async Task<bool> IsInboundRulePresentAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var escapedRule = EscapePowerShell(RuleName);
        var command =
            "$ErrorActionPreference='SilentlyContinue'; " +
            $"$rules = Get-NetFirewallRule -DisplayName '{escapedRule}' | " +
            "Where-Object { $_.Enabled -eq 'True' -and $_.Direction -eq 'Inbound' -and $_.Action -eq 'Allow' }; " +
            "foreach ($r in $rules) { " +
            "$filters = $r | Get-NetFirewallPortFilter; " +
            $"if ($filters | Where-Object {{ $_.Protocol -eq 'TCP' -and ($_.LocalPort -eq '{port}' -or $_.LocalPort -eq {port}) }}) {{ exit 0 }} " +
            "}; exit 1";

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = BuildEncodedPowerShellArguments(command),
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

    public static async Task<bool> EnsureInboundRuleAsync(
        int port,
        CancellationToken cancellationToken = default) =>
        (await EnsureInboundRuleDetailedAsync(port, cancellationToken)).Success;

    public static async Task<FirewallRuleApplyResult> EnsureInboundRuleDetailedAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new FirewallRuleApplyResult(
                false,
                false,
                "O Windows Firewall só pode ser configurado no Windows.");
        }

        var escapedRule = EscapePowerShell(RuleName);
        var description = EscapePowerShell(
            "Permite conexões de entrada do OMSI NavBR Multiplayer na porta TCP 27730.");

        // Deliberately use a port rule for all Windows network profiles. The NavBR room
        // can be hosted by the WPF client or the dedicated server, and a rule bound to
        // only one executable/profile can appear to exist while still blocking traffic.
        var command =
            "$ErrorActionPreference='Stop'; " +
            $"Get-NetFirewallRule -DisplayName '{escapedRule}' -ErrorAction SilentlyContinue | " +
            "Remove-NetFirewallRule -ErrorAction SilentlyContinue; " +
            $"New-NetFirewallRule -DisplayName '{escapedRule}' " +
            $"-Description '{description}' -Direction Inbound -Action Allow " +
            $"-Protocol TCP -LocalPort {port} -Profile Any -Enabled True | Out-Null; " +
            "exit 0";

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = BuildEncodedPowerShellArguments(command),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            });

            if (process is null)
            {
                return new FirewallRuleApplyResult(
                    false,
                    false,
                    "O Windows não iniciou o processo administrativo para configurar a regra.");
            }

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                return new FirewallRuleApplyResult(
                    false,
                    false,
                    $"O Windows PowerShell retornou código {process.ExitCode} ao criar a regra.");
            }

            var verified = await IsInboundRulePresentAsync(port, cancellationToken);
            return verified
                ? new FirewallRuleApplyResult(true, false)
                : new FirewallRuleApplyResult(
                    false,
                    false,
                    "A criação terminou, mas a regra TCP não foi encontrada na verificação final.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new FirewallRuleApplyResult(
                false,
                true,
                "A solicitação de administrador foi cancelada.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new FirewallRuleApplyResult(
                false,
                false,
                ex.Message);
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

    private static string BuildEncodedPowerShellArguments(string command)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        return $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}";
    }

    private static string EscapePowerShell(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}
