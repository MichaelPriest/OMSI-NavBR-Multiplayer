using NavBR.Shared.Multiplayer;

namespace NavBR.Client;

public partial class MainWindow
{
    internal IReadOnlyList<TrafficVehicleState> GetRoadTrafficForMultiplayer() =>
        _telemetryProvider.ReadRoadTraffic();
}
