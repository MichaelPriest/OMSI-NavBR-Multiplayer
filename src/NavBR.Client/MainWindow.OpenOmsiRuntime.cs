using System.Diagnostics;
using System.Windows;
using NavBR.Client.Hardware;
using NavBR.Client.Maps;
using NavBR.Client.Omsi;
using NavBR.Client.PluginInstaller;
using NavBR.Shared.OpenOmsi;
using NavBR.Client.Telemetry;
using NavBR.Shared.Telemetry;

namespace NavBR.Client;

public partial class MainWindow
{
    private int? _openOmsiProcessId;
    private readonly string _openOmsiInstanceId =
        $"navbr-{Environment.ProcessId}";
    private bool _openOmsiBridgeHooked;
    private string[] _openOmsiVehicleIdentityRoots = [];
    private string? _openOmsiCachedVehiclePath;
    private string? _openOmsiCachedVehicleCompatibilityId;
    private readonly Dictionary<string, string?>
        _openOmsiVehicleFingerprintCache =
            new(StringComparer.OrdinalIgnoreCase);
    private string? _openOmsiCachedMapReference;
    private string? _openOmsiCachedMapCompatibilityId;

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
                EnrichOpenOmsiCompatibilityIdentity(telemetry);
            ApplyLocalTelemetrySnapshot(identified);
            UpdateTelemetryPollingCadence(identified);
            RenderCurrentState();
        }));
    }

    private VehicleTelemetry EnrichOpenOmsiCompatibilityIdentity(
        VehicleTelemetry telemetry)
    {
        var result = telemetry;

        if (string.IsNullOrWhiteSpace(
                result.VehicleCompatibilityId) &&
            !string.IsNullOrWhiteSpace(result.VehiclePath))
        {
            var normalizedVehiclePath =
                result.VehiclePath.Trim();
            var compatibilityId =
                ResolveOpenOmsiVehicleCompatibilityId(
                    normalizedVehiclePath);
            _openOmsiCachedVehiclePath =
                normalizedVehiclePath;
            _openOmsiCachedVehicleCompatibilityId =
                compatibilityId;

            if (!string.IsNullOrWhiteSpace(compatibilityId))
            {
                result = result with
                {
                    VehicleCompatibilityId = compatibilityId
                };
            }
        }

        if (string.IsNullOrWhiteSpace(
                result.MapCompatibilityId))
        {
            var gatewayMap =
                OpenOmsiLanGateway.Shared.GetStatus().Map;
            var mapReference =
                string.IsNullOrWhiteSpace(gatewayMap)
                    ? result.MapName
                    : gatewayMap;

            var normalizedMapReference =
                mapReference?.Trim();
            var cachedMapReferenceMatches =
                string.Equals(
                    _openOmsiCachedMapReference,
                    normalizedMapReference,
                    StringComparison.OrdinalIgnoreCase);
            var compatibilityId =
                cachedMapReferenceMatches
                    ? _openOmsiCachedMapCompatibilityId
                    : null;

            if (!cachedMapReferenceMatches &&
                !string.IsNullOrWhiteSpace(
                    normalizedMapReference))
            {
                compatibilityId =
                    OmsiMapCatalog.TryFingerprintInstalledMap(
                        _openOmsiVehicleIdentityRoots,
                        normalizedMapReference);
                _openOmsiCachedMapReference =
                    normalizedMapReference;
                _openOmsiCachedMapCompatibilityId =
                    compatibilityId;
            }

            if (!string.IsNullOrWhiteSpace(compatibilityId))
            {
                result = result with
                {
                    MapCompatibilityId = compatibilityId
                };
            }
        }

        return result;
    }

    private string? ResolveOpenOmsiVehicleCompatibilityId(
        string? vehiclePath)
    {
        var normalized =
            vehiclePath?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (_openOmsiVehicleFingerprintCache.TryGetValue(
                normalized,
                out var cached))
        {
            return cached;
        }

        string? compatibilityId = null;
        foreach (var root in _openOmsiVehicleIdentityRoots)
        {
            compatibilityId =
                OmsiVehicleIdentityReader
                    .TryFingerprintInstalledVehicle(
                        root,
                        normalized);
            if (!string.IsNullOrWhiteSpace(compatibilityId))
            {
                break;
            }
        }

        _openOmsiVehicleFingerprintCache[normalized] =
            compatibilityId;
        return compatibilityId;
    }

    private void RefreshOpenOmsiVehicleIdentityRoots()
    {
        try
        {
            var verification =
                OpenOmsiPluginInstallationService.Verify();
            var roots =
                new List<string>(
                    OpenOmsiPluginInstallationService
                        .ResolveContentSearchRoots(
                            verification.ExecutablePath));

            foreach (var profile in
                     OmsiInstallationProfileStore.Load())
            {
                if (string.IsNullOrWhiteSpace(
                        profile.InstallDirectory) ||
                    !Directory.Exists(
                        profile.InstallDirectory))
                {
                    continue;
                }

                var full =
                    Path.TrimEndingDirectorySeparator(
                        Path.GetFullPath(
                            profile.InstallDirectory));
                if (!roots.Contains(
                        full,
                        StringComparer.OrdinalIgnoreCase))
                {
                    roots.Add(full);
                }
            }

            _openOmsiVehicleIdentityRoots = roots
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
            ClearOpenOmsiCompatibilityIdentityCache();
        }
        catch
        {
            _openOmsiVehicleIdentityRoots = [];
            ClearOpenOmsiCompatibilityIdentityCache();
        }
    }

    private void ClearOpenOmsiCompatibilityIdentityCache()
    {
        _openOmsiCachedVehiclePath = null;
        _openOmsiCachedVehicleCompatibilityId = null;
        _openOmsiVehicleFingerprintCache.Clear();
        _openOmsiCachedMapReference = null;
        _openOmsiCachedMapCompatibilityId = null;
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
            telemetry =
                EnrichOpenOmsiCompatibilityIdentity(telemetry);
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
