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
    private bool _refreshing;
    private bool _closing;

    public MainWindow(bool smokeOnly = false)
    {
        _smokeOnly = smokeOnly;
        InitializeComponent();
        Closed += MainWindow_Closed;

        NativeMultiplayerPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeOperationsPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeCompanyPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeRoleplayPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeHudPage.CommandHandler = ExecuteNativeCommandAsync;
        NativeHardwarePage.CommandHandler = ExecuteNativeCommandAsync;

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
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(750);
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
            using var response = await _runtime.GetStateAsync();
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

        var system = Property(state, "system");
        var health = Property(system, "sessionHealth");
        var pluginConnected = Bool(health, "pluginConnected");
        var performance = Property(health, "pluginPerformance");

        var pressure = Integer(performance, "pressureLevel");
        var work = Number(performance, "workMilliseconds");
        var averageWork = Number(performance, "averageWorkMilliseconds");
        var frameInterval = Number(performance, "averageFrameIntervalMilliseconds");
        var maxCommands = Integer(performance, "maxCommandsPerSlice");
        var minimumInterval = Integer(performance, "minimumWorkIntervalMilliseconds");

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
        FrameIntervalText.Text = frameInterval is null ? "—" : $"{frameInterval:0.0} ms";
        CommandBudgetText.Text = maxCommands?.ToString() ?? "—";

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

        NativeMultiplayerPage.ApplyState(state);
        NativeOperationsPage.ApplyState(state);
        NativeCompanyPage.ApplyState(state);
        NativeNavigationPage.ApplyState(state);
        NativeRoleplayPage.ApplyState(state);
        NativeHudPage.ApplyState(state);
        NativeHardwarePage.ApplyState(state);
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
        NativeNavigationPage.Visibility = tag == "navigation"
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

        var usePlaceholder = tag is not (
            "home" or
            "performance" or
            "multiplayer" or
            "cco" or
            "company" or
            "navigation" or
            "roleplay" or
            "hud" or
            "hardware");
        ModulePage.Visibility = usePlaceholder
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!usePlaceholder)
        {
            return;
        }

        (ModuleTitleText.Text, ModuleSubtitleText.Text) = tag switch
        {
            "diagnostics" => ("Diagnóstico", "Estado do OMSI, plugin, bridge, multiplayer, rede e reparos."),
            "settings" => ("Configurações", "Idioma, aparência, OMSI, atualização, privacidade e recursos experimentais."),
            _ => ("NavBR", "Módulo nativo em migração.")
        };
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
}
