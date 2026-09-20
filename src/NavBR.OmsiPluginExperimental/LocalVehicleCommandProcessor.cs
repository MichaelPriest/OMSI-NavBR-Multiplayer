using System.Runtime.InteropServices;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

internal static class LocalVehicleCommandProcessor
{
    public static bool ExperimentalWritesEnabled =>
        ExperimentalFeatureFlags.MobileVehicleControlsEnabled;

    public static bool IsRuntimeSupported =>
        OmsiNativeInterop.IsRoleplayShimReady;

    public static bool IsCommandType(string type) =>
        string.Equals(
            type,
            PluginBridgeProtocol.TriggerLocalVehicle,
            StringComparison.Ordinal);

    public static bool TryRejectBeforeOmsiThread(
        PluginBridgeMessage command,
        out PluginBridgeMessage? rejection)
    {
        rejection = Validate(command);
        return rejection is not null;
    }

    public static PluginBridgeMessage ProcessOnOmsiThread(
        PluginBridgeMessage command)
    {
        var rejection = Validate(command);
        if (rejection is not null)
        {
            return rejection;
        }

        try
        {
            var vehicle = OmsiNativeInterop.GetPlayerVehiclePointer();
            if (vehicle == 0 ||
                OmsiNativeInterop.IsRoadVehiclePointer(vehicle) != 1)
            {
                return Result(
                    command,
                    false,
                    "player-vehicle-unavailable",
                    "OMSI did not expose a valid local player RoadVehicle.");
            }

            if (!TryNormalizeTriggerName(command.TriggerName, out var triggerName) ||
                command.TriggerActive is not bool active)
            {
                return Result(
                    command,
                    false,
                    "invalid-local-trigger",
                    "A bounded trigger name and boolean state are required.");
            }

            var triggerPointer = Marshal.StringToHGlobalAnsi(triggerName);
            try
            {
                if (OmsiNativeInterop.TriggerRoadVehicle(
                        vehicle,
                        triggerPointer,
                        active ? 1 : 0) != 1)
                {
                    return Result(
                        command,
                        false,
                        "local-trigger-rejected",
                        "OMSI rejected the guarded local vehicle trigger.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(triggerPointer);
            }

            return Result(command, true);
        }
        catch (Exception ex)
        {
            return Result(
                command,
                false,
                "local-trigger-error",
                ex.Message);
        }
    }

    private static PluginBridgeMessage? Validate(PluginBridgeMessage command)
    {
        if (!IsCommandType(command.Type))
        {
            return Result(
                command,
                false,
                "unsupported-local-command",
                "Unsupported local vehicle command.");
        }

        if (string.IsNullOrWhiteSpace(command.CommandId) ||
            command.CommandId.Length > 128)
        {
            return Result(
                command,
                false,
                "invalid-command-id",
                "Local vehicle commands require a bounded command id.");
        }

        if (!IsRuntimeSupported)
        {
            return Result(
                command,
                false,
                "local-trigger-backend-unavailable",
                "The guarded OMSI local vehicle trigger backend is unavailable.");
        }

        if (!ExperimentalWritesEnabled)
        {
            return Result(
                command,
                false,
                "local-trigger-disabled",
                "Mobile local-vehicle controls are disabled.");
        }

        if (!TryNormalizeTriggerName(command.TriggerName, out _) ||
            command.TriggerActive is not bool)
        {
            return Result(
                command,
                false,
                "invalid-local-trigger",
                "A bounded trigger name and boolean state are required.");
        }

        return null;
    }

    private static bool TryNormalizeTriggerName(
        string? value,
        out string triggerName)
    {
        triggerName = value?.Trim() ?? string.Empty;
        if (triggerName.Length is <= 0 or > 128)
        {
            return false;
        }

        foreach (var character in triggerName)
        {
            if (char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }

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
            TriggerName: command.TriggerName,
            TriggerActive: command.TriggerActive,
            ExperimentalWritesEnabled: ExperimentalWritesEnabled,
            Success: success,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
}
