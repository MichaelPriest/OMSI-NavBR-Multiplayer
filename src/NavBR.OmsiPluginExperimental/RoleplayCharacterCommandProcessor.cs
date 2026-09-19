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
        string.Equals(type, PluginBridgeProtocol.ReleaseRoleplayCharacter, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.TriggerRoleplayVehicle, StringComparison.Ordinal);

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

        if (string.Equals(
                command.Type,
                PluginBridgeProtocol.TriggerRoleplayVehicle,
                StringComparison.Ordinal) &&
            (!TryNormalizeTriggerName(command.TriggerName, out _) ||
             command.TriggerActive is not bool))
        {
            return Result(
                command,
                false,
                "invalid-roleplay-trigger",
                "Roleplay vehicle interaction requires a bounded trigger name and boolean state.");
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
        string? errorMessage = null,
        int? humanIndex = null,
        float? x = null,
        float? y = null,
        float? z = null,
        float? heading = null,
        float? speed = null,
        byte? aiMode = null,
        byte? aiModeEx = null,
        byte? aiSubMode = null,
        float? sollSpeed = null,
        float? actSpeed = null,
        float? lastMovedDist = null,
        float? animationState = null,
        byte? activityLeg = null,
        byte? activityArmUmbrella = null,
        byte? activityArmKi = null,
        byte? activityHeadKi = null) =>
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
            TriggerName: command.TriggerName,
            TriggerActive: command.TriggerActive,
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
            CharacterAiMode: aiMode,
            CharacterAiModeEx: aiModeEx,
            CharacterAiSubMode: aiSubMode,
            CharacterSollSpeedMps: sollSpeed,
            CharacterActSpeedMps: actSpeed,
            CharacterLastMovedDistanceMeters: lastMovedDist,
            CharacterAnimationState: animationState,
            CharacterActivityLegRaw: activityLeg,
            CharacterActivityArmUmbrellaRaw: activityArmUmbrella,
            CharacterActivityArmKiRaw: activityArmKi,
            CharacterActivityHeadKiRaw: activityHeadKi,
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
    private const double MaxInteractionDistanceMeters = 8d;
    private const double MaxInteractionHeightDifferenceMeters = 4d;
    private const int MaxRetainedTriggerStrings = 256;

    private static readonly object Sync = new();
    private static readonly Dictionary<string, RoleplayCharacterInstance> Owned =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> RetainedTriggerStrings =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, HashSet<string>> ActiveTriggersByInstance =
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

        if (string.Equals(command.Type, PluginBridgeProtocol.TriggerRoleplayVehicle, StringComparison.Ordinal))
        {
            return TriggerVehicle(command);
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
            !TryReadAnchor(command, out var anchorX, out var anchorY, out var anchorZ) ||
            command.HeadingDegrees is not double busHeadingValue ||
            !double.IsFinite(busHeadingValue) ||
            command.CharacterDefinitionPointer is not int definitionPointer ||
            definitionPointer < 0)
        {
            return Fail(
                command,
                "invalid-character-selection",
                "A live OMSI driver selection (or automatic driver resolution) and finite local anchor are required.");
        }

        lock (Sync)
        {
            if (Owned.TryGetValue(instanceId, out var existing))
            {
                return BuildCurrentState(command, existing);
            }

            if (Owned.Count >= MaxOwnedCharacters)
            {
                return Fail(
                    command,
                    "character-limit-reached",
                    "The roleplay character ownership limit was reached.");
            }

            if (!OmsiNativeInterop.TrySnapshotHumans(out var humans) || humans.Length == 0)
            {
                return Fail(command, "no-humans", "OMSI has no readable human instances.");
            }

            var usedPointers = Owned.Values
                .Select(value => value.HumanPointer)
                .ToHashSet();

            var driverPointer = 0;
            var driverIndex = -1;
            var bestDistance = double.MaxValue;
            float driverX = 0f;
            float driverY = 0f;
            float driverZ = 0f;
            float driverHeading = 0f;
            float driverSpeed = 0f;

            for (var index = 0; index < humans.Length; index++)
            {
                var pointer = humans[index];
                if (pointer == 0 ||
                    usedPointers.Contains(pointer) ||
                    OmsiNativeInterop.IsPlayerBusDriverHuman(
                        pointer,
                        definitionPointer) != 1 ||
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

                var dx = x - anchorX;
                var dy = y - anchorY;
                var dz = z - anchorZ;
                var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (!double.IsFinite(distance) || distance >= bestDistance)
                {
                    continue;
                }

                driverPointer = pointer;
                driverIndex = index;
                bestDistance = distance;
                driverX = x;
                driverY = y;
                driverZ = z;
                driverHeading = heading;
                driverSpeed = speed;
            }

            if (driverPointer == 0)
            {
                return Fail(
                    command,
                    "selected-driver-not-active",
                    "The selected character is not the active human driver of the player's bus.");
            }

            if (bestDistance > MaxAcquireDistanceMeters ||
                Math.Abs(driverZ - anchorZ) > MaxAcquireHeightDifferenceMeters)
            {
                return Fail(
                    command,
                    "selected-driver-too-far",
                    "The selected driver is not close enough to the current player bus.");
            }

            if (OmsiNativeInterop.ReadHumanAiState(
                    driverPointer,
                    out var aiMode,
                    out var aiModeEx,
                    out var aiSubMode,
                    out var sollSpeed,
                    out var actSpeed) != 1 ||
                OmsiNativeInterop.ReadHumanDriverState(
                    driverPointer,
                    out var originalBus,
                    out var fixDriver,
                    out var renderMe,
                    out var inWorld) != 1)
            {
                return Fail(
                    command,
                    "driver-state-read-failed",
                    "Could not snapshot the selected driver's OMSI state.");
            }

            if (OmsiNativeInterop.DetachHumanForRoleplay(driverPointer) != 1)
            {
                return Fail(
                    command,
                    "driver-detach-failed",
                    "OMSI rejected detaching the selected driver from the bus.");
            }

            var spawnHeading = NormalizeHeading((float)busHeadingValue);
            var spawnRadians = spawnHeading * (Math.PI / 180d);
            const double SideExitOffsetMeters = 1.8d;
            var spawnX = anchorX + (float)(Math.Cos(spawnRadians) * SideExitOffsetMeters);
            var spawnY = anchorY - (float)(Math.Sin(spawnRadians) * SideExitOffsetMeters);
            var spawnZ = anchorZ;

            if (OmsiNativeInterop.SetHumanTransform(
                    driverPointer,
                    spawnX,
                    spawnY,
                    spawnZ,
                    spawnHeading,
                    0f) != 1)
            {
                _ = OmsiNativeInterop.RestoreHumanDriverState(
                    driverPointer,
                    originalBus,
                    fixDriver,
                    renderMe,
                    inWorld);
                _ = OmsiNativeInterop.RestoreHumanAiState(
                    driverPointer,
                    aiMode,
                    aiModeEx,
                    aiSubMode,
                    sollSpeed,
                    actSpeed);

                return Fail(
                    command,
                    "driver-control-failed",
                    "OMSI rejected the selected driver roleplay state.");
            }

            // Do not report RP as active only because the guarded writes
            // returned success. Read the human back from OMSI and confirm that
            // the driver is detached, visible/in-world and actually positioned
            // at the requested exit point.
            var stateConfirmed =
                OmsiNativeInterop.ReadHumanDriverState(
                    driverPointer,
                    out var detachedBus,
                    out var detachedFixDriver,
                    out var detachedRenderMe,
                    out var detachedInWorld) == 1 &&
                detachedBus == 0 &&
                detachedFixDriver == 0 &&
                detachedRenderMe != 0 &&
                detachedInWorld != 0;

            var poseConfirmed =
                OmsiNativeInterop.ReadHumanPose(
                    driverPointer,
                    out var confirmedX,
                    out var confirmedY,
                    out var confirmedZ,
                    out var confirmedHeading,
                    out var confirmedSpeed) == 1;
            if (poseConfirmed)
            {
                var confirmDx = confirmedX - spawnX;
                var confirmDy = confirmedY - spawnY;
                var confirmDz = confirmedZ - spawnZ;
                var confirmDistance = Math.Sqrt(
                    confirmDx * confirmDx +
                    confirmDy * confirmDy +
                    confirmDz * confirmDz);
                poseConfirmed =
                    double.IsFinite(confirmDistance) &&
                    confirmDistance <= 1.0d;
            }

            if (!stateConfirmed || !poseConfirmed)
            {
                // SetHumanTransform requires a detached/controllable human, so
                // restore the original pose before reattaching the driver.
                _ = OmsiNativeInterop.SetHumanTransform(
                    driverPointer,
                    driverX,
                    driverY,
                    driverZ,
                    NormalizeHeading(driverHeading),
                    Math.Clamp(Math.Abs(driverSpeed), 0f, MaxCharacterSpeedMps));
                _ = OmsiNativeInterop.RestoreHumanDriverState(
                    driverPointer,
                    originalBus,
                    fixDriver,
                    renderMe,
                    inWorld);
                _ = OmsiNativeInterop.RestoreHumanAiState(
                    driverPointer,
                    aiMode,
                    aiModeEx,
                    aiSubMode,
                    sollSpeed,
                    actSpeed);

                return Fail(
                    command,
                    !stateConfirmed
                        ? "driver-detach-unconfirmed"
                        : "driver-transform-unconfirmed",
                    !stateConfirmed
                        ? "OMSI did not confirm the active driver as detached and visible in the world."
                        : "OMSI did not confirm the roleplay character at the requested exit position.");
            }

            var instance = new RoleplayCharacterInstance(
                instanceId,
                driverPointer,
                driverIndex,
                definitionPointer,
                originalBus,
                fixDriver,
                renderMe,
                inWorld,
                aiMode,
                aiModeEx,
                aiSubMode,
                sollSpeed,
                actSpeed,
                driverX,
                driverY,
                driverZ,
                NormalizeHeading(driverHeading),
                driverSpeed,
                DateTimeOffset.UtcNow);

            Owned[instanceId] = instance;

            return BuildSuccessStateResult(
                command,
                instance,
                spawnX,
                spawnY,
                spawnZ,
                spawnHeading,
                0f);
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

            return BuildSuccessStateResult(
                command,
                instance,
                x,
                y,
                z,
                heading,
                speed);
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
            if (!Owned.TryGetValue(instanceId, out var instance))
            {
                ActiveTriggersByInstance.Remove(instanceId);
                return RoleplayCharacterCommandProcessor.Result(command, true);
            }

            ReleaseActiveTriggersBestEffort(instanceId, instance.OriginalBusPointer);

            if (OmsiNativeInterop.IsHumanPointer(instance.HumanPointer) != 1)
            {
                Owned.Remove(instanceId);
                ActiveTriggersByInstance.Remove(instanceId);
                return Fail(
                    command,
                    "driver-pointer-stale",
                    "The owned OMSI driver pointer is no longer valid and could not be restored.");
            }

            var poseRestored =
                OmsiNativeInterop.SetHumanTransform(
                    instance.HumanPointer,
                    instance.OriginalX,
                    instance.OriginalY,
                    instance.OriginalZ,
                    instance.OriginalHeading,
                    Math.Clamp(Math.Abs(instance.OriginalSpeed), 0f, MaxCharacterSpeedMps)) == 1;
            var driverStateRestored =
                OmsiNativeInterop.RestoreHumanDriverState(
                    instance.HumanPointer,
                    instance.OriginalBusPointer,
                    instance.FixDriver,
                    instance.RenderMe,
                    instance.InWorld) == 1;
            var aiStateRestored =
                OmsiNativeInterop.RestoreHumanAiState(
                    instance.HumanPointer,
                    instance.AiMode,
                    instance.AiModeEx,
                    instance.AiSubMode,
                    instance.SollSpeed,
                    instance.ActSpeed) == 1;

            if (!poseRestored || !driverStateRestored || !aiStateRestored)
            {
                return Fail(
                    command,
                    "driver-restore-failed",
                    "OMSI rejected one or more writes while restoring the roleplay driver to the bus.");
            }

            var stateConfirmed =
                OmsiNativeInterop.ReadHumanDriverState(
                    instance.HumanPointer,
                    out var restoredBus,
                    out var restoredFixDriver,
                    out var restoredRenderMe,
                    out var restoredInWorld) == 1 &&
                restoredBus == instance.OriginalBusPointer &&
                restoredFixDriver == instance.FixDriver &&
                restoredRenderMe == instance.RenderMe &&
                restoredInWorld == instance.InWorld;

            if (!stateConfirmed)
            {
                return Fail(
                    command,
                    "driver-restore-unconfirmed",
                    "OMSI did not confirm the restored driver state after leaving roleplay mode.");
            }

            Owned.Remove(instanceId);
            ActiveTriggersByInstance.Remove(instanceId);
            return RoleplayCharacterCommandProcessor.Result(command, true);
        }
    }

    private static PluginBridgeMessage TriggerVehicle(
        PluginBridgeMessage command)
    {
        if (!TryGetCharacterId(command, out var instanceId) ||
            !TryNormalizeTriggerName(command.TriggerName, out var triggerName) ||
            command.TriggerActive is not bool triggerActive)
        {
            return Fail(
                command,
                "invalid-roleplay-trigger",
                "A valid owned character, trigger name and boolean state are required.");
        }

        lock (Sync)
        {
            if (!Owned.TryGetValue(instanceId, out var instance))
            {
                return Fail(
                    command,
                    "character-not-owned",
                    "The requested roleplay character is not owned by NavBR.");
            }

            var playerVehicle = OmsiNativeInterop.GetPlayerVehiclePointer();
            if (playerVehicle == 0 ||
                playerVehicle != instance.OriginalBusPointer ||
                OmsiNativeInterop.IsRoadVehiclePointer(playerVehicle) != 1)
            {
                return Fail(
                    command,
                    "roleplay-bus-changed",
                    "The original roleplay bus is no longer the current player vehicle.");
            }

            if (OmsiNativeInterop.ReadHumanPose(
                    instance.HumanPointer,
                    out var humanX,
                    out var humanY,
                    out var humanZ,
                    out _,
                    out _) != 1 ||
                OmsiNativeInterop.ReadRoadVehiclePosition(
                    playerVehicle,
                    out var busX,
                    out var busY,
                    out var busZ) != 1)
            {
                return Fail(
                    command,
                    "roleplay-interaction-position-unavailable",
                    "Could not validate the character and bus positions for interaction.");
            }

            var dx = humanX - busX;
            var dy = humanY - busY;
            var dz = humanZ - busZ;
            var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (!double.IsFinite(distance) ||
                distance > MaxInteractionDistanceMeters ||
                Math.Abs(dz) > MaxInteractionHeightDifferenceMeters)
            {
                return Fail(
                    command,
                    "roleplay-interaction-too-far",
                    "The roleplay character is too far from the bus to use this interaction.");
            }

            if (!TryGetRetainedTriggerString(triggerName, out var triggerPointer))
            {
                return Fail(
                    command,
                    "roleplay-trigger-allocation-failed",
                    "Could not allocate or retain the OMSI trigger name safely.");
            }

            if (OmsiNativeInterop.TriggerRoadVehicle(
                    playerVehicle,
                    triggerPointer,
                    triggerActive ? 1 : 0) != 1)
            {
                return Fail(
                    command,
                    "roleplay-trigger-failed",
                    "OMSI rejected the guarded roleplay vehicle trigger.");
            }

            if (triggerActive)
            {
                if (!ActiveTriggersByInstance.TryGetValue(
                        instanceId,
                        out var activeTriggers))
                {
                    activeTriggers = new HashSet<string>(StringComparer.Ordinal);
                    ActiveTriggersByInstance[instanceId] = activeTriggers;
                }

                activeTriggers.Add(triggerName);
            }
            else if (ActiveTriggersByInstance.TryGetValue(
                         instanceId,
                         out var activeTriggers))
            {
                activeTriggers.Remove(triggerName);
                if (activeTriggers.Count == 0)
                {
                    ActiveTriggersByInstance.Remove(instanceId);
                }
            }

            return RoleplayCharacterCommandProcessor.Result(
                command,
                true,
                humanIndex: instance.HumanIndex,
                x: humanX,
                y: humanY,
                z: humanZ);
        }
    }

    private static bool TryGetRetainedTriggerString(
        string triggerName,
        out int triggerPointer)
    {
        if (RetainedTriggerStrings.TryGetValue(triggerName, out triggerPointer))
        {
            return triggerPointer > 0;
        }

        if (RetainedTriggerStrings.Count >= MaxRetainedTriggerStrings)
        {
            triggerPointer = 0;
            return false;
        }

        triggerPointer = OmsiNativeInterop.AllocateAnsiString(triggerName);
        if (triggerPointer <= 0)
        {
            triggerPointer = 0;
            return false;
        }

        // RVTriggerXML's Delphi string lifetime is not publicly documented.
        // Retain a bounded, deduplicated set for Omsi.exe's lifetime instead
        // of risking a use-after-free after the native call.
        RetainedTriggerStrings.Add(triggerName, triggerPointer);
        return true;
    }

    public static void ReleaseAllBestEffort()
    {
        lock (Sync)
        {
            foreach (var instance in Owned.Values)
            {
                ReleaseActiveTriggersBestEffort(
                    instance.InstanceId,
                    instance.OriginalBusPointer);

                if (OmsiNativeInterop.IsHumanPointer(instance.HumanPointer) == 1)
                {
                    _ = OmsiNativeInterop.SetHumanTransform(
                        instance.HumanPointer,
                        instance.OriginalX,
                        instance.OriginalY,
                        instance.OriginalZ,
                        instance.OriginalHeading,
                        Math.Clamp(Math.Abs(instance.OriginalSpeed), 0f, MaxCharacterSpeedMps));
                    _ = OmsiNativeInterop.RestoreHumanDriverState(
                        instance.HumanPointer,
                        instance.OriginalBusPointer,
                        instance.FixDriver,
                        instance.RenderMe,
                        instance.InWorld);
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
            ActiveTriggersByInstance.Clear();
        }
    }

    private static void ReleaseActiveTriggersBestEffort(
        string instanceId,
        int busPointer)
    {
        if (!ActiveTriggersByInstance.Remove(
                instanceId,
                out var activeTriggers) ||
            activeTriggers.Count == 0 ||
            OmsiNativeInterop.IsRoadVehiclePointer(busPointer) != 1)
        {
            return;
        }

        foreach (var triggerName in activeTriggers)
        {
            if (RetainedTriggerStrings.TryGetValue(
                    triggerName,
                    out var triggerPointer) &&
                triggerPointer > 0)
            {
                _ = OmsiNativeInterop.TriggerRoadVehicle(
                    busPointer,
                    triggerPointer,
                    active: 0);
            }
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

        return BuildSuccessStateResult(
            command,
            instance,
            x,
            y,
            z,
            NormalizeHeading(heading),
            Math.Clamp(Math.Abs(speed), 0f, MaxCharacterSpeedMps));
    }

    private static PluginBridgeMessage BuildSuccessStateResult(
        PluginBridgeMessage command,
        RoleplayCharacterInstance instance,
        float x,
        float y,
        float z,
        float heading,
        float speed)
    {
        byte? aiMode = null;
        byte? aiModeEx = null;
        byte? aiSubMode = null;
        float? sollSpeed = null;
        float? actSpeed = null;
        float? lastMovedDist = null;
        float? animationState = null;
        byte? activityLeg = null;
        byte? activityArmUmbrella = null;
        byte? activityArmKi = null;
        byte? activityHeadKi = null;

        if (OmsiNativeInterop.ReadHumanAiState(
                instance.HumanPointer,
                out var readAiMode,
                out var readAiModeEx,
                out var readAiSubMode,
                out var readSollSpeed,
                out var readActSpeed) == 1)
        {
            aiMode = readAiMode;
            aiModeEx = readAiModeEx;
            aiSubMode = readAiSubMode;
            sollSpeed = readSollSpeed;
            actSpeed = readActSpeed;
        }

        if (OmsiNativeInterop.ReadHumanAnimationState(
                instance.HumanPointer,
                out var readLastMovedDist,
                out var readAnimationState) == 1)
        {
            lastMovedDist = readLastMovedDist;
            animationState = readAnimationState;
        }

        if (OmsiNativeInterop.ReadHumanActivityState(
                instance.HumanPointer,
                out var readActivityLeg,
                out var readActivityArmUmbrella,
                out var readActivityArmKi,
                out var readActivityHeadKi) == 1)
        {
            activityLeg = readActivityLeg;
            activityArmUmbrella = readActivityArmUmbrella;
            activityArmKi = readActivityArmKi;
            activityHeadKi = readActivityHeadKi;
        }

        return RoleplayCharacterCommandProcessor.Result(
            command,
            true,
            humanIndex: instance.HumanIndex,
            x: x,
            y: y,
            z: z,
            heading: heading,
            speed: speed,
            aiMode: aiMode,
            aiModeEx: aiModeEx,
            aiSubMode: aiSubMode,
            sollSpeed: sollSpeed,
            actSpeed: actSpeed,
            lastMovedDist: lastMovedDist,
            animationState: animationState,
            activityLeg: activityLeg,
            activityArmUmbrella: activityArmUmbrella,
            activityArmKi: activityArmKi,
            activityHeadKi: activityHeadKi);
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
        int CharacterDefinitionPointer,
        int OriginalBusPointer,
        byte FixDriver,
        byte RenderMe,
        byte InWorld,
        byte AiMode,
        byte AiModeEx,
        byte AiSubMode,
        float SollSpeed,
        float ActSpeed,
        float OriginalX,
        float OriginalY,
        float OriginalZ,
        float OriginalHeading,
        float OriginalSpeed,
        DateTimeOffset AcquiredAtUtc);
}
