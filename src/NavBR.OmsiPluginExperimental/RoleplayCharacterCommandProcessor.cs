using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

internal static class RoleplayCharacterCommandProcessor
{
    public static bool ExperimentalWritesEnabled =>
        ExperimentalFeatureFlags.RoleplayCharacterEnabled;

    public static bool IsRuntimeSupported => OmsiNativeInterop.IsShimReady;

    public static bool IsCharacterCommandType(string type) =>
        string.Equals(type, PluginBridgeProtocol.AcquireRoleplayCharacter, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.UpdateRoleplayCharacter, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.ReleaseRoleplayCharacter, StringComparison.Ordinal);

    public static bool TryRejectBeforeOmsiThread(
        PluginBridgeMessage command,
        out PluginBridgeMessage? rejection)
    {
        rejection = ValidateCommand(command);
        return rejection is not null;
    }

    public static PluginBridgeMessage ProcessOnOmsiThread(PluginBridgeMessage command)
    {
        var rejection = ValidateCommand(command);
        if (rejection is not null)
        {
            return rejection;
        }

        try
        {
            return RoleplayCharacterBackend.Execute(command);
        }
        catch (Exception ex)
        {
            return Result(command, false, "character-backend-error", ex.Message);
        }
    }

    private static PluginBridgeMessage? ValidateCommand(PluginBridgeMessage command)
    {
        if (!IsCharacterCommandType(command.Type))
        {
            return Result(command, false, "unsupported-character-command", "Unsupported roleplay character command.");
        }

        if (string.IsNullOrWhiteSpace(command.CommandId) || command.CommandId.Length > 128)
        {
            return Result(command, false, "invalid-command-id", "Roleplay character commands require a bounded command id.");
        }

        if (string.IsNullOrWhiteSpace(command.CharacterInstanceId) ||
            command.CharacterInstanceId.Length > 128)
        {
            return Result(command, false, "invalid-character-id", "CharacterInstanceId is required.");
        }

        if (!IsRuntimeSupported)
        {
            return Result(
                command,
                false,
                "character-backend-unavailable",
                "The OMSI human-control backend is not available for this runtime.");
        }

        var isRelease = string.Equals(
            command.Type,
            PluginBridgeProtocol.ReleaseRoleplayCharacter,
            StringComparison.Ordinal);

        // Release remains available after opt-out so a possessed NPC can always
        // be returned to OMSI safely.
        if (!ExperimentalWritesEnabled && !isRelease)
        {
            return Result(
                command,
                false,
                "roleplay-writes-disabled",
                "Roleplay character control is disabled.");
        }

        return null;
    }

    internal static PluginBridgeMessage Result(
        PluginBridgeMessage command,
        bool success,
        string? errorCode = null,
        string? errorMessage = null,
        int? humanIndex = null,
        float? x = null,
        float? y = null,
        float? z = null,
        float? heading = null,
        float? speed = null) =>
        new(
            PluginBridgeProtocol.CommandResult,
            PluginBridgeProtocol.Version,
            ProcessId: Environment.ProcessId,
            TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PlayerId: command.PlayerId,
            DisplayName: command.DisplayName,
            CommandId: command.CommandId,
            CharacterInstanceId: command.CharacterInstanceId,
            CharacterHumanIndex: humanIndex,
            CharacterActivity: command.CharacterActivity,
            CharacterActive: success &&
                             !string.Equals(
                                 command.Type,
                                 PluginBridgeProtocol.ReleaseRoleplayCharacter,
                                 StringComparison.Ordinal),
            LocalX: x,
            LocalY: y,
            LocalZ: z,
            HeadingDegrees: heading,
            SpeedMps: speed,
            ExperimentalWritesEnabled:
                ExperimentalFeatureFlags.PhysicalVehiclesEnabled ||
                ExperimentalFeatureFlags.RoleplayCharacterEnabled,
            Success: success,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
}

internal static class RoleplayCharacterBackend
{
    private const int MaxOwnedCharacters = 24;
    private const double MaxAcquireDistanceMeters = 45d;
    private const double MaxAcquireHeightDifferenceMeters = 4d;
    private const float MaxCharacterSpeedMps = 6f;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, RoleplayCharacterInstance> Owned =
        new(StringComparer.OrdinalIgnoreCase);

    public static PluginBridgeMessage Execute(PluginBridgeMessage command)
    {
        if (string.Equals(command.Type, PluginBridgeProtocol.AcquireRoleplayCharacter, StringComparison.Ordinal))
        {
            return Acquire(command);
        }

        if (string.Equals(command.Type, PluginBridgeProtocol.UpdateRoleplayCharacter, StringComparison.Ordinal))
        {
            return Update(command);
        }

        if (string.Equals(command.Type, PluginBridgeProtocol.ReleaseRoleplayCharacter, StringComparison.Ordinal))
        {
            return Release(command);
        }

        return RoleplayCharacterCommandProcessor.Result(
            command,
            false,
            "unsupported-character-command",
            "Unsupported roleplay character command.");
    }

