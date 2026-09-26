using System.Windows;
using System.Windows.Threading;
using NavBR.Client.Mobile;
using NavBR.Client.Multiplayer;
using NavBR.Client.PluginBridge;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.Client;

public partial class MainWindow
{
    private DispatcherTimer? _mobilePttLeaseTimer;

    internal Task<object> BuildMobileCompanionStateAsync()
    {
        if (Dispatcher.CheckAccess())
        {
            return Task.FromResult(BuildMobileCompanionState());
        }

        return Dispatcher.InvokeAsync(BuildMobileCompanionState).Task;
    }

    internal async Task<object> ExecuteMobileCompanionCommandAsync(
        MobileCompanionCommand command)
    {
        if (!Dispatcher.CheckAccess())
        {
            var operation = Dispatcher.InvokeAsync(
                () => ExecuteMobileCompanionCommandAsync(command));
            return await await operation.Task;
        }

        return await ExecuteMobileCompanionCommandCoreAsync(command);
    }

    private object BuildMobileCompanionState()
    {
        var telemetry = _lastTelemetry;
        var app = Application.Current as App;
        var plugin = app?.PluginBridge.GetConnectionInfo();
        var capabilities = plugin?.LastCapabilities?.Capabilities
            ?? plugin?.LastStatus?.Capabilities
            ?? Array.Empty<string>();
        var localVehicleTriggerAvailable =
            app?.PluginBridge.SupportsCapability(
                PluginBridgeProtocol.CapabilityLocalVehicleTrigger) == true;
        var localVehicleControlsEnabled =
            ExperimentalFeatureFlags.MobileVehicleControlsEnabled;
        var operational =
            LocalOmsiOperationalSnapshotStore.Latest;
        var operationalFresh =
            operational is not null &&
            DateTimeOffset.UtcNow - operational.CapturedAtUtc <=
                TimeSpan.FromSeconds(2);
        var hudSettings = MultiplayerSettingsStore.Load();

        var detectedVehicleEvents = telemetry is null
            ? Array.Empty<string>()
            : OmsiVehicleInteractionCatalog.Read(
                ResolveConfiguredOmsiRootForPlugin(),
                telemetry.VehiclePath)
                .Take(128)
                .ToArray();

        var detectedIbisEvents = telemetry is null
            ? Array.Empty<string>()
            : OmsiVehicleInteractionCatalog.ReadIbisEvents(
                ResolveConfiguredOmsiRootForPlugin(),
                telemetry.VehiclePath)
                .Take(64)
                .ToArray();

        object? vehicle = telemetry is null ? null : new
        {
            telemetry.MapName,
            telemetry.VehicleName,
            telemetry.VehiclePath,
            telemetry.Line,
            telemetry.Route,
            telemetry.DestinationName,
            telemetry.NextStopName,
            telemetry.HofName,
            telemetry.SpeedKph,
            telemetry.HeadingDegrees,
            telemetry.DelaySeconds,
            telemetry.CurrentStreetName,
            telemetry.CurrentStopIndex,
            telemetry.IsInGame,
            telemetry.FuelPercent,
            telemetry.AccelerationMps2,
            telemetry.ThrottlePercent,
            telemetry.BrakePercent,
            telemetry.SteeringDegrees,
            doors = telemetry.Doors.ToString(),
            lights = telemetry.Lights.ToString(),
            turnSignal = telemetry.TurnSignal.ToString(),
            telemetry.HornActive,
            telemetry.WipersActive,
            telemetry.ParkingBrakeActive,
            telemetry.ReverseGear,
            telemetry.StopRequested
        };

        var controlsWritable =
            telemetry?.IsInGame == true &&
            localVehicleControlsEnabled &&
            localVehicleTriggerAvailable &&
            detectedVehicleEvents.Length > 0;

        var ibisWritable =
            controlsWritable &&
            detectedIbisEvents.Length > 0;

