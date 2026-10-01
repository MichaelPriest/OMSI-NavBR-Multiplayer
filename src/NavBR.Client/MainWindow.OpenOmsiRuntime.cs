using System.Diagnostics;
using System.Windows;
using NavBR.Client.Hardware;
using NavBR.Client.PluginInstaller;
using NavBR.Shared.OpenOmsi;
using NavBR.Client.Telemetry;
using NavBR.Shared.Telemetry;

namespace NavBR.Client;

public partial class MainWindow
{
    private int? _openOmsiProcessId;
    private bool _openOmsiBridgeHooked;
    private string[] _openOmsiVehicleIdentityRoots = [];

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
        RefreshOpenOmsiVehicleIdentityRoots();
        OpenOmsiLanGateway.Shared.Start();
        OpenOmsiLanGateway.Shared.LocalTelemetryReceived +=
            OpenOmsiLanGateway_LocalTelemetryReceived;
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
        OpenOmsiLanGateway.Shared.LocalTelemetryReceived -=
            OpenOmsiLanGateway_LocalTelemetryReceived;
        _openOmsiBridgeHooked = false;
    }

    private void OpenOmsiLanGateway_LocalTelemetryReceived(
        VehicleTelemetry telemetry)
    {
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_telemetryProvider.IsAttached)
            {
                return;
            }

            _openOmsiProcessId =
                OpenOmsiPluginInstallationService.GetRunningProcessId();
            var identified =
                EnrichOpenOmsiVehicleIdentity(telemetry);
            ApplyLocalTelemetrySnapshot(identified);
            UpdateTelemetryPollingCadence(identified);
            RenderCurrentState();
        }));
    }

    private VehicleTelemetry EnrichOpenOmsiVehicleIdentity(
        VehicleTelemetry telemetry)
    {
        if (!string.IsNullOrWhiteSpace(
                telemetry.VehicleCompatibilityId) ||
            string.IsNullOrWhiteSpace(telemetry.VehiclePath))
        {
            return telemetry;
        }

        foreach (var root in _openOmsiVehicleIdentityRoots)
        {
            var compatibilityId =
                OmsiVehicleIdentityReader.TryFingerprintInstalledVehicle(
                    root,
                    telemetry.VehiclePath);
            if (!string.IsNullOrWhiteSpace(compatibilityId))
            {
                return telemetry with
                {
                    VehicleCompatibilityId = compatibilityId
                };
            }
        }

        return telemetry;
    }

    private void RefreshOpenOmsiVehicleIdentityRoots()
    {
        try
        {
            var verification =
                OpenOmsiPluginInstallationService.Verify();
            var roots = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(
                    verification.ContentRoot))
            {
                roots.Add(
                    Path.GetFullPath(
                        verification.ContentRoot));
            }

            if (!string.IsNullOrWhiteSpace(
                    verification.ExecutablePath))
            {
                var executableDirectory =
                    Path.GetDirectoryName(
                        verification.ExecutablePath);
                if (!string.IsNullOrWhiteSpace(
                        executableDirectory))
                {
                    roots.Add(
                        Path.GetFullPath(
                            executableDirectory));
                }
            }

            _openOmsiVehicleIdentityRoots = roots
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            _openOmsiVehicleIdentityRoots = [];
        }
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
        var gatewayReady =
            OpenOmsiLanGateway.Shared.IsClientConnected;

        if (_openOmsiProcessId is null &&
            !bridgeReady &&
            !gatewayReady)
        {
            return false;
        }

        _statusKey = bridgeReady || gatewayReady
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

        var gatewayTelemetry =
            OpenOmsiLanGateway.Shared.LatestLocalTelemetry;
        var telemetry =
            gatewayTelemetry is not null &&
            DateTimeOffset.UtcNow - gatewayTelemetry.Timestamp <=
                TimeSpan.FromSeconds(2)
                ? gatewayTelemetry
                : OpenOmsiBridgeTelemetryProvider.Read(
                    "local",
                    connection);

        if (telemetry is not null)
        {
            ApplyLocalTelemetrySnapshot(telemetry);
            UpdateTelemetryPollingCadence(telemetry);
            RenderCurrentState();
            return true;
        }

        var bridgeReady =
            OpenOmsiBridgeTelemetryProvider.IsOpenOmsiRuntime(connection);
        var gatewayReady =
            OpenOmsiLanGateway.Shared.IsClientConnected;
        _openOmsiProcessId =
            GetLiveProcessId(_openOmsiProcessId) ??
            OpenOmsiPluginInstallationService.GetRunningProcessId();

        if (_openOmsiProcessId is null &&
            !bridgeReady &&
            !gatewayReady)
        {
            _lastTelemetry = null;
            _telemetryTimer.Stop();
            _statusKey = "OmsiNotRunning";
            _telemetryStatusKey = "TelemetryWaiting";
            ClearRoadmap();
            RenderCurrentState();
            return false;
        }

        _statusKey = bridgeReady || gatewayReady
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
