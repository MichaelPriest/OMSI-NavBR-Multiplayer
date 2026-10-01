using System.Diagnostics;
using System.Windows;
using NavBR.Client.Hardware;
using NavBR.Client.PluginInstaller;
using NavBR.Client.Telemetry;
using NavBR.Shared.Telemetry;

namespace NavBR.Client;

public partial class MainWindow
{
    private int? _openOmsiProcessId;
    private bool _openOmsiBridgeHooked;

    private void InitializeOpenOmsiRuntimeBridge()
    {
        if (_openOmsiBridgeHooked ||
            Application.Current is not App app)
        {
            return;
        }

        _openOmsiBridgeHooked = true;
        app.PluginBridge.ConnectionStateChanged +=
            OpenOmsiPluginBridge_ConnectionStateChanged;
    }

    private void DisposeOpenOmsiRuntimeBridge()
    {
        if (!_openOmsiBridgeHooked ||
            Application.Current is not App app)
        {
            return;
        }

        app.PluginBridge.ConnectionStateChanged -=
            OpenOmsiPluginBridge_ConnectionStateChanged;
        _openOmsiBridgeHooked = false;
    }

    private void OpenOmsiPluginBridge_ConnectionStateChanged(bool connected)
    {
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_telemetryProvider.IsAttached)
            {
                return;
            }

            if (connected)
            {
                _openOmsiProcessId =
                    OpenOmsiPluginInstallationService.GetRunningProcessId();
                _statusKey = "TelemetryConnecting";
                _telemetryStatusKey = "TelemetryWaiting";
                if (!_telemetryTimer.IsEnabled)
                {
                    _telemetryTimer.Start();
                }

                PollOpenOmsiTelemetry();
                return;
            }

            if (_openOmsiProcessId is not null)
            {
                _statusKey = "TelemetryConnecting";
                _telemetryStatusKey = "TelemetryWaiting";
                RenderCurrentState();
            }
        }));
    }

    private bool TryStartOpenOmsiRuntimeMonitoring()
    {
        _openOmsiProcessId =
            OpenOmsiPluginInstallationService.GetRunningProcessId();

        var connection = (Application.Current as App)?
            .PluginBridge
            .GetConnectionInfo();
        var bridgeReady =
            OpenOmsiBridgeTelemetryProvider.IsOpenOmsiRuntime(connection);

        if (_openOmsiProcessId is null && !bridgeReady)
        {
            return false;
        }

        _statusKey = bridgeReady
            ? "TelemetryConnected"
            : "TelemetryConnecting";
        _telemetryStatusKey = "TelemetryWaiting";
        _telemetryPollIntervalMs = Math.Max(
            _telemetryPollIntervalMs,
            500);
        _telemetryTimer.Interval =
            TimeSpan.FromMilliseconds(_telemetryPollIntervalMs);

        PollOpenOmsiTelemetry();
        if (!_telemetryTimer.IsEnabled)
        {
            _telemetryTimer.Start();
        }

        return true;
    }

    private bool PollOpenOmsiTelemetry()
    {
        var connection = (Application.Current as App)?
            .PluginBridge
            .GetConnectionInfo();
        var telemetry =
            OpenOmsiBridgeTelemetryProvider.Read("local", connection);

        if (telemetry is not null)
        {
            ApplyLocalTelemetrySnapshot(telemetry);
            UpdateTelemetryPollingCadence(telemetry);
            RenderCurrentState();
            return true;
        }

        var bridgeReady =
            OpenOmsiBridgeTelemetryProvider.IsOpenOmsiRuntime(connection);
        _openOmsiProcessId =
            GetLiveProcessId(_openOmsiProcessId) ??
            OpenOmsiPluginInstallationService.GetRunningProcessId();

        if (_openOmsiProcessId is null && !bridgeReady)
        {
            _lastTelemetry = null;
            _telemetryTimer.Stop();
            _statusKey = "OmsiNotRunning";
            _telemetryStatusKey = "TelemetryWaiting";
            ClearRoadmap();
            RenderCurrentState();
            return false;
        }

        _statusKey = bridgeReady
            ? "TelemetryConnected"
            : "TelemetryConnecting";
        _telemetryStatusKey = "TelemetryWaiting";
        UpdateTelemetryPollingCadence(null);
        RenderCurrentState();
        return true;
    }

    private void ApplyLocalTelemetrySnapshot(VehicleTelemetry telemetry)
    {
        _lastTelemetry = telemetry;

        var hardwareCockpit =
            HardwareCockpitBridgeController.Shared;
        if (hardwareCockpit.WantsTelemetry)
        {
            hardwareCockpit.PublishTelemetry(
                GetCurrentTelemetryForAlpha11());
        }

        _statusKey = "TelemetryConnected";
        _telemetryStatusKey = telemetry.IsInGame
            ? "TelemetryConnected"
            : "TelemetryWaiting";
    }

    private int? GetActiveSimulatorProcessIdForHud() =>
        _currentOmsi?.ProcessId ??
        GetLiveProcessId(_openOmsiProcessId);

    private static int? GetLiveProcessId(int? processId)
    {
        if (processId is not int id)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(id);
            return process.HasExited
                ? null
                : id;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