    private static PluginBridgeMessage Acquire(PluginBridgeMessage command)
    {
        if (!TryGetCharacterId(command, out var instanceId) ||
            !TryReadAnchor(command, out var anchorX, out var anchorY, out var anchorZ))
        {
            return Fail(command, "invalid-character-anchor", "A finite local OMSI anchor is required.");
        }

        lock (Sync)
        {
            if (Owned.TryGetValue(instanceId, out var existing))
            {
                return BuildCurrentState(command, existing);
            }

            if (Owned.Count >= MaxOwnedCharacters)
            {
                return Fail(command, "character-limit-reached", "The roleplay character ownership limit was reached.");
            }

            if (!OmsiNativeInterop.TrySnapshotHumans(out var humans) || humans.Length == 0)
            {
                return Fail(command, "no-humans", "OMSI has no readable human instances.");
            }

            var usedPointers = Owned.Values
                .Select(value => value.HumanPointer)
                .ToHashSet();

            var bestPointer = 0;
            var bestIndex = -1;
            var bestDistance = double.MaxValue;
            float bestX = 0f;
            float bestY = 0f;
            float bestZ = 0f;
            float bestHeading = 0f;
            float bestSpeed = 0f;

            for (var index = 0; index < humans.Length; index++)
            {
                var pointer = humans[index];
                if (pointer == 0 ||
                    usedPointers.Contains(pointer) ||
                    OmsiNativeInterop.IsHumanControllable(pointer) != 1 ||
                    OmsiNativeInterop.ReadHumanPose(
                        pointer,
                        out var x,
                        out var y,
                        out var z,
                        out var heading,
                        out var speed) != 1)
                {
                    continue;
                }

                var dz = Math.Abs(z - anchorZ);
                if (dz > MaxAcquireHeightDifferenceMeters)
                {
                    continue;
                }

                // OMSI uses X/Y as the ground plane and Z as height.
                var dx = x - anchorX;
                var dy = y - anchorY;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (!double.IsFinite(distance) ||
                    distance > MaxAcquireDistanceMeters ||
                    distance >= bestDistance)
                {
                    continue;
                }

                bestPointer = pointer;
                bestIndex = index;
                bestDistance = distance;
                bestX = x;
                bestY = y;
                bestZ = z;
                bestHeading = heading;
                bestSpeed = speed;
            }

            if (bestPointer == 0)
            {
                return Fail(
                    command,
                    "no-controllable-human-nearby",
                    "No free, in-world OMSI human was found near the requested position.");
            }

            if (OmsiNativeInterop.ReadHumanAiState(
                    bestPointer,
                    out var aiMode,
                    out var aiModeEx,
                    out var aiSubMode,
                    out var sollSpeed,
                    out var actSpeed) != 1)
            {
                return Fail(command, "human-ai-read-failed", "Could not snapshot the NPC AI state.");
            }

            if (OmsiNativeInterop.SetHumanTransform(
                    bestPointer,
                    bestX,
                    bestY,
                    bestZ,
                    NormalizeHeading(bestHeading),
                    0f) != 1)
            {
                return Fail(command, "human-control-failed", "OMSI rejected the initial NPC possession state.");
            }

            var instance = new RoleplayCharacterInstance(
                instanceId,
                bestPointer,
                bestIndex,
                aiMode,
                aiModeEx,
                aiSubMode,
                sollSpeed,
                actSpeed,
                DateTimeOffset.UtcNow);

            Owned[instanceId] = instance;

            return RoleplayCharacterCommandProcessor.Result(
                command,
                true,
                humanIndex: bestIndex,
                x: bestX,
                y: bestY,
                z: bestZ,
                heading: NormalizeHeading(bestHeading),
                speed: 0f);
        }
    }

    private static PluginBridgeMessage Update(PluginBridgeMessage command)
    {
        if (!TryGetCharacterId(command, out var instanceId))
        {
            return Fail(command, "invalid-character-id", "CharacterInstanceId is required.");
        }

        lock (Sync)
        {
            if (!Owned.TryGetValue(instanceId, out var instance))
            {
                return Fail(command, "character-not-owned", "The requested NPC is not owned by NavBR.");
            }

            if (OmsiNativeInterop.IsHumanPointer(instance.HumanPointer) != 1)
            {
                Owned.Remove(instanceId);
                return Fail(command, "character-pointer-stale", "The possessed OMSI human no longer exists.");
            }

            if (!TryReadPose(command, out var x, out var y, out var z, out var heading, out var speed))
            {
                return Fail(command, "invalid-character-pose", "Character updates require a finite local pose.");
            }

            if (OmsiNativeInterop.SetHumanTransform(
                    instance.HumanPointer,
                    x,
                    y,
                    z,
                    heading,
                    speed) != 1)
            {
                return Fail(command, "character-transform-failed", "OMSI rejected the guarded human transform.");
            }

            return RoleplayCharacterCommandProcessor.Result(
                command,
                true,
                humanIndex: instance.HumanIndex,
                x: x,
                y: y,
                z: z,
                heading: heading,
                speed: speed);
        }
    }

