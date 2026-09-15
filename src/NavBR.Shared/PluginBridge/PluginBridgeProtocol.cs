namespace NavBR.Shared.PluginBridge;

public static class PluginBridgeProtocol
{
    public const int Version = 1;
    public const string PipeName = "OMSI.NavBR.Multiplayer.Plugin.v1";
    public const int MaxMessageChars = 16_384;

    public const string PluginHello = "plugin-hello";
    public const string ClientHello = "client-hello";
    public const string RemoteVehicleState = "remote-vehicle-state";
}

public sealed record PluginBridgeMessage(
    string Type,
    int ProtocolVersion,
    int? ProcessId = null,
    string? ComponentVersion = null,
    string? PlayerId = null,
    string? DisplayName = null,
    string? MapName = null,
    string? MapCompatibilityId = null,
    long? TimestampUnixMilliseconds = null,
    double? X = null,
    double? Y = null,
    double? Z = null,
    int? GridX = null,
    int? GridY = null,
    double? TileX = null,
    double? TileY = null,
    double? HeadingDegrees = null,
    double? SpeedKph = null);
