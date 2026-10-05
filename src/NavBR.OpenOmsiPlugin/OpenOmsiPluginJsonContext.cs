using System.Text.Json.Serialization;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.OpenOmsiPlugin;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PluginBridgeMessage))]
[JsonSerializable(typeof(TrafficVehicleState[]))]
[JsonSerializable(typeof(OpenOmsiNearbyVehicleState[]))]
[JsonSerializable(typeof(VehicleSectionPose[]))]
internal partial class OpenOmsiPluginJsonContext : JsonSerializerContext
{
}
