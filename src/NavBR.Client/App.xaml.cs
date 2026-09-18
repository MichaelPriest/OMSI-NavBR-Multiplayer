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
using NavBR.Client.Windows;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client;

public partial class App : Application
{
    internal OmsiPluginBridgeServer PluginBridge { get; } = new();
    internal NavBRTrayIconService TrayIcon { get; } = new();
    internal NavBRNetworkRuntime NetworkRuntime { get; } = new();

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
        EventManager.RegisterClassHandler(
            typeof(HardwareCockpitView),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(HardwareCockpitView_Loaded));
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
        Alpha12FigmaOperationalWindowStyler.Apply(window);

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

        if (window is MainWindow mainWindow)
        {
            Alpha12FigmaShellInstaller.Install(mainWindow);
            Alpha11VisualTuning.Apply(mainWindow);
            OmsiProfilesUiInstaller.Install(mainWindow);
            Alpha12TechnicalControlsOrganizer.Attach(mainWindow);
            Alpha12GhostToolsInstaller.Install(mainWindow);
            Alpha12ExperienceInstaller.Install(mainWindow);
            Alpha12HudShortcutInstaller.Install(mainWindow);
            Alpha12MultiplayerStatusInstaller.Install(mainWindow);

            // Figma OPERAÇÃO order: CCO → Empresa → Rede → Equipe → Perfil.
            DispatcherInstaller.Install(mainWindow);
            VirtualCompanyInstaller.Install(mainWindow);
            CompanyNetworkInstaller.Install(mainWindow);
            CompanyMembersInstaller.Install(mainWindow);
            DriverProfileInstaller.Install(mainWindow);

            SessionHealthInstaller.Install(mainWindow);
            Alpha12NavigationPolishInstaller.Install(mainWindow);
            Alpha12FigmaNavigationModeInstaller.Install(mainWindow);
            Alpha12FigmaLiveDataInstaller.Install(mainWindow);
            Alpha12NavigationEtaInstaller.Install(mainWindow);
            Alpha12FigmaMultiplayerFidelityInstaller.Install(mainWindow);
            Alpha12FigmaOrderedStopsInstaller.Install(mainWindow);
            Alpha12FigmaHomeCompanyInstaller.Install(mainWindow);
            Alpha12FigmaHomeMultiplayerInstaller.Install(mainWindow);
            Alpha12VisualAccentInstaller.Install(mainWindow);
            Alpha12FigmaResponsiveShellInstaller.Install(mainWindow);
            Alpha12FigmaSystemSurfaceInstaller.Install(mainWindow);
            mainWindow.InitializeRoleplayForShell();
            TrayIcon.Attach(mainWindow);
        }

        if (window is HudOverlayWindow hudOverlay)
        {
            Alpha12HudThemeService.Attach(hudOverlay);
        }

        // Run last: Figma/installers may create or move ComboBox controls during Loaded.
        NavBRControlThemeInstaller.Attach(window);
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
