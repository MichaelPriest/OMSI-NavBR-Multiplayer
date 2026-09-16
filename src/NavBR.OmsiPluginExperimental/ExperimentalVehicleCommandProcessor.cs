using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

internal static class ExperimentalVehicleCommandProcessor
{
    private static readonly bool WritesEnabled =
        string.Equals(
            Environment.GetEnvironmentVariable("NAVBR_OMSI_EXPERIMENTAL_WRITES"),
            "1",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            Environment.GetEnvironmentVariable("NAVBR_OMSI_EXPERIMENTAL_WRITES"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static bool ExperimentalWritesEnabled => WritesEnabled;

    public static string[] GetCapabilities()
    {
        var capabilities = new List<string>
        {
            PluginBridgeProtocol.CapabilityAdvancedTelemetry,
            PluginBridgeProtocol.CapabilityTimetableState
        };

        // Do not advertise physical capabilities until a validated OMSI 2.3.004
        // write backend is active. The client therefore keeps remote buses on the
        // NavBR GPS/HUD instead of attempting unsafe writes.
        if (WritesEnabled && PhysicalVehicleBackend.IsAvailable)
        {
            capabilities.Add(PluginBridgeProtocol.CapabilityGhostReplay);
            capabilities.Add(PluginBridgeProtocol.CapabilityVehicleSpawn);
            capabilities.Add(PluginBridgeProtocol.CapabilityVehicleTransform);
            capabilities.Add(PluginBridgeProtocol.CapabilityVehicleVisualState);
        }

        return capabilities.ToArray();
    }

    /// <summary>
    /// Performs only cheap/fail-closed checks that are safe on the bridge worker.
    /// If this returns false, the command is eligible to be queued for OMSI's
    /// plugin callback thread; no raw simulator function has been called yet.
    /// </summary>
    public static bool TryRejectBeforeOmsiThread(
        PluginBridgeMessage command,
        out PluginBridgeMessage? rejection)
    {
        rejection = ValidateCommand(command);
        return rejection is not null;
    }

    /// <summary>
    /// Executes a validated physical command. This method is only called while
    /// draining <see cref="OmsiThreadCommandQueue"/> from an OMSI callback.
    /// </summary>
    public static PluginBridgeMessage ProcessOnOmsiThread(PluginBridgeMessage command)
    {
        var rejection = ValidateCommand(command);
        if (rejection is not null)
        {
            return rejection;
        }

        try
        {
            return PhysicalVehicleBackend.Execute(command);
        }
        catch (Exception ex)
        {
            return Result(command, false, "backend-error", ex.Message);
        }
    }

    private static PluginBridgeMessage? ValidateCommand(PluginBridgeMessage command)
    {
        if (!IsVehicleCommand(command.Type))
        {
            return Result(command, false, "unsupported-command", "Unsupported physical vehicle command.");
        }

        if (string.IsNullOrWhiteSpace(command.CommandId) || command.CommandId.Length > 128)
        {
            return Result(command, false, "invalid-command-id", "Physical vehicle commands require a bounded command id.");
        }

        if (!WritesEnabled)
        {
            return Result(
                command,
                false,
                "writes-disabled",
                "Physical OMSI writes are disabled. Enable the alpha.11 experimental write mode only for controlled tests.");
        }

        if (!PhysicalVehicleBackend.IsAvailable)
        {
            return Result(
                command,
                false,
                "backend-unavailable",
                "The physical OMSI vehicle backend is not available for this build/runtime.");
        }

        return null;
    }

    private static bool IsVehicleCommand(string type) =>
        string.Equals(type, PluginBridgeProtocol.SpawnRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.UpdateRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.DespawnRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.SpawnGhostVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.UpdateGhostVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.DespawnGhostVehicle, StringComparison.Ordinal);

    internal static PluginBridgeMessage Result(
        PluginBridgeMessage command,
        bool success,
        string? errorCode = null,
        string? errorMessage = null) =>
        new(
            PluginBridgeProtocol.CommandResult,
            PluginBridgeProtocol.Version,
            ProcessId: Environment.ProcessId,
            TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PlayerId: command.PlayerId,
            CommandId: command.CommandId,
            VehicleInstanceId: command.VehicleInstanceId,
            ExperimentalWritesEnabled: WritesEnabled,
            Success: success,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
}

// The interface is intentionally isolated. A validated 2.3.004 backend can be
// implemented without contaminating the safe network/bridge code with raw OMSI
// process writes. Until then IsAvailable remains false and the plugin fails closed.
internal static class PhysicalVehicleBackend
{
    public static bool IsAvailable => false;

    public static PluginBridgeMessage Execute(PluginBridgeMessage command) =>
        ExperimentalVehicleCommandProcessor.Result(
            command,
            false,
            "not-implemented",
            "Physical vehicle backend has not been enabled in this build.");
}
