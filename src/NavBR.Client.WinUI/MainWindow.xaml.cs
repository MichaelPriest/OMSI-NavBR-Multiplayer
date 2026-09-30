using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NavBR.Client.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly NativeHostClient _runtime = new();
    private readonly bool _smokeOnly;
    private DispatcherQueueTimer? _refreshTimer;
    private int _stateRefreshIntervalMs = 750;
    private bool _refreshing;
    private bool _closing;
    private string _activePageTag = "home";
    private bool _applyingPerformanceProfile;
    private static readonly PerformanceProfileOption[] PerformanceProfiles =
    [
        new("auto", "Automático", "Equilibra FPS, responsividade e carga conforme o OMSI."),
        new("stability", "Estabilidade", "Reduz a cadência e o número de comandos para priorizar estabilidade."),
        new("multiplayer", "Multiplayer", "Mantém maior vazão de sincronização sem abandonar o governador adaptativo."),
        new("quality", "Qualidade", "Aumenta a frequência das atualizações quando há orçamento de frame disponível."),
        new("diagnostics", "Diagnóstico", "Reduz trabalho físico e aumenta a frequência das métricas para investigação.")
    ];

    public MainWindow(bool smokeOnly = false)
    {
        _smokeOnly = smokeOnly;
        InitializeComponent();
        Closed += MainWindow_Closed;

        NativeMultiplayerPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeOperationsPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeCompanyPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeDriverPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeGhostPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeRoleplayPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeHudPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeHardwarePage.CommandHandler = ExecuteNativeCommandAsync;
        NativeDiagnosticsPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeSettingsPage.CommandHandler = ExecuteNativeCommandAsync;
        PerformanceProfileComboBox.ItemsSource = PerformanceProfiles;

        if (_smokeOnly)
        {
            return;
        }

        Navigation.SelectedItem = Navigation.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item =>
                string.Equals(item.Tag?.ToString(), "home", StringComparison.Ordinal));
    }

    private async void RootGrid_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_smokeOnly)
        {
            return;
        }

        try
        {
            FooterStatusText.Text = "Iniciando Runtime Host x86...";
            await _runtime.EnsureRuntimeHostAsync();
            RuntimeBadgeText.Text = "RUNTIME x86 CONECTADO";
            FooterStatusText.Text = "Runtime Host conectado.";

            _refreshTimer = DispatcherQueue.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(_stateRefreshIntervalMs);
            _refreshTimer.Tick += async (_, _) => await RefreshStateAsync();
            _refreshTimer.Start();

            await RefreshStateAsync();
        }
        catch (Exception ex)
        {
            RuntimeBadgeText.Text = "RUNTIME INDISPONÍVEL";
            FooterStatusText.Text = ex.Message;
            HomeSubtitle.Text = "Não foi possível conectar ao backend nativo.";
            StartupLog.Write(ex);
        }
    }

    private async Task RefreshStateAsync()
    {
        if (_refreshing || _closing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            using var response = await _runtime.GetStateAsync(_activePageTag);
            if (!response.RootElement.TryGetProperty("ok", out var ok) ||
                !ok.GetBoolean() ||
                !response.RootElement.TryGetProperty("payload", out var payload))
            {
                throw new InvalidOperationException(
                    "Runtime Host returned an invalid state envelope.");
            }

            ApplyState(payload);
            RuntimeBadgeText.Text = "RUNTIME x86 CONECTADO";
            FooterStatusText.Text =
                $"Atualizado {DateTime.Now:HH:mm:ss} · WinUI x64 ↔ Runtime Host x86";
        }
        catch (Exception ex)
        {
            RuntimeBadgeText.Text = "RUNTIME RECONECTANDO";
            FooterStatusText.Text = ex.Message;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ApplyState(JsonElement state)
    {
        var omsi = Property(state, "omsi");
        var omsiRunning = Bool(omsi, "running");
        OmsiStatusText.Text = omsiRunning ? "CONECTADO" : "AGUARDANDO";
        OmsiDetailText.Text = omsiRunning
            ? $"OMSI {String(omsi, "version") ?? "detectado"} · PID {Integer(omsi, "processId")?.ToString() ?? "—"}"
            : "OMSI não está em execução";

        var multiplayer = Property(state, "multiplayer");
        var multiplayerConnected = Bool(multiplayer, "connected");
        MultiplayerStatusText.Text = multiplayerConnected ? "ONLINE" : "OFFLINE";
        MultiplayerDetailText.Text = multiplayerConnected
            ? $"{String(multiplayer, "roomId") ?? "sala"} · {Integer(multiplayer, "playerCount") ?? 0} jogadores"
            : "Nenhuma sala conectada";

        var companyNetwork = Property(state, "companyNetwork");
        var company = Property(companyNetwork, "company");
        var badge = Property(company, "selfBadge");
        if (badge.ValueKind == JsonValueKind.Undefined ||
            badge.ValueKind == JsonValueKind.Null)
        {
            var membership = Property(companyNetwork, "membership");
            badge = Property(membership, "badge");
        }

        var employeeNumber = String(badge, "employeeNumber");
        CompanyStatusText.Text = employeeNumber is null
            ? "SEM CRACHÁ"
            : $"#{employeeNumber}";
        CompanyDetailText.Text = employeeNumber is null
            ? String(company, "name") ?? "Nenhuma empresa vinculada"
            : $"{String(badge, "companyShortName") ?? String(badge, "companyName") ?? "EMPRESA"} · {String(badge, "role") ?? "membro"}";

        var telemetry = Property(state, "telemetry");
        if (telemetry.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            OperationText.Text = "Nenhuma telemetria recebida do OMSI.";
        }
        else
        {
            OperationText.Text =
                $"Mapa: {String(telemetry, "mapName") ?? "—"}\n" +
                $"Linha / rota: {String(telemetry, "line") ?? "—"} / {String(telemetry, "route") ?? "—"}\n" +
                $"Destino: {String(telemetry, "destinationName") ?? "—"}\n" +
                $"Próxima parada: {String(telemetry, "nextStopName") ?? "—"}\n" +
                $"Velocidade: {Number(telemetry, "speedKph")?.ToString("0.0") ?? "—"} km/h";
        }

        var omsiMemory = Property(omsi, "memory");
        var privateMiB = Number(omsiMemory, "privateMiB");
        var memoryLevel = String(omsiMemory, "level");
        OmsiPrivateMemoryText.Text = privateMiB is null
            ? "—"
            : $"{privateMiB:0} MiB";
        OmsiMemoryLevelText.Text = memoryLevel?.ToUpperInvariant() ?? "—";
        OmsiMemoryAdviceText.Text = memoryLevel switch
        {
            "elevated" => "Uso elevado. O NavBR continuará reduzindo apenas a própria carga; acompanhe se o mapa/ônibus continuar crescendo.",
            "high" => "Uso alto. Evite aumentar tráfego/objetos e observe carregamentos de tiles e addons pesados.",
            "critical" => "Uso muito alto para um processo 32-bit. Salve a sessão quando possível e reduza carga antes que a estabilidade piore.",
            "normal" => "Uso dentro da faixa observacional normal. Nenhuma limpeza forçada de memória é aplicada.",
            _ => "Inicie o OMSI para acompanhar memória privada e working set em tempo real."
        };

        var system = Property(state, "system");
        var runtimeHost = Property(system, "runtimeHost");
        var telemetryPollInterval = Integer(runtimeHost, "telemetryPollIntervalMilliseconds");
        var telemetryLastRead = Number(runtimeHost, "telemetryLastReadMilliseconds");
        var telemetryAverageRead = Number(runtimeHost, "telemetryAverageReadMilliseconds");
        var hudRefreshInterval = Integer(runtimeHost, "hudRefreshIntervalMilliseconds");
        RuntimeCadenceText.Text =
            $"Host x86: telemetria {telemetryPollInterval?.ToString() ?? "—"} ms · " +
            $"leitura {(telemetryAverageRead is double averageRead && averageRead > 0d ? averageRead.ToString("0.00") : "—")} ms média · " +
            $"última {(telemetryLastRead is double lastRead && lastRead > 0d ? lastRead.ToString("0.00") : "—")} ms · " +
            $"HUD {hudRefreshInterval?.ToString() ?? "—"} ms";

        var health = Property(system, "sessionHealth");
        var pluginConnected = Bool(health, "pluginConnected");
        var performance = Property(health, "pluginPerformance");

        var pressure = Integer(performance, "pressureLevel");
        var work = Number(performance, "workMilliseconds");
        var averageWork = Number(performance, "averageWorkMilliseconds");
        var frameInterval = Number(performance, "averageFrameIntervalMilliseconds");
        var maxCommands = Integer(performance, "maxCommandsPerSlice");
        var minimumInterval = Integer(performance, "minimumWorkIntervalMilliseconds");
        var lastFrameInterval = Number(performance, "lastFrameIntervalMilliseconds");
        var peakFrameInterval = Number(performance, "peakFrameIntervalMilliseconds");
        var frameStallCount = LongInteger(performance, "frameStallCount");
        var configuredProfile = String(performance, "configuredProfile") ?? "auto";
        var activeProfile = String(performance, "activeProfile");
        UpdateStateRefreshCadence(omsiRunning, pressure);

        PerformanceStatusText.Text = !pluginConnected
            ? "PLUGIN OFFLINE"
            : pressure is null
                ? "MONITORANDO"
                : pressure == 0
                    ? "NORMAL"
                    : $"PROTEÇÃO {pressure}";
        PerformanceDetailText.Text = pluginConnected
            ? averageWork is null
                ? "Aguardando amostras"
                : $"NavBR {averageWork:0.00} ms médio"
            : "Plugin OMSI não conectado";

        PressureText.Text = pressure?.ToString() ?? "—";
        PluginWorkText.Text = work is null ? "—" : $"{work:0.00} ms";
        FrameIntervalText.Text = frameInterval is null || frameInterval <= 0d
            ? "—"
            : $"{frameInterval:0.0} ms · {1000d / frameInterval:0} FPS";
        CommandBudgetText.Text = maxCommands?.ToString() ?? "—";
        LastFrameIntervalText.Text = lastFrameInterval is null
            ? "—"
            : $"{lastFrameInterval:0.0} ms";
        PeakFrameIntervalText.Text = peakFrameInterval is null
            ? "—"
            : $"{peakFrameInterval:0.0} ms";
        FrameStallCountText.Text = frameStallCount?.ToString() ?? "—";

        var selectedProfile = PerformanceProfiles.FirstOrDefault(profile =>
            string.Equals(profile.Id, configuredProfile, StringComparison.OrdinalIgnoreCase))
            ?? PerformanceProfiles[0];
        _applyingPerformanceProfile = true;
        try
        {
            PerformanceProfileComboBox.SelectedItem = selectedProfile;
        }
        finally
        {
            _applyingPerformanceProfile = false;
        }
        PerformanceProfileDetailText.Text = activeProfile is null
            ? $"{selectedProfile.DisplayName} · será aplicado quando o plugin conectar. {selectedProfile.Description}"
            : $"{selectedProfile.DisplayName} · ativo no plugin: {activeProfile}. {selectedProfile.Description}";

        PerformanceExplanationText.Text = !pluginConnected
            ? "O Performance Bridge começará a medir quando o plugin do OMSI estiver conectado."
            : pressure switch
            {
                null => "Plugin conectado. Aguardando a primeira janela de medição.",
                0 => $"Carga do NavBR dentro do callback está normal. Média {averageWork:0.00} ms; intervalo mínimo {minimumInterval ?? 0} ms.",
                1 => "Pressão leve detectada. O NavBR reduziu trabalho opcional para preservar o frame time.",
                2 => "Pressão moderada. Atualizações físicas e diagnóstico estão em cadência reduzida.",
                _ => "Pressão alta. O NavBR está usando o orçamento mínimo por callback para priorizar a estabilidade do OMSI."
            };

        HomeSubtitle.Text = omsiRunning
            ? "OMSI detectado · operação acompanhada pelo Runtime Host"
            : "WinUI 3 x64 conectado ao Runtime Host · aguardando OMSI";

        switch (_activePageTag)
        {
            case "multiplayer":
                NativeMultiplayerPage.ApplyState(state);
                break;
            case "cco":
                NativeOperationsPage.ApplyState(state);
                break;
            case "company":
                NativeCompanyPage.ApplyState(state);
                break;
            case "driver":
                NativeDriverPage.ApplyState(state);
                break;
            case "navigation":
                NativeNavigationPage.ApplyState(state);
                break;
            case "ghost":
                NativeGhostPage.ApplyState(state);
                break;
            case "roleplay":
                NativeRoleplayPage.ApplyState(state);
                break;
            case "hud":
                NativeHudPage.ApplyState(state);
                break;
            case "hardware":
                NativeHardwarePage.ApplyState(state);
                break;
            case "diagnostics":
                NativeDiagnosticsPage.ApplyState(state);
                break;
            case "settings":
                NativeSettingsPage.ApplyState(state);
                break;
        }
    }

    private void UpdateStateRefreshCadence(
        bool omsiRunning,
        int? pressure)
    {
        if (_refreshTimer is null)
        {
            return;
        }

        var intervalMs = pressure switch
        {
            >= 3 => 2_000,
            2 => 1_500,
            1 => 1_000,
            _ => 750
        };

        // When OMSI is not running there is no reason to rebuild the complete
        // x86 runtime snapshot at gameplay cadence.
        if (!omsiRunning)
        {
            intervalMs = Math.Max(intervalMs, 1_250);
        }

        if (_stateRefreshIntervalMs == intervalMs)
        {
            return;
        }

        _stateRefreshIntervalMs = intervalMs;
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
    }

    private void Navigation_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        ShowPage(item.Tag?.ToString() ?? "home");
    }

    private void ShowPage(string tag)
    {
        _activePageTag = string.IsNullOrWhiteSpace(tag)
            ? "home"
            : tag;

        HomePage.Visibility = tag == "home"
            ? Visibility.Visible
            : Visibility.Collapsed;
        PerformancePage.Visibility = tag == "performance"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeMultiplayerPage.Visibility = tag == "multiplayer"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeOperationsPage.Visibility = tag == "cco"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeCompanyPage.Visibility = tag == "company"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeDriverPage.Visibility = tag == "driver"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeNavigationPage.Visibility = tag == "navigation"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeGhostPage.Visibility = tag == "ghost"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeRoleplayPage.Visibility = tag == "roleplay"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeHudPage.Visibility = tag == "hud"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeHardwarePage.Visibility = tag == "hardware"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeDiagnosticsPage.Visibility = tag == "diagnostics"
            ? Visibility.Visible
            : Visibility.Collapsed;
        NativeSettingsPage.Visibility = tag == "settings"
            ? Visibility.Visible
            : Visibility.Collapsed;

        var usePlaceholder = tag is not (
            "home" or
            "performance" or
            "multiplayer" or
            "cco" or
            "company" or
            "driver" or
            "navigation" or
            "ghost" or
            "roleplay" or
            "hud" or
            "hardware" or
            "diagnostics" or
            "settings");
        ModulePage.Visibility = usePlaceholder
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!usePlaceholder)
        {
            _ = RefreshStateAsync();
            return;
        }

        (ModuleTitleText.Text, ModuleSubtitleText.Text) =
            ("NavBR", "Módulo nativo em migração.");
    }

    private async Task ExecuteNativeCommandAsync(
        string command,
        object? payload)
    {
        await _runtime.SendCommandAsync(command, payload);
        await RefreshStateAsync();
    }

    private void QuickNavButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not string tag)
        {
            return;
        }

        var target = Navigation.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item =>
                string.Equals(
                    item.Tag?.ToString(),
                    tag,
                    StringComparison.OrdinalIgnoreCase));

        if (target is not null)
        {
            Navigation.SelectedItem = target;
            ShowPage(tag);
        }
    }

    private async void PerformanceProfileComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_applyingPerformanceProfile ||
            PerformanceProfileComboBox.SelectedItem is not PerformanceProfileOption profile)
        {
            return;
        }

        try
        {
            await ExecuteNativeCommandAsync(
                "setPerformanceProfile",
                new { profile = profile.Id });
        }
        catch (Exception ex)
        {
            FooterStatusText.Text = ex.Message;
        }
    }

    private async void RefreshButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await _runtime.SendCommandAsync("refreshOmsiDetection");
        }
        catch
        {
        }

        await RefreshStateAsync();
    }

    private async void LaunchOmsiButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await _runtime.SendCommandAsync("launchOmsi");
            await Task.Delay(600);
            await RefreshStateAsync();
        }
        catch (Exception ex)
        {
            FooterStatusText.Text = ex.Message;
        }
    }

    private async void LegacyShellButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await _runtime.SendCommandAsync("openLegacyShell");
        }
        catch (Exception ex)
        {
            FooterStatusText.Text = ex.Message;
        }
    }

    private async void MainWindow_Closed(
        object sender,
        WindowEventArgs args)
    {
        _closing = true;
        _refreshTimer?.Stop();
        _refreshTimer = null;
        await _runtime.ShutdownOwnedHostAsync();
    }

    private static JsonElement Property(
        JsonElement element,
        string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value)
            ? value
            : default;

    private static string? String(
        JsonElement element,
        string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool Bool(
        JsonElement element,
        string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.True ||
               (value.ValueKind == JsonValueKind.False
                   ? false
                   : false);
    }

    private static int? Integer(
        JsonElement element,
        string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out var result)
            ? result
            : null;
    }

    private static double? Number(
        JsonElement element,
        string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.Number &&
               value.TryGetDouble(out var result)
            ? result
            : null;
    }

    private static long? LongInteger(
        JsonElement element,
        string name)
    {
        var value = Property(element, name);
        return value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt64(out var result)
            ? result
            : null;
    }
}


internal sealed record PerformanceProfileOption(
    string Id,
    string DisplayName,
    string Description);