    private static PluginBridgeMessage Release(PluginBridgeMessage command)
    {
        if (!TryGetCharacterId(command, out var instanceId))
        {
            return Fail(command, "invalid-character-id", "CharacterInstanceId is required.");
        }

        lock (Sync)
        {
            if (!Owned.Remove(instanceId, out var instance))
            {
                return RoleplayCharacterCommandProcessor.Result(command, true);
            }

            if (OmsiNativeInterop.IsHumanPointer(instance.HumanPointer) == 1)
            {
                _ = OmsiNativeInterop.RestoreHumanAiState(
                    instance.HumanPointer,
                    instance.AiMode,
                    instance.AiModeEx,
                    instance.AiSubMode,
                    instance.SollSpeed,
                    instance.ActSpeed);
            }

            return RoleplayCharacterCommandProcessor.Result(command, true);
        }
    }

    public static void ReleaseAllBestEffort()
    {
        lock (Sync)
        {
            foreach (var instance in Owned.Values)
            {
                if (OmsiNativeInterop.IsHumanPointer(instance.HumanPointer) == 1)
                {
                    _ = OmsiNativeInterop.RestoreHumanAiState(
                        instance.HumanPointer,
                        instance.AiMode,
                        instance.AiModeEx,
                        instance.AiSubMode,
                        instance.SollSpeed,
                        instance.ActSpeed);
                }
            }

            Owned.Clear();
        }
    }

    private static PluginBridgeMessage BuildCurrentState(
        PluginBridgeMessage command,
        RoleplayCharacterInstance instance)
    {
        if (OmsiNativeInterop.ReadHumanPose(
                instance.HumanPointer,
                out var x,
                out var y,
                out var z,
                out var heading,
                out var speed) != 1)
        {
            return Fail(command, "character-pose-read-failed", "Could not read the possessed NPC pose.");
        }

        return RoleplayCharacterCommandProcessor.Result(
            command,
            true,
            humanIndex: instance.HumanIndex,
            x: x,
            y: y,
            z: z,
            heading: NormalizeHeading(heading),
            speed: Math.Clamp(Math.Abs(speed), 0f, MaxCharacterSpeedMps));
    }

    private static bool TryReadAnchor(
        PluginBridgeMessage command,
        out float x,
        out float y,
        out float z)
    {
        x = 0f;
        y = 0f;
        z = 0f;

        if (command.LocalX is not double dx ||
            command.LocalY is not double dy ||
            command.LocalZ is not double dz ||
            !double.IsFinite(dx) ||
            !double.IsFinite(dy) ||
            !double.IsFinite(dz) ||
            Math.Abs(dx) > 100000d ||
            Math.Abs(dy) > 100000d ||
            Math.Abs(dz) > 100000d)
        {
            return false;
        }

        x = (float)dx;
        y = (float)dy;
        z = (float)dz;
        return true;
    }

    private static bool TryReadPose(
        PluginBridgeMessage command,
        out float x,
        out float y,
        out float z,
        out float heading,
        out float speed)
    {
        x = 0f;
        y = 0f;
        z = 0f;
        heading = 0f;
        speed = 0f;

        if (!TryReadAnchor(command, out x, out y, out z) ||
            command.HeadingDegrees is not double headingValue ||
            command.SpeedMps is not double speedValue ||
            !double.IsFinite(headingValue) ||
            !double.IsFinite(speedValue) ||
            speedValue < 0d ||
            speedValue > MaxCharacterSpeedMps)
        {
            return false;
        }

        heading = NormalizeHeading((float)headingValue);
        speed = (float)speedValue;
        return true;
    }

    private static bool TryGetCharacterId(PluginBridgeMessage command, out string instanceId)
    {
        instanceId = command.CharacterInstanceId?.Trim() ?? string.Empty;
        return instanceId.Length is > 0 and <= 128;
    }

    private static float NormalizeHeading(float value)
    {
        value %= 360f;
        return value < 0f ? value + 360f : value;
    }

    private static PluginBridgeMessage Fail(
        PluginBridgeMessage command,
        string code,
        string message) =>
        RoleplayCharacterCommandProcessor.Result(command, false, code, message);

    private sealed record RoleplayCharacterInstance(
        string InstanceId,
        int HumanPointer,
        int HumanIndex,
        byte AiMode,
        byte AiModeEx,
        byte AiSubMode,
        float SollSpeed,
        float ActSpeed,
        DateTimeOffset AcquiredAtUtc);
}
