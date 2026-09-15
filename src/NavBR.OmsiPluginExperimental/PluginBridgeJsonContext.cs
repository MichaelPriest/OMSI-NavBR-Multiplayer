using System.Text.Json.Serialization;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PluginBridgeMessage))]
[JsonSerializable(typeof(TrafficVehicleState[]))]
internal partial class PluginBridgeJsonContext : JsonSerializerContext
{
}
