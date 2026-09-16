using System.IO;
using System.Windows;
using System.Windows.Threading;
using NavBR.Client.Diagnostics;
using NavBR.Client.Driver;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;
using NavBR.Client.Omsi;
using NavBR.Client.Operations;
using NavBR.Client.Overlay;
using NavBR.Client.PluginBridge;
using NavBR.Client.Windows;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client;

public partial class App : Application
{
    internal OmsiPluginBridgeServer PluginBridge { get; } = new();
    internal NavBRTrayIconService TrayIcon { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        NavBRAppLog.StartSession();
        LocalizationService.Initialize();

        RemoteDiagnosticsService.Initialize();
        RemoteDiagnosticsService.Record("session", "info", "client-start");

        PluginBridge.ConnectionStateChanged += PluginBridge_ConnectionStateChanged;
        PluginBridge.CommandResultReceived += PluginBridge_CommandResultReceived;
        PluginBridge.Start();
        NavBRAppLog.Info("plugin-bridge-start");

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(Window_Loaded));
        base.OnStartup(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        TrayIcon.PrepareForSystemExit();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        TrayIcon.PrepareForSystemExit();
        TrayIcon.Dispose();

        RemoteDiagnosticsService.Record("session", "info", "client-stop");

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

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        WindowsThemeService.ApplyDarkTitleBar(window);

        if (window is MainWindow mainWindow)
        {
            Alpha12ShellUiInstaller.Install(mainWindow);
            Alpha11VisualTuning.Apply(mainWindow);
            OmsiProfilesUiInstaller.Install(mainWindow);
            Alpha12TechnicalControlsOrganizer.Attach(mainWindow);
            Alpha12ExperienceInstaller.Install(mainWindow);
            Alpha12MultiplayerStatusInstaller.Install(mainWindow);
            DriverProfileInstaller.Install(mainWindow);
            VirtualCompanyInstaller.Install(mainWindow);
            DispatcherInstaller.Install(mainWindow);
            SessionHealthInstaller.Install(mainWindow);
            TrayIcon.Attach(mainWindow);
        }

        if (window is HudOverlayWindow hudOverlay)
        {
            Alpha12HudThemeService.Attach(hudOverlay);
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
