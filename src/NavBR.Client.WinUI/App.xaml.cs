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
                        payload.GetProperty("system").ValueKind !=
                            System.Text.Json.JsonValueKind.Object)
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
                            System.Text.Json.JsonValueKind.Object)
                    {
                        throw new InvalidOperationException(
                            "Runtime Host scoped navigation state smoke did not include navigation modules.");
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