        return new
        {
            schema = "navbr-mobile-state",
            version = 3,
            generatedAtUtc = DateTimeOffset.UtcNow,
            omsi = new
            {
                detected = _currentOmsi is not null,
                inGame = telemetry?.IsInGame == true,
                mapName = telemetry?.MapName
            },
            plugin = new
            {
                connected = plugin?.IsConnected == true,
                version = plugin?.PluginComponentVersion,
                capabilities
            },
            hud = new
            {
                enabled = hudSettings.HudEnabled,
                telematrixEnabled = hudSettings.TelematrixWidgetEnabled,
                telematrixTheme = hudSettings.TelematrixTheme,
                telematrixSize = hudSettings.TelematrixSize,
                telematrixAutoDirection = hudSettings.TelematrixAutoDirection,
                telematrixManualLine = hudSettings.TelematrixManualLine,
                telematrixManualDirection = hudSettings.TelematrixManualDirection
            },
            operation = new
            {
                available = operationalFresh,
                capturedAtUtc = operationalFresh
                    ? operational?.CapturedAtUtc
                    : null,
                cabinTemperatureC = operationalFresh
                    ? operational?.CabinTemperatureC
                    : null,
                passengerCount = operationalFresh
                    ? operational?.PassengerCount
                    : null,
                scheduleActive = operationalFresh
                    ? operational?.ScheduleActive
                    : null,
                simulationTime = operationalFresh
                    ? operational?.SimulationTime
                    : null,
                simulationDay = operationalFresh
                    ? operational?.SimulationDay
                    : null,
                simulationMonth = operationalFresh
                    ? operational?.SimulationMonth
                    : null,
                simulationYear = operationalFresh
                    ? operational?.SimulationYear
                    : null,
                simulationPaused = operationalFresh
                    ? operational?.SimulationPaused
                    : null,
                ibisLineCourse = operationalFresh
                    ? operational?.IbisLineCourse
                    : null,
                ibisRouteCode = operationalFresh
                    ? operational?.IbisRouteCode
                    : null,
                ibisTerminusName = operationalFresh
                    ? operational?.IbisTerminusName
                    : null,
                ibisDelayMinutes = operationalFresh
                    ? operational?.IbisDelayMinutes
                    : null,
                ibisDelaySeconds = operationalFresh
                    ? operational?.IbisDelaySeconds
                    : null,
                ibisDelayState = operationalFresh
                    ? operational?.IbisDelayState
                    : null
            },
            vehicle,
            vehicleControls = new
            {
                writable = controlsWritable,
                enabled = localVehicleControlsEnabled,
                capabilityAvailable = localVehicleTriggerAvailable,
                writeReason = controlsWritable
                    ? null
                    : !localVehicleControlsEnabled
                        ? "mobile-local-vehicle-controls-disabled"
                        : !localVehicleTriggerAvailable
                            ? "plugin-bridge-local-vehicle-trigger-unavailable"
                            : detectedVehicleEvents.Length == 0
                                ? "no-real-vehicle-events-detected"
                                : "player-vehicle-unavailable",
                detectedEvents = detectedVehicleEvents
            },
            navigation = BuildWebNavigationState(),
            multiplayer = BuildWebMultiplayerState(),
            ibis = new
            {
                available = telemetry?.IsInGame == true,
                writable = ibisWritable,
                writeReason = ibisWritable
                    ? null
                    : !localVehicleControlsEnabled
                        ? "mobile-local-vehicle-controls-disabled"
                        : !localVehicleTriggerAvailable
                            ? "plugin-bridge-local-vehicle-trigger-unavailable"
                            : detectedIbisEvents.Length == 0
                                ? "no-real-ibis-events-detected"
                                : "player-vehicle-unavailable",
                controlMode = "real-mouseevent-trigger",
                detectedEvents = detectedIbisEvents,
                line = telemetry?.Line,
                route = telemetry?.Route,
                destination = telemetry?.DestinationName,
                hof = telemetry?.HofName,
                nextStop = telemetry?.NextStopName,
                delaySeconds = telemetry?.DelaySeconds
            }
        };
    }

