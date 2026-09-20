using System.Windows;
using System.Windows.Threading;
using NavBR.Client.Mobile;
using NavBR.Client.Multiplayer;

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

    internal Task<object> ExecuteMobileCompanionCommandAsync(MobileCompanionCommand command)
    {
        if (Dispatcher.CheckAccess())
        {
            return Task.FromResult(ExecuteMobileCompanionCommand(command));
        }

        return Dispatcher.InvokeAsync(() => ExecuteMobileCompanionCommand(command)).Task;
    }

    private object BuildMobileCompanionState()
    {
        var telemetry = _lastTelemetry;
        var app = Application.Current as App;
        var plugin = app?.PluginBridge.GetConnectionInfo();
        var capabilities = plugin?.LastCapabilities?.Capabilities
            ?? plugin?.LastStatus?.Capabilities
            ?? Array.Empty<string>();

        var detectedVehicleEvents = telemetry is null
            ? Array.Empty<string>()
            : OmsiVehicleInteractionCatalog.Read(
                ResolveConfiguredOmsiRootForPlugin(),
                telemetry.VehiclePath)
                .Take(128)
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

        return new
        {
            schema = "navbr-mobile-state",
            version = 2,
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
            vehicle,
            vehicleControls = new
            {
                writable = false,
                writeReason = "local-player-vehicle-command-capability-not-implemented",
                detectedEvents = detectedVehicleEvents
            },
            navigation = BuildWebNavigationState(),
            multiplayer = BuildWebMultiplayerState(),
            ibis = new
            {
                available = telemetry?.IsInGame == true,
                writable = false,
                writeReason = "plugin-bridge-ibis-capability-not-implemented",
                line = telemetry?.Line,
                route = telemetry?.Route,
                destination = telemetry?.DestinationName,
                hof = telemetry?.HofName,
                nextStop = telemetry?.NextStopName,
                delaySeconds = telemetry?.DelaySeconds
            }
        };
    }

    private object ExecuteMobileCompanionCommand(MobileCompanionCommand command)
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
                if (_multiplayerWindow is null || string.IsNullOrWhiteSpace(command.PlayerId))
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

            default:
                return MobileCommandResult(false, action, "unsupported-command");
        }
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
        string? error = null) =>
        new
        {
            success,
            action,
            error,
            timestampUtc = DateTimeOffset.UtcNow
        };

    private static object BuildMobileCompanionDesktopState()
    {
        var host = (Application.Current as App)?.MobileCompanion;
        return new
        {
            running = host?.IsRunning == true,
            port = host?.Port ?? MobileCompanionHostService.DefaultPort,
            discoveryPort = MobileCompanionHostService.DiscoveryPort,
            pairingCode = host?.PairingCode,
            urls = host?.AccessUrls ?? Array.Empty<string>(),
            mode = "lan-auto-discovery"
        };
    }
}
