using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using NavBR.Client.Diagnostics;
using NavBR.Client.Driver;
using NavBR.Client.Hardware;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;
using NavBR.Client.Network;
using NavBR.Client.Omsi;
using NavBR.Client.Operations;
using NavBR.Client.Overlay;
using NavBR.Client.PluginBridge;
using NavBR.Client.PluginInstaller;
using NavBR.Client.Windows;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client;

public partial class App : Application
{
    internal OmsiPluginBridgeServer PluginBridge { get; } = new();
    internal NavBRTrayIconService TrayIcon { get; } = new();
    internal NavBRNetworkRuntime NetworkRuntime { get; } = new();

    private CancellationTokenSource? _deferredPluginUpdateCts;

    protected override void OnStartup(StartupEventArgs e)
    {
        NavBRAppLog.StartSession();
        LocalizationService.Initialize();

        RemoteDiagnosticsService.Initialize();
        RemoteDiagnosticsService.Record("session", "info", "client-start");

        try
        {
            var omsiProfiles = OmsiInstallationProfileStore.Load();
            var preferredOmsiRoot = omsiProfiles
                .FirstOrDefault(profile => profile.IsPreferred)?
                .InstallDirectory
                ?? omsiProfiles.FirstOrDefault()?.InstallDirectory;
            var pluginBootstrap =
                OmsiPluginInstallationService.EnsureInstalledAtStartup(preferredOmsiRoot);
            NavBRAppLog.Info(
                $"plugin-bootstrap status={pluginBootstrap.Status} changed={pluginBootstrap.Changed} " +
                $"root={pluginBootstrap.OmsiRoot ?? "-"} message={pluginBootstrap.Message ?? "-"}");
            RemoteDiagnosticsService.Record(
                "plugin-bootstrap",
                pluginBootstrap.Status is "failed" or "conflict" ? "warning" : "info",
                $"status={pluginBootstrap.Status} changed={pluginBootstrap.Changed}");

            if (pluginBootstrap.Status == "omsi-running" &&
                !string.IsNullOrWhiteSpace(pluginBootstrap.OmsiRoot))
            {
                _deferredPluginUpdateCts = new CancellationTokenSource();
                _ = InstallPluginWhenOmsiClosesAsync(
                    pluginBootstrap.OmsiRoot,
                    _deferredPluginUpdateCts.Token);
                NavBRAppLog.Info("plugin-bootstrap deferred-until-omsi-exit");
            }
        }
        catch (Exception ex)
        {
            NavBRAppLog.Error("plugin-bootstrap-error", ex);
            RemoteDiagnosticsService.Record(
                "plugin-bootstrap",
                "warning",
                $"status=failed type={ex.GetType().Name}");
        }

        PluginBridge.ConnectionStateChanged += PluginBridge_ConnectionStateChanged;
        PluginBridge.CommandResultReceived += PluginBridge_CommandResultReceived;
        PluginBridge.Start();
        NavBRAppLog.Info("plugin-bridge-start");

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(Window_Loaded));
        EventManager.RegisterClassHandler(
            typeof(HardwareCockpitView),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(HardwareCockpitView_Loaded));

        base.OnStartup(e);

        // The historical WPF MainWindow is now only an in-memory native-service
        // host. Do not Show() it: React/WebView2 is the only desktop window
        // exposed to the user. Explicit shutdown keeps the tray/runtime alive
        // when the React shell is closed.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var nativeHost = new MainWindow();
        MainWindow = nativeHost;
        nativeHost.InitializeRoleplayForShell();
        TrayIcon.Attach(nativeHost);
        nativeHost.StartNativeRuntimeForReact();
        nativeHost.OpenPrimaryWebShell();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        TrayIcon.PrepareForSystemExit();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _deferredPluginUpdateCts?.Cancel();
        TrayIcon.PrepareForSystemExit();
        TrayIcon.Dispose();

        RemoteDiagnosticsService.Record("session", "info", "client-stop");

        try
        {
            NetworkRuntime.DisposeAsync().AsTask().GetAwaiter().GetResult();
            NavBRAppLog.Info("network-runtime-stop");
        }
        catch (Exception ex)
        {
            NavBRAppLog.Error("network-runtime-stop-error", ex);
        }

