using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    private NatDiagnosticsSnapshot? _webNatDiagnostics;
    private ExternalPortProbeResult? _webExternalPortProbe;
    private string? _webNetworkMessage;
    private string? _webNetworkError;

    private object BuildWebNetworkState()
    {
        var settings = MultiplayerSettingsStore.Load();
        var diagnostics = _webNatDiagnostics;
        var probe = _webExternalPortProbe;

        return new
        {
            hostPort = NatDiagnosticsService.HostPort,
            hostRunning = _multiplayerWindow?.IsHostRunningForWeb == true,
            runningAsAdministrator = WindowsFirewallService.IsRunningAsAdministrator(),
            automaticUpnpEnabled = settings.EnableAutomaticUpnp,
            externalProbeConfigured = ExternalPortProbeClient.IsConfiguredForCurrentEnvironment(),
            externalProbeServiceOrigin = ExternalPortProbeClient.GetConfiguredServiceOrigin(),
            message = _webNetworkMessage,
            error = _webNetworkError,
            diagnostics = diagnostics is null
                ? null
                : new
                {
                    localIpv4Addresses = diagnostics.LocalIpv4Addresses,
                    localPortListening = diagnostics.LocalPortListening,
                    firewallRulePresent = diagnostics.FirewallRulePresent,
                    automaticUpnpEnabled = diagnostics.AutomaticUpnpEnabled,
                    upnpGatewayFound = diagnostics.UpnpGatewayFound,
                    gatewayLocalAddress = diagnostics.GatewayLocalAddress,
                    gatewayExternalAddress = diagnostics.GatewayExternalAddress,
                    environmentKind = diagnostics.EnvironmentKind.ToString(),
                    externalPortVerified = diagnostics.ExternalPortVerified,
                    technicalNote = diagnostics.TechnicalNote
                },
            externalProbe = probe is null
                ? null
                : new
                {
                    reachable = probe.Reachable,
                    port = probe.Port,
                    status = probe.Status,
                    checkedAtUtc = probe.CheckedAtUtc,
                    durationMilliseconds = probe.DurationMilliseconds
                }
        };
    }

    private async Task RefreshWebNetworkDiagnosticsAsync()
    {
        _webNetworkError = null;
        _webNetworkMessage = "Atualizando diagnóstico de rede…";

        try
        {
            var service = new NatDiagnosticsService();
            _webNatDiagnostics = await service.InspectAsync();
            _webNetworkMessage = "Diagnóstico de rede atualizado.";
        }
        catch (Exception ex)
        {
            _webNetworkError = ex.Message;
            _webNetworkMessage = null;
        }
    }

    private async Task ApplyWebFirewallRuleAsync()
    {
        _webNetworkError = null;
        _webNetworkMessage = "Solicitando permissão administrativa para configurar TCP 27730…";

        try
        {
            var result = await WindowsFirewallService.EnsureInboundRuleDetailedAsync(
                NatDiagnosticsService.HostPort);

            _webNetworkMessage = result.Success
                ? "Regra TCP 27730 aplicada e confirmada em todos os perfis de rede."
                : result.Cancelled
                    ? "A solicitação de administrador foi cancelada; a regra não foi alterada."
                    : null;
            _webNetworkError = result.Success || result.Cancelled
                ? null
                : result.ErrorMessage ?? "Não foi possível aplicar a regra TCP 27730.";

            await RefreshWebNetworkDiagnosticsAsync();

            if (result.Success)
            {
                _webNetworkMessage =
                    "Regra TCP 27730 aplicada e confirmada em todos os perfis de rede.";
            }
        }
        catch (Exception ex)
        {
            _webNetworkError = ex.Message;
            _webNetworkMessage = null;
        }
    }

    private async Task SetWebAutomaticUpnpAsync(bool enabled)
    {
        _webNetworkError = null;

        try
        {
            if (_multiplayerWindow is not null)
            {
                _multiplayerWindow.SetAutomaticUpnpFromWeb(enabled);
            }
            else
            {
                var settings = MultiplayerSettingsStore.Load();
                MultiplayerSettingsStore.Save(settings with
                {
                    EnableAutomaticUpnp = enabled
                });
            }

            _webNetworkMessage = enabled
                ? "UPnP automático ativado para a próxima sala hospedada."
                : "UPnP automático desativado.";
            await RefreshWebNetworkDiagnosticsAsync();
        }
        catch (Exception ex)
        {
            _webNetworkError = ex.Message;
            _webNetworkMessage = null;
        }
    }

    private async Task RunWebExternalPortProbeAsync()
    {
        _webNetworkError = null;
        _webNetworkMessage = "Executando teste externo da porta TCP 27730…";

        try
        {
            using var probe = new ExternalPortProbeClient();
            if (!probe.IsConfigured)
            {
                _webExternalPortProbe = null;
                _webNetworkError = null;
                _webNetworkMessage =
                    "Teste externo indisponível nesta instalação. A sala local/LAN continua funcionando; UPnP e endereço externo são verificados separadamente.";
                return;
            }

            _webExternalPortProbe = await probe.ProbeAsync();
            if (_webExternalPortProbe is null)
            {
                _webNetworkError = "O serviço externo não retornou resultado.";
                _webNetworkMessage = null;
                return;
            }

            _webNetworkMessage = _webExternalPortProbe.Reachable
                ? "A porta TCP 27730 respondeu ao teste externo."
                : "A porta TCP 27730 não respondeu ao teste externo.";
        }
        catch (Exception ex)
        {
            _webExternalPortProbe = null;
            _webNetworkError = ex.Message;
            _webNetworkMessage = null;
        }
    }
}
