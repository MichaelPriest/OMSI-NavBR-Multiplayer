using System.Windows;
using NavBR.Client.Maps;
using NavBR.Client.Omsi;
using NavBR.Shared.Telemetry;

namespace NavBR.Client;

public partial class MainWindow
{
    internal VehicleTelemetry? GetCurrentTelemetryForAlpha11()
    {
        var telemetry = _lastTelemetry;
        if (telemetry is null)
        {
            return null;
        }

        var streetName = OmsiStreetProfileResolver.Resolve(
            GetActiveMapForMultiplayer(),
            telemetry);
        if (!string.IsNullOrWhiteSpace(streetName))
        {
            telemetry = telemetry with
            {
                CurrentStreetName = streetName
            };
        }

        if (Application.Current is not App app)
        {
            return telemetry;
        }

        var status = app.PluginBridge.GetConnectionInfo().LastStatus;
        if (status?.TimestampUnixMilliseconds is not long timestamp ||
            status.StopRequested is not bool stopRequested)
        {
            return telemetry;
        }

        DateTimeOffset statusTime;
        try
        {
            statusTime = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            return telemetry;
        }

        if (DateTimeOffset.UtcNow - statusTime > TimeSpan.FromSeconds(1.5))
        {
            return telemetry;
        }

        return telemetry with
        {
            StopRequested = stopRequested
        };
    }

    internal OmsiProcessInfo? GetCurrentOmsiProcessForAlpha11() => _currentOmsi;

    internal IReadOnlyList<OmsiMapInfo> GetMapsForAlpha11Tools() => _installedMaps;
}