        try
        {
            PluginBridge.DisposeAsync().AsTask().GetAwaiter().GetResult();
            NavBRAppLog.Info("plugin-bridge-stop");
        }
        catch (Exception ex)
        {
            NavBRAppLog.Error("plugin-bridge-stop-error", ex);
            RemoteDiagnosticsService.Record(
                "plugin-bridge",
                "error",
                $"stop-error type={ex.GetType().Name} message={ex.Message}");
        }

        try
        {
            using var flushCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            RemoteDiagnosticsService.TryFlushAsync(flushCts.Token).GetAwaiter().GetResult();
        }
        catch
        {
            // Diagnostics are strictly best effort during shutdown.
        }

        NavBRAppLog.EndSession();
        base.OnExit(e);
    }

    private static async Task InstallPluginWhenOmsiClosesAsync(
        string omsiRoot,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var omsiRunning = false;
                var processes = Process.GetProcessesByName("Omsi");
                try
                {
                    foreach (var process in processes)
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                omsiRunning = true;
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }

                if (!omsiRunning)
                {
                    var installed = OmsiPluginInstallationService.InstallOrUpdate(omsiRoot);
                    NavBRAppLog.Info(
                        $"plugin-bootstrap deferred-install-complete root={installed.OmsiRoot} files={installed.InstalledFiles}");
                    RemoteDiagnosticsService.Record(
                        "plugin-bootstrap",
                        "info",
                        "status=deferred-installed restart-omsi-required");
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            NavBRAppLog.Error("plugin-bootstrap-deferred-install-error", ex);
            RemoteDiagnosticsService.Record(
                "plugin-bootstrap",
                "warning",
                $"status=deferred-failed type={ex.GetType().Name}");
        }
    }

    private static void PluginBridge_ConnectionStateChanged(bool connected)
    {
        RemoteDiagnosticsService.Record(
            "plugin-bridge",
            connected ? "info" : "warning",
            connected ? "connected" : "disconnected");
    }

    private static void PluginBridge_CommandResultReceived(PluginBridgeMessage message)
    {
        if (message.Success != false)
        {
            return;
        }

        RemoteDiagnosticsService.Record(
            "plugin-command",
            "error",
            $"error={message.ErrorCode ?? "unknown"} detail={message.ErrorMessage ?? string.Empty}");
    }

    private static void HardwareCockpitView_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is HardwareCockpitView hardwareView)
        {
            HardwareCockpitPersistenceInstaller.Attach(hardwareView);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        WindowsThemeService.ApplyDarkTitleBar(window);
        if (window is not NavBR.Client.MainWindow)
        {
            Alpha12FigmaOperationalWindowStyler.Apply(window);
        }

        if (window is DriverProfileWindow driverProfileWindow)
        {
            DriverProfilePortabilityInstaller.Attach(driverProfileWindow);
            DriverTripHistoryInstaller.Attach(driverProfileWindow);
        }

        if (window is SessionHealthWindow sessionHealthWindow)
        {
            SessionHealthDiagnosticsExportInstaller.Attach(sessionHealthWindow);
        }

        if (window is DispatcherWindow dispatcher && dispatcher.Owner is MainWindow dispatcherOwner)
        {
            DispatcherFigmaMapInstaller.Attach(
                dispatcher,
                dispatcherOwner.GetCurrentTelemetryForAlpha11,
                dispatcherOwner.GetActiveMapForOperations);
        }

        if (window is HudOverlayWindow hudOverlay)
        {
            Alpha12HudThemeService.Attach(hudOverlay);
        }

        // Auxiliary native windows still use the shared dark control theme.
        // The hidden MainWindow host has no user-facing controls anymore.
        if (window is not NavBR.Client.MainWindow)
        {
            NavBRControlThemeInstaller.Attach(window);
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        NavBRAppLog.Error("dispatcher-unhandled", e.Exception);
        RemoteDiagnosticsService.Record(
            "unhandled-exception",
            "error",
            $"type={e.Exception.GetType().Name} message={e.Exception.Message}");

        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OMSI NavBR Multiplayer");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "navbr-error.log"),
                $"[{DateTimeOffset.Now:O}] {e.Exception}\n\n");
        }
        catch
        {
            // Logging must never replace the original failure.
        }

        MessageBox.Show(
            $"O NavBR encontrou um erro e evitou o fechamento completo do aplicativo.\n\n{e.Exception.Message}\n\nDetalhes foram registrados em %LOCALAPPDATA%\\OMSI NavBR Multiplayer\\navbr-error.log.",
            "OMSI NavBR Multiplayer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
