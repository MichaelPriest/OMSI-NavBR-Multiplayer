using NavBR.Client.Maps;
using NavBR.Client.Omsi;
using NavBR.Shared.Telemetry;

namespace NavBR.Client;

public partial class MainWindow
{
    internal VehicleTelemetry? GetCurrentTelemetryForAlpha11() => _lastTelemetry;

    internal OmsiProcessInfo? GetCurrentOmsiProcessForAlpha11() => _currentOmsi;

    internal IReadOnlyList<OmsiMapInfo> GetMapsForAlpha11Tools() => _installedMaps;
}