    private async Task<object> ExecuteMobileCompanionCommandCoreAsync(
        MobileCompanionCommand command)
    {
        var action = command.Action.Trim().ToLowerInvariant();

        switch (action)
        {
            case "voice-enabled":
                OpenMultiplayerCentralForShell(showWindow: false);
                if (_multiplayerWindow is null)
                {
                    return MobileCommandResult(false, action, "multiplayer-unavailable");
                }

                _multiplayerWindow.SetVoiceEnabledFromWeb(command.Enabled == true);
                return MobileCommandResult(true, action);

            case "voice-configure":
                OpenMultiplayerCentralForShell(showWindow: false);
                if (_multiplayerWindow is null)
                {
                    return MobileCommandResult(false, action, "multiplayer-unavailable");
                }

                _multiplayerWindow.ConfigureVoiceFromWeb(
                    command.Channel,
                    command.ProximityMeters,
                    command.Deafened == true);
                return MobileCommandResult(true, action);

            case "voice-remote":
                OpenMultiplayerCentralForShell(showWindow: false);
                if (_multiplayerWindow is null ||
                    string.IsNullOrWhiteSpace(command.PlayerId))
                {
                    return MobileCommandResult(false, action, "player-required");
                }

                _multiplayerWindow.ConfigureRemoteVoiceFromWeb(
                    command.PlayerId,
                    command.Muted == true,
                    command.Gain);
                return MobileCommandResult(true, action);

            case "voice-ptt":
                OpenMultiplayerCentralForShell(showWindow: false);
                if (_multiplayerWindow is null)
                {
                    return MobileCommandResult(false, action, "multiplayer-unavailable");
                }

                ApplyMobilePttLease(command.Active == true);
                return MobileCommandResult(true, action);

            case "hud-enabled":
            {
                var enabled = command.Enabled == true;
                var current = MultiplayerSettingsStore.Load();
                MultiplayerSettingsStore.Save(
                    current with
                    {
                        HudVisibilitySettingsVersion = 1,
                        HudEnabled = enabled
                    });
                var hud = EnsureHudOverlay();
                hud.SetHudEnabled(enabled);
                return MobileCommandResult(true, action);
            }

            case "telematrix-configure":
            {
                var current = MultiplayerSettingsStore.Load();
                var theme = command.Theme is int requestedTheme
                    ? Math.Clamp(requestedTheme, 0, 2)
                    : current.TelematrixTheme;
                var size = command.Size is int requestedSize
                    ? Math.Clamp(requestedSize, 0, 2)
                    : current.TelematrixSize;
                var direction =
                    string.Equals(
                        command.Direction,
                        "TS",
                        StringComparison.OrdinalIgnoreCase)
                        ? "TS"
                        : "TP";

                MultiplayerSettingsStore.Save(
                    current with
                    {
                        TelematrixWidgetEnabled =
                            command.Enabled ?? current.TelematrixWidgetEnabled,
                        TelematrixTheme = theme,
                        TelematrixSize = size,
                        TelematrixAutoDirection =
                            command.AutoDirection ??
                            current.TelematrixAutoDirection,
                        TelematrixManualLine =
                            string.IsNullOrWhiteSpace(command.Line)
                                ? current.TelematrixManualLine
                                : command.Line.Trim(),
                        TelematrixManualDirection =
                            string.IsNullOrWhiteSpace(command.Direction)
                                ? current.TelematrixManualDirection
                                : direction
                    });
                _ = EnsureHudOverlay();
                return MobileCommandResult(true, action);
            }

            case "vehicle-trigger":
                return await ExecuteMobileVehicleTriggerAsync(command, action);

            case "ibis-trigger":
                return await ExecuteMobileVehicleTriggerAsync(
                    command,
                    action,
                    ibisOnly: true);

            default:
                return MobileCommandResult(false, action, "unsupported-command");
        }
    }

