using Microsoft.UI.Xaml;

namespace NavBR.Client.WinUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var commandLine = Environment.GetCommandLineArgs();
        var smokeOnly = commandLine.Any(argument => string.Equals(
            argument,
            "--xaml-smoke",
            StringComparison.OrdinalIgnoreCase));
        var runtimeSmoke = commandLine.Any(argument => string.Equals(
            argument,
            "--runtime-smoke",
            StringComparison.OrdinalIgnoreCase));

        if (runtimeSmoke)
        {
            try
            {
                var runtime = new NativeHostClient();
                await runtime.EnsureRuntimeHostAsync();

                using (var state = await runtime.GetStateAsync())
                {
                    var root = state.RootElement;
                    if (!root.TryGetProperty("ok", out var ok) ||
                        !ok.GetBoolean() ||
                        !root.TryGetProperty("payload", out var payload) ||
                        payload.ValueKind != System.Text.Json.JsonValueKind.Object ||
                        !payload.TryGetProperty("appVersion", out _))
                    {
                        throw new InvalidOperationException(
                            "Runtime Host IPC state smoke returned an invalid payload.");
                    }
                }

                using (var homeState = await runtime.GetStateAsync("home"))
                {
                    var payload = homeState.RootElement.GetProperty("payload");
                    if (payload.GetProperty("navigation").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("navigation3D").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("operations").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("roadmapStudio").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("ghost").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("driver").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("hardware").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("network").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("roleplay").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("roomDirectory").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("system").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("system").GetProperty("runtimeHost").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("system").GetProperty("runtimeHost").GetProperty("telemetryLastReadMilliseconds").ValueKind !=
                            System.Text.Json.JsonValueKind.Number ||
                        payload.GetProperty("system").GetProperty("runtimeHost").GetProperty("telemetryAverageReadMilliseconds").ValueKind !=
                            System.Text.Json.JsonValueKind.Number ||
                        payload.GetProperty("system").GetProperty("sessionHealth").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("system").GetProperty("pluginInstallation").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("system").GetProperty("hud").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("system").GetProperty("diagnostics").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("system").GetProperty("installations").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("system").GetProperty("legacyPreferences").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("multiplayer").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("companyNetwork").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("companyNetwork").GetProperty("node").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("multiplayer").TryGetProperty("players", out _))
                    {
                        throw new InvalidOperationException(
                            "Runtime Host scoped home state smoke returned unexpected modules.");
                    }
                }

                using (var navigationState = await runtime.GetStateAsync("navigation"))
                {
                    var payload = navigationState.RootElement.GetProperty("payload");
                    if (payload.GetProperty("navigation").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("navigation3D").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("operations").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("network").ValueKind !=
                            System.Text.Json.JsonValueKind.Null)
                    {
                        throw new InvalidOperationException(
                            "Runtime Host scoped navigation state smoke did not isolate navigation modules.");
                    }
                }

                using (var ghostState = await runtime.GetStateAsync("ghost"))
                {
                    var payload = ghostState.RootElement.GetProperty("payload");
                    if (payload.GetProperty("ghost").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("navigation").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("operations").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("roadmapStudio").ValueKind !=
                            System.Text.Json.JsonValueKind.Null)
                    {
                        throw new InvalidOperationException(
                            "Runtime Host scoped ghost state smoke did not isolate the Ghost/Replay module.");
                    }
                }

                using (var diagnosticsState = await runtime.GetStateAsync("diagnostics"))
                {
                    var payload = diagnosticsState.RootElement.GetProperty("payload");
                    if (payload.GetProperty("network").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("system").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("system").GetProperty("pluginInstallation").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("system").GetProperty("diagnostics").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("navigation").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("operations").ValueKind !=
                            System.Text.Json.JsonValueKind.Null)
                    {
                        throw new InvalidOperationException(
                            "Runtime Host scoped diagnostics state smoke did not isolate diagnostics modules.");
                    }
                }

                using (var companyState = await runtime.GetStateAsync("company"))
                {
                    var payload = companyState.RootElement.GetProperty("payload");
                    if (payload.GetProperty("companyNetwork").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("companyNetwork").GetProperty("node").ValueKind !=
                            System.Text.Json.JsonValueKind.Object)
                    {
                        throw new InvalidOperationException(
                            "Runtime Host scoped company state smoke did not include Company Node details.");
                    }
                }

                using (var driverState = await runtime.GetStateAsync("driver"))
                {
                    var payload = driverState.RootElement.GetProperty("payload");
                    var driver = payload.GetProperty("driver");
                    if (driver.ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        driver.GetProperty("profile").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        driver.GetProperty("tripHistory").ValueKind !=
                            System.Text.Json.JsonValueKind.Array ||
                        driver.GetProperty("profileTransfer").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("operations").ValueKind !=
                            System.Text.Json.JsonValueKind.Null ||
                        payload.GetProperty("ghost").ValueKind !=
                            System.Text.Json.JsonValueKind.Null)
                    {
                        throw new InvalidOperationException(
                            "Runtime Host scoped driver state smoke did not isolate the Driver module.");
                    }
                }

                using (var multiplayerState = await runtime.GetStateAsync("multiplayer"))
                {
                    var payload = multiplayerState.RootElement.GetProperty("payload");
                    var multiplayer = payload.GetProperty("multiplayer");
                    if (multiplayer.ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        !multiplayer.TryGetProperty("players", out var players) ||
                        players.ValueKind !=
                            System.Text.Json.JsonValueKind.Array ||
                        multiplayer.GetProperty("chat").ValueKind !=
                            System.Text.Json.JsonValueKind.Array ||
                        multiplayer.GetProperty("voiceInputDevices").ValueKind !=
                            System.Text.Json.JsonValueKind.Array ||
                        multiplayer.GetProperty("voiceOutputDevices").ValueKind !=
                            System.Text.Json.JsonValueKind.Array ||
                        multiplayer.GetProperty("voiceMixers").ValueKind !=
                            System.Text.Json.JsonValueKind.Array ||
                        payload.GetProperty("network").ValueKind !=
                            System.Text.Json.JsonValueKind.Object ||
                        payload.GetProperty("roomDirectory").ValueKind !=
                            System.Text.Json.JsonValueKind.Object)
                    {
                        throw new InvalidOperationException(
                            "Runtime Host scoped multiplayer state smoke did not include players, chat, voice, network diagnostics and public rooms.");
                    }
                }

                await runtime.SendCommandAsync(
                    "setPerformanceProfile",
                    new { profile = "stability" });

                using (var state = await runtime.GetStateAsync())
                {
                    var payload = state.RootElement.GetProperty("payload");
                    var configuredProfile = payload
                        .GetProperty("system")
                        .GetProperty("sessionHealth")
                        .GetProperty("pluginPerformance")
                        .GetProperty("configuredProfile")
                        .GetString();

                    if (!string.Equals(
                            configuredProfile,
                            "stability",
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Runtime Host performance profile smoke failed: {configuredProfile ?? "<null>"}.");
                    }
                }

                await runtime.SendCommandAsync(
                    "setPerformanceProfile",
                    new { profile = "auto" });
                await runtime.ShutdownOwnedHostAsync();
                Environment.Exit(0);
                return;
            }
            catch (Exception ex)
            {
                StartupLog.Write(ex);
                Environment.Exit(87);
                return;
            }
        }

        try
        {
            _window = new MainWindow(smokeOnly);
            if (smokeOnly)
            {
                Environment.Exit(0);
                return;
            }

            _window.Activate();
        }
        catch (Exception ex)
        {
            StartupLog.Write(ex);
            if (smokeOnly)
            {
                Environment.Exit(86);
                return;
            }

            throw;
        }
    }
}

internal static class StartupLog
{
    public static void Write(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OMSI NavBR Multiplayer");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "navbr-winui.log"),
                $"[{DateTimeOffset.Now:O}] {exception}\r\n\r\n");
        }
        catch
        {
        }
    }
}
