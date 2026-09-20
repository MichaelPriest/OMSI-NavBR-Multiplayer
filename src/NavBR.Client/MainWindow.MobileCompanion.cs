using System.Windows;

namespace NavBR.Client;

public partial class MainWindow
{
    internal Task<object> BuildMobileCompanionStateAsync()
    {
        if (Dispatcher.CheckAccess()) return Task.FromResult(BuildMobileCompanionState());
        return Dispatcher.InvokeAsync(BuildMobileCompanionState).Task;
    }

    private object BuildMobileCompanionState()
    {
        var telemetry = _lastTelemetry;
        var plugin = (Application.Current as App)?.PluginBridge.GetConnectionInfo();

        object? vehicle = telemetry is null ? null : new
        {
            telemetry.MapName,
            telemetry.VehicleName,
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
            telemetry.Doors,
            telemetry.Lights,
            telemetry.TurnSignal,
            telemetry.StopRequested
        };

        return new
        {
            schema = "navbr-mobile-state",
            version = 1,
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
                version = plugin?.PluginComponentVersion
            },
            vehicle,
            navigation = BuildWebNavigationState(),
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

    private static object BuildMobileCompanionDesktopState()
    {
        var host = (Application.Current as App)?.MobileCompanion;
        return new
        {
            running = host?.IsRunning == true,
            port = host?.Port ?? Mobile.MobileCompanionHostService.DefaultPort,
            pairingCode = host?.PairingCode,
            urls = host?.AccessUrls ?? Array.Empty<string>(),
            mode = "lan-pwa"
        };
    }
}
