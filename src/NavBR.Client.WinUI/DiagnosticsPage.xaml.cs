using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class DiagnosticsPage : UserControl
{
    public Func<string, object?, Task>? CommandHandler { get; set; }

    private bool _applyingState;

    public DiagnosticsPage()
    {
        InitializeComponent();
    }

    public void ApplyState(JsonElement state)
    {
        var system = JsonState.Property(state, "system");
        var plugin = JsonState.Property(system, "pluginInstallation");
        var health = JsonState.Property(system, "sessionHealth");
        var diagnostics = JsonState.Property(system, "diagnostics");
        var network = JsonState.Property(state, "network");

        var pluginConnected = JsonState.Bool(health, "pluginConnected");
        PluginStateText.Text = pluginConnected
            ? "CONECTADO"
            : (JsonState.String(plugin, "state") ?? "offline").ToUpperInvariant();

        MultiplayerStateText.Text = JsonState.Bool(health, "multiplayerConnected")
            ? "ONLINE"
            : "OFFLINE";

        LatencyText.Text = JsonState.Double(health, "latencyMs") is double latency
            ? $"{latency:0} ms"
            : "—";

        var performance = JsonState.Property(health, "pluginPerformance");
        var pressure = JsonState.Int(performance, "pressureLevel");
        PerformanceText.Text = pressure is null
            ? "—"
            : pressure == 0
                ? "NORMAL"
                : $"PRESSÃO {pressure}";

        PluginDetailText.Text =
            $"Instalado: {JsonState.String(plugin, "installedVersion") ?? "—"}\n" +
            $"Esperado: {JsonState.String(plugin, "expectedVersion") ?? "—"}\n" +
            $"Arquivos: {JsonState.Int(plugin, "verifiedFiles") ?? 0}/{JsonState.Int(plugin, "requiredFilesTotal") ?? 0}\n" +
            $"{JsonState.String(plugin, "message") ?? string.Empty}";
        InstallPluginButton.IsEnabled = JsonState.Bool(plugin, "installAvailable");

        _applyingState = true;
        try
        {
            DiagnosticsToggle.IsOn = JsonState.Bool(diagnostics, "enabled");
            UpnpToggle.IsOn = JsonState.Bool(network, "automaticUpnpEnabled");
        }
        finally
        {
            _applyingState = false;
        }

        LogText.Text =
            $"Log: {JsonState.String(diagnostics, "logPath") ?? "—"} · " +
            $"{(JsonState.Double(diagnostics, "logSizeBytes") ?? 0d) / 1024d:0} KiB";

        var netDiag = JsonState.Property(network, "diagnostics");
        var probe = JsonState.Property(network, "externalProbe");
        NetworkDetailText.Text =
            $"Host TCP: {JsonState.Int(network, "hostPort") ?? 27730} · " +
            $"host {(JsonState.Bool(network, "hostRunning") ? "ativo" : "inativo")}\n" +
            $"Firewall: {(JsonState.Bool(netDiag, "firewallRulePresent") ? "regra encontrada" : "não confirmada")} · " +
            $"UPnP: {(JsonState.Bool(netDiag, "upnpGatewayFound") ? "gateway encontrado" : "não confirmado")}\n" +
            $"IP externo: {JsonState.String(netDiag, "gatewayExternalAddress") ?? "—"} · " +
            $"porta externa: {(JsonState.IsObject(probe) ? (JsonState.Bool(probe, "reachable") ? "acessível" : "não acessível") : "não testada")}\n" +
            $"{JsonState.String(network, "message") ?? JsonState.String(network, "error") ?? string.Empty}";
    }

    private async Task RunAsync(string command, object? payload = null)
    {
        try
        {
            if (CommandHandler is null)
            {
                return;
            }

            await CommandHandler(command, payload);
            NoticeBar.Message = "Diagnóstico atualizado.";
            NoticeBar.Severity = InfoBarSeverity.Success;
            NoticeBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            NoticeBar.Message = ex.Message;
            NoticeBar.Severity = InfoBarSeverity.Error;
            NoticeBar.IsOpen = true;
        }
    }

    private async void VerifyPlugin_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("verifyOmsiPlugin");

    private async void InstallPlugin_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("installOmsiPlugin");

    private async void FlushDiagnostics_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("flushDiagnostics");

    private async void PurgeDiagnostics_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("purgeDiagnostics");

    private async void ExportHealth_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("exportSessionHealth");

    private async void RefreshNetwork_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("refreshNetworkDiagnostics");

    private async void Firewall_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("applyFirewallRule");

    private async void Probe_Click(object sender, RoutedEventArgs e) =>
        await RunAsync("runExternalPortProbe");

    private async void DiagnosticsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_applyingState)
        {
            return;
        }

        await RunAsync(
            "setDiagnosticsEnabled",
            new { enabled = DiagnosticsToggle.IsOn });
    }

    private async void UpnpToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_applyingState)
        {
            return;
        }

        await RunAsync(
            "setAutomaticUpnp",
            new { enabled = UpnpToggle.IsOn });
    }
}