    private async Task<object> ExecuteMobileVehicleTriggerAsync(
        MobileCompanionCommand command,
        string action,
        bool ibisOnly = false)
    {
        var telemetry = _lastTelemetry;
        var triggerName = command.TriggerName?.Trim();
        if (telemetry?.IsInGame != true ||
            string.IsNullOrWhiteSpace(telemetry.VehiclePath))
        {
            return MobileCommandResult(false, action, "player-vehicle-unavailable");
        }

        if (!ExperimentalFeatureFlags.MobileVehicleControlsEnabled)
        {
            return MobileCommandResult(false, action, "mobile-local-vehicle-controls-disabled");
        }

        if (Application.Current is not App app ||
            !app.PluginBridge.SupportsCapability(
                PluginBridgeProtocol.CapabilityLocalVehicleTrigger))
        {
            return MobileCommandResult(false, action, "plugin-bridge-local-vehicle-trigger-unavailable");
        }

        if (string.IsNullOrWhiteSpace(triggerName) ||
            triggerName.Length > 128)
        {
            return MobileCommandResult(false, action, "invalid-trigger");
        }

        var detectedEvents = ibisOnly
            ? OmsiVehicleInteractionCatalog.ReadIbisEvents(
                ResolveConfiguredOmsiRootForPlugin(),
                telemetry.VehiclePath)
            : OmsiVehicleInteractionCatalog.Read(
                ResolveConfiguredOmsiRootForPlugin(),
                telemetry.VehiclePath);
        if (!detectedEvents.Contains(triggerName, StringComparer.Ordinal))
        {
            return MobileCommandResult(
                false,
                action,
                ibisOnly
                    ? "ibis-trigger-not-in-real-vehicle-catalog"
                    : "trigger-not-in-real-vehicle-catalog");
        }

        var result = await OmsiPluginBridgeRelay.SetLocalVehicleTriggerAsync(
            telemetry.PlayerId,
            triggerName,
            command.Active == true);

        return result?.Success == true
            ? MobileCommandResult(true, action)
            : MobileCommandResult(
                false,
                action,
                result?.ErrorCode ?? "local-vehicle-trigger-failed",
                result?.ErrorMessage);
    }

    private void ApplyMobilePttLease(bool active)
    {
        if (_multiplayerWindow is null)
        {
            return;
        }

        _mobilePttLeaseTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1600)
        };
        _mobilePttLeaseTimer.Tick -= MobilePttLeaseTimer_Tick;
        _mobilePttLeaseTimer.Tick += MobilePttLeaseTimer_Tick;
        _mobilePttLeaseTimer.Stop();

        _multiplayerWindow.SetPushToTalk(active);
        if (active)
        {
            _mobilePttLeaseTimer.Start();
        }
    }

    private void MobilePttLeaseTimer_Tick(object? sender, EventArgs e)
    {
        _mobilePttLeaseTimer?.Stop();
        _multiplayerWindow?.SetPushToTalk(false);
    }

    private static object MobileCommandResult(
        bool success,
        string action,
        string? error = null,
        string? detail = null) =>
        new
        {
            success,
            action,
            error,
            detail,
            timestampUtc = DateTimeOffset.UtcNow
        };

    private static object BuildMobileCompanionDesktopState()
    {
        var app = Application.Current as App;
        var host = app?.MobileCompanion;
        return new
        {
            running = host?.IsRunning == true,
            port = host?.Port ?? MobileCompanionHostService.DefaultPort,
            discoveryPort = MobileCompanionHostService.DiscoveryPort,
            pairingCode = host?.PairingCode,
            urls = host?.AccessUrls ?? Array.Empty<string>(),
            mode = "lan-auto-discovery",
            vehicleControlsEnabled = ExperimentalFeatureFlags.MobileVehicleControlsEnabled,
            vehicleControlsAvailable =
                app?.PluginBridge.SupportsCapability(
                    PluginBridgeProtocol.CapabilityLocalVehicleTrigger) == true
        };
    }
}
