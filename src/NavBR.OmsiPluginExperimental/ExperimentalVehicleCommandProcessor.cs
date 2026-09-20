using NavBR.Shared.Multiplayer;
using NavBR.Shared.PluginBridge;

namespace NavBR.OmsiPluginExperimental;

internal static class ExperimentalVehicleCommandProcessor
{
    public static bool ExperimentalWritesEnabled =>
        ExperimentalFeatureFlags.PhysicalVehiclesEnabled;

    public static string[] GetCapabilities()
    {
        var capabilities = new List<string>
        {
            PluginBridgeProtocol.CapabilityAdvancedTelemetry,
            PluginBridgeProtocol.CapabilityTimetableState
        };

        // Capabilities describe what this plugin/runtime can do. The actual
        // physical writes remain behind the explicit user opt-in and are
        // re-checked for every command.
        if (PhysicalVehicleBackend.IsRuntimeSupported)
        {
            capabilities.Add(PluginBridgeProtocol.CapabilityGhostReplay);
            capabilities.Add(PluginBridgeProtocol.CapabilityVehicleSpawn);
            capabilities.Add(PluginBridgeProtocol.CapabilityVehicleTransform);
            capabilities.Add(PluginBridgeProtocol.CapabilityVehicleVisualState);
            capabilities.Add(PluginBridgeProtocol.CapabilityVehicleInterpolation);
            capabilities.Add(PluginBridgeProtocol.CapabilityVehicleTileSync);
        }

        if (RoleplayCharacterCommandProcessor.IsRuntimeSupported)
        {
            capabilities.Add(PluginBridgeProtocol.CapabilityCharacterPossession);
            capabilities.Add(PluginBridgeProtocol.CapabilityCharacterTransform);
            capabilities.Add(PluginBridgeProtocol.CapabilityCharacterInteraction);
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
            PhysicalVehicleLifecycleSupervisor.ObserveCommand(command);
            var result = PhysicalVehicleBackend.Execute(command);
            PhysicalVehicleLifecycleSupervisor.ObserveResult(command, result);
            return result;
        }
        catch (Exception ex)
        {
            var result = Result(command, false, "backend-error", ex.Message);
            PhysicalVehicleLifecycleSupervisor.ObserveResult(command, result);
            return result;
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

        if (!PhysicalVehicleBackend.IsRuntimeSupported)
        {
            return Result(
                command,
                false,
                "backend-unavailable",
                "The physical OMSI vehicle backend is not available for this build/runtime.");
        }

        var isDespawn =
            string.Equals(command.Type, PluginBridgeProtocol.DespawnRemoteVehicle, StringComparison.Ordinal) ||
            string.Equals(command.Type, PluginBridgeProtocol.DespawnGhostVehicle, StringComparison.Ordinal);

        // Despawn stays allowed after the user turns the experiment off so the
        // client can safely remove every NavBR-owned vehicle already in OMSI.
        if (!ExperimentalWritesEnabled && !isDespawn)
        {
            return Result(
                command,
                false,
                "writes-disabled",
                "Physical OMSI writes are disabled. Enable Remote 3D bus (EXPERIMENTAL) in the multiplayer window.");
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
        string? errorMessage = null,
        int? remoteVehicleCount = null,
        float? localX = null,
        float? localY = null,
        float? localZ = null,
        int? mapTileIndex = null) =>
        new(
            PluginBridgeProtocol.CommandResult,
            PluginBridgeProtocol.Version,
            ProcessId: Environment.ProcessId,
            TimestampUnixMilliseconds: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PlayerId: command.PlayerId,
            CommandId: command.CommandId,
            VehicleInstanceId: command.VehicleInstanceId,
            ExperimentalWritesEnabled: ExperimentalWritesEnabled,
            Success: success,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            RemoteVehicleCount: remoteVehicleCount,
            LocalX: localX,
            LocalY: localY,
            LocalZ: localZ,
            MapTileIndex: mapTileIndex);
}

internal static class PhysicalVehicleBackend
{
    private const int MaxRetainedVehiclePathStrings = 256;
    private const int RequiredMaterializationFlags =
        (1 << 0) |
        (1 << 1) |
        (1 << 2) |
        (1 << 3) |
        (1 << 5);
    // Busweave/OmsiHook's guarded spawn bridge allows up to 20 seconds for
    // OMSI to register/materialize a road vehicle. Match that proven window
    // before declaring a remote bus visually dead.
    private static readonly TimeSpan MaterializationTimeout =
        TimeSpan.FromSeconds(20);
    private static int _retainedVehiclePathStrings;

    public static bool IsRuntimeSupported => OmsiNativeInterop.IsShimReady;

    public static bool IsAvailable =>
        ExperimentalFeatureFlags.PhysicalVehiclesEnabled && IsRuntimeSupported;

    public static PluginBridgeMessage Execute(PluginBridgeMessage command)
    {
        if (IsSpawn(command.Type))
        {
            return Spawn(command);
        }

        if (IsUpdate(command.Type))
        {
            return Update(command);
        }

        if (IsDespawn(command.Type))
        {
            return Despawn(command);
        }

        return ExperimentalVehicleCommandProcessor.Result(
            command,
            false,
            "unsupported-command",
            "Unsupported physical vehicle command.");
    }

    public static void MarkAllOwnedVehiclesForRemoval()
    {
        foreach (var instance in PhysicalVehicleInstanceRegistry.Snapshot())
        {
            if (IsSafeOwnedPointer(instance, out var unsafeReason))
            {
                OmsiNativeInterop.MarkVehicleForKilling(instance.VehiclePointer);
            }
            else
            {
                PluginLogWriter.Enqueue(
                    $"physical-safety skip-remove id={instance.InstanceId} pointer={FormatPointer(instance.VehiclePointer)} reason={unsafeReason}");
            }
        }

        PhysicalVehicleMotionController.Clear();
        PhysicalVehicleInstanceRegistry.Clear();
    }

    private static PluginBridgeMessage Spawn(PluginBridgeMessage command)
    {
        if (!TryGetInstanceId(command, out var instanceId))
        {
            return Fail(command, "invalid-instance-id", "VehicleInstanceId is required for spawn.");
        }

        if (PhysicalVehicleInstanceRegistry.TryGet(instanceId, out var existing))
        {
            // A MakeVehicle result can enter RoadVehicles one OMSI callback
            // before its ComplObj/model graph is fully attached. Keep driving
            // the exact NavBR-owned pointer onto the requested loaded Kachel
            // while OMSI finishes materializing it, but do not report a
            // physical bus as active until the model itself is confirmed.
            var existingApplied = IsRemoteCommand(command.Type)
                ? ApplyRemoteTarget(command, existing)
                : ApplyState(command, existing);
            if (existingApplied.Success != true)
            {
                return existingApplied;
            }

            return GetMaterializationPendingResult(command, existing)
                ?? existingApplied;
        }

        if (IsRemoteCommand(command.Type) &&
            !TryResolveLocalRemoteTile(
                command,
                out command,
                out var tileErrorCode,
                out var tileErrorMessage))
        {
            return Fail(
                command,
                tileErrorCode,
                tileErrorMessage);
        }

        if (command.MapTileIndex is int mapTileIndex &&
            mapTileIndex != OmsiNativeInterop.HostPlayerTileSentinel &&
            (mapTileIndex < 0 ||
             mapTileIndex > 200_000 ||
             OmsiNativeInterop.IsMapTileIndexValid(mapTileIndex) != 1))
        {
            return Fail(
                command,
                "tile-unavailable",
                "The remote OMSI map tile is not currently available in the local map.");
        }

        if (!TryResolveVehiclePath(command.VehiclePath, out var vehiclePath))
        {
            return Fail(command, "invalid-vehicle-path", "Vehicle path must resolve to an existing Vehicles\\*.bus or Vehicles\\*.ovh file.");
        }

        var programManager = OmsiNativeInterop.GetProgramManager();
        var roadVehicleTypes = OmsiNativeInterop.GetRoadVehicleTypes();
        if (programManager == 0 || roadVehicleTypes == 0)
        {
            return Fail(command, "omsi-runtime-unavailable", "OMSI ProgramManager or RoadVehicleTypes is unavailable.");
        }

        // Omsi-Extensions does not establish a safe point at which the Delphi
        // AnsiString passed to MakeVehicle can be released. Retain a bounded
        // number for the lifetime of Omsi.exe rather than risking use-after-free.
        if (Volatile.Read(ref _retainedVehiclePathStrings) >= MaxRetainedVehiclePathStrings)
        {
            return Fail(
                command,
                "spawn-resource-limit",
                "The experimental vehicle path retention limit was reached. Restart OMSI before creating more remote vehicles.");
        }

        var tempList = OmsiNativeInterop.TempRoadVehicleListCreate(1);
        if (tempList == 0)
        {
            return Fail(command, "temp-list-failed", "OMSI did not create the temporary vehicle list.");
        }

        var filename = OmsiNativeInterop.AllocateAnsiString(vehiclePath);
        if (filename == 0)
        {
            return Fail(command, "vehicle-path-allocation-failed", "Could not allocate the OMSI vehicle path string.");
        }

        var locked = false;
        var handedToOmsi = false;
        var makeVehicleResult = 0;
        var copyTempListResult = 0;
        var createdVehiclePointers = Array.Empty<int>();
        var tempVehiclePointers = Array.Empty<int>();
        var tempListSnapshotAvailable = false;
        var hostVehiclePointer = 0;
        var beforeVehiclePointers = Array.Empty<int>();
        var afterVehiclePointers = Array.Empty<int>();
        var newRoadVehicleCandidates = Array.Empty<int>();
        try
        {
            locked = OmsiNativeInterop.LockMakeVehicle(programManager) == 1;
            if (!locked)
            {
                return Fail(command, "makevehicle-lock-failed", "Could not enter OMSI's MakeVehicle critical section.");
            }

            // Snapshot while holding OMSI's own MakeVehicle critical section.
            // This prevents an unrelated vehicle creation from being mistaken
            // for a NavBR-owned result between the before/after snapshots.
            hostVehiclePointer = OmsiNativeInterop.GetPlayerVehiclePointer();
            if (!OmsiNativeInterop.TrySnapshotRoadVehicles(out var before))
            {
                return Fail(
                    command,
                    "roadvehicles-unavailable",
                    "Could not snapshot the OMSI road vehicle list inside the MakeVehicle critical section.");
            }

            beforeVehiclePointers = before;
            PluginLogWriter.Enqueue(
                $"physical-spawn before id={instanceId} hostVehiclePointer={FormatPointer(hostVehiclePointer)} RoadVehicles={FormatPointerList(beforeVehiclePointers)}");

            handedToOmsi = true;
            makeVehicleResult = OmsiNativeInterop.MakeVehicle(
                programManager,
                tempList,
                roadVehicleTypes,
                onlyVehicleList: 0,
                cs: 0,
                timetableTimeBits: 0,
                situationLoad: 0,
                dialog: 0,
                setDriver: 0,
                thread: 0,
                licensePlateIndex: -1,
                initCall: 1,
                startDay: 0,
                trainBuildDirection: 2,
                reverse: 0,
                groupHof: 0,
                type: 0,
                tour: 0,
                line: 0,
                paintScheme: -1,
                scheduled: 0,
                // Keep exact remote buses on the proven MakeVehicle path
                // used by Omsi-Extensions. AIRoadVehicle changes OMSI's AI
                // ownership semantics and can defer/bypass the supplied temp
                // list, which prevents NavBR from safely identifying the exact
                // object created for this remote player. NavBR owns movement
                // after creation through its guarded transform controller.
                aiRoadVehicle: 0,
                randomLicensePlate: 0,
                randomPaintScheme: 0,
                filenameAnsiString: filename);

            // The temporary RoadVehicle list belongs only to this
            // MakeVehicle call. Capture its exact pointers before copying it
            // into the global list so unrelated OMSI AI traffic can never be
            // claimed by a NavBR player.
            tempListSnapshotAvailable =
                OmsiNativeInterop.TrySnapshotTempRoadVehicles(
                    tempList,
                    out tempVehiclePointers);

            copyTempListResult =
                OmsiNativeInterop.CopyTempRoadVehicleListIntoMain(tempList);

            if (!OmsiNativeInterop.TrySnapshotRoadVehicles(out var after))
            {
                return Fail(
                    command,
                    "roadvehicles-after-unavailable",
                    "Could not snapshot the OMSI road vehicle list after MakeVehicle.");
            }

            afterVehiclePointers = after;
            var currentHostVehiclePointer = OmsiNativeInterop.GetPlayerVehiclePointer();
            var knownBefore = new HashSet<int>(beforeVehiclePointers);
            var liveAfter = new HashSet<int>(afterVehiclePointers);
            newRoadVehicleCandidates = afterVehiclePointers
                .Where(pointer =>
                    pointer != 0 &&
                    !knownBefore.Contains(pointer) &&
                    pointer != hostVehiclePointer &&
                    pointer != currentHostVehiclePointer &&
                    OmsiNativeInterop.IsRoadVehiclePointer(pointer) == 1)
                .Distinct()
                .ToArray();

            PluginLogWriter.Enqueue(
                $"physical-spawn after id={instanceId} hostVehiclePointer={FormatPointer(currentHostVehiclePointer)} RoadVehicles={FormatPointerList(afterVehiclePointers)} newCandidates={FormatPointerList(newRoadVehicleCandidates)} tempCandidates={FormatPointerList(tempVehiclePointers)} makeVehicleResult={FormatPointer(makeVehicleResult)}");

            var completeDiff = false;
            if (tempListSnapshotAvailable &&
                tempVehiclePointers.Length > 0)
            {
                var distinctTempPointers =
                    tempVehiclePointers.Distinct().ToArray();
                createdVehiclePointers = distinctTempPointers
                    .Where(pointer =>
                        pointer != 0 &&
                        !knownBefore.Contains(pointer) &&
                        pointer != hostVehiclePointer &&
                        pointer != currentHostVehiclePointer &&
                        liveAfter.Contains(pointer) &&
                        OmsiNativeInterop.IsRoadVehiclePointer(pointer) == 1)
                    .ToArray();
                completeDiff =
                    createdVehiclePointers.Length ==
                    distinctTempPointers.Length;
            }
            else
            {
                createdVehiclePointers = newRoadVehicleCandidates;
                completeDiff = createdVehiclePointers.Length > 0;

                if (OmsiNativeInterop.TryResolveMakeVehicleResult(
                        makeVehicleResult,
                        beforeVehiclePointers,
                        out var exactCreatedVehiclePointer) &&
                    newRoadVehicleCandidates.Contains(exactCreatedVehiclePointer))
                {
                    createdVehiclePointers = [exactCreatedVehiclePointer];
                    completeDiff = true;
                }
            }

            if (!completeDiff || createdVehiclePointers.Length == 0)
            {
                return Fail(
                    command,
                    "spawn-pointer-unresolved",
                    $"OMSI spawn did not produce a fully identifiable RoadVehicle. Native MakeVehicle result={makeVehicleResult}, CopyTempList result={copyTempListResult}, tempSnapshot={tempListSnapshotAvailable}, tempCount={tempVehiclePointers.Length}, detected={createdVehiclePointers.Length}.");
            }

            if (createdVehiclePointers.Length != 1)
            {
                // Do not kill every pointer from an ambiguous global diff:
                // another OMSI AI spawn may have happened concurrently and we
                // must never delete a vehicle that NavBR does not own.
                return Fail(
                    command,
                    "spawn-pointer-ambiguous",
                    $"OMSI created or exposed {createdVehiclePointers.Length} new RoadVehicle candidates but the exact MakeVehicle result could not be resolved safely. Native MakeVehicle result={makeVehicleResult}, CopyTempList result={copyTempListResult}.",
                    remoteVehicleCount: createdVehiclePointers.Length);
            }
        }
        finally
        {
            if (locked)
            {
                _ = OmsiNativeInterop.UnlockMakeVehicle(programManager);
            }

            if (handedToOmsi)
            {
                Interlocked.Increment(ref _retainedVehiclePathStrings);
            }
            else
            {
                _ = OmsiNativeInterop.FreeAnsiString(filename);
            }
        }

        var vehiclePointer = createdVehiclePointers[0];
        var hostVehiclePointerAfterSpawn = OmsiNativeInterop.GetPlayerVehiclePointer();
        if (beforeVehiclePointers.Contains(vehiclePointer) ||
            !afterVehiclePointers.Contains(vehiclePointer) ||
            vehiclePointer == hostVehiclePointer ||
            vehiclePointer == hostVehiclePointerAfterSpawn ||
            OmsiNativeInterop.IsRoadVehiclePointer(vehiclePointer) != 1)
        {
            PluginLogWriter.Enqueue(
                $"physical-safety reject-spawn id={instanceId} pointer={FormatPointer(vehiclePointer)} hostBefore={FormatPointer(hostVehiclePointer)} hostAfter={FormatPointer(hostVehiclePointerAfterSpawn)} before={FormatPointerList(beforeVehiclePointers)} after={FormatPointerList(afterVehiclePointers)}");
            return Fail(
                command,
                "unsafe-spawn-pointer",
                "The candidate RoadVehicle was not proven to be a newly created non-player vehicle.");
        }

        var assignedTileIndex =
            OmsiNativeInterop.ReadRoadVehicleTileIndex(vehiclePointer);
        PluginLogWriter.Enqueue(
            $"physical-spawn assigned id={instanceId} pointer={FormatPointer(vehiclePointer)} hostVehiclePointer={FormatPointer(hostVehiclePointerAfterSpawn)} vehiclePath={vehiclePath} kachel={assignedTileIndex} newCandidate=true");

        var instance = new PhysicalVehicleInstance(
            instanceId,
            vehiclePointer,
            vehiclePath,
            DateTimeOffset.UtcNow,
            hostVehiclePointer,
            PointerWasAbsentBeforeSpawn: true);

        if (!PhysicalVehicleInstanceRegistry.TryAdd(instance))
        {
            _ = OmsiNativeInterop.MarkVehicleForKilling(vehiclePointer);
            return Fail(command, "instance-registry-full", "Could not register the newly created NavBR vehicle safely.");
        }

        // Position the exact MakeVehicle result on the real loaded Kachel
        // before demanding a complete render model. Some OMSI vehicle add-ons
        // finish attaching ComplObj/model state on the callback after the temp
        // list is copied into RoadVehicles; killing the pointer immediately
        // prevented that materialization from ever completing.
        var applied = IsRemoteCommand(command.Type)
            ? InitializeRemoteMotion(command, instance)
            : ApplyState(command, instance);
        if (applied.Success != true)
        {
            if (IsTransientPostSpawnFailure(applied.ErrorCode))
            {
                // MakeVehicle already succeeded and the pointer is ours. Do
                // not destroy/recreate it just because a secondary transform
                // field is not writable yet while OMSI finishes attaching the
                // RoadVehicle graph. Keeping ownership also prevents one
                // retained Delphi path allocation per retry.
                var pending = GetMaterializationPendingResult(
                    command,
                    instance);
                return pending ?? applied;
            }

            PhysicalVehicleMotionController.Remove(instanceId);
            PhysicalVehicleInstanceRegistry.TryRemove(instanceId, out _);
            _ = OmsiNativeInterop.MarkVehicleForKilling(vehiclePointer);
            return applied;
        }

        return GetMaterializationPendingResult(command, instance)
            ?? applied;
    }

    private static PluginBridgeMessage? GetMaterializationPendingResult(
        PluginBridgeMessage command,
        PhysicalVehicleInstance instance)
    {
        if (!IsSafeOwnedPointer(instance, out var unsafeReason))
        {
            PhysicalVehicleMotionController.Remove(instance.InstanceId);
            PhysicalVehicleInstanceRegistry.TryRemove(instance.InstanceId, out _);
            return Fail(
                command,
                "unsafe-owned-pointer",
                $"The OMSI vehicle instance is no longer safe to own: {unsafeReason}.");
        }

        var flags = OmsiNativeInterop.GetRoadVehicleMaterializationFlags(
            instance.VehiclePointer);
        if ((flags & RequiredMaterializationFlags) ==
            RequiredMaterializationFlags)
        {
            return null;
        }

        var age = DateTimeOffset.UtcNow - instance.CreatedAtUtc;
        if (age >= MaterializationTimeout)
        {
            PhysicalVehicleMotionController.Remove(instance.InstanceId);
            PhysicalVehicleInstanceRegistry.TryRemove(instance.InstanceId, out _);
            _ = OmsiNativeInterop.MarkVehicleForKilling(instance.VehiclePointer);
            return Fail(
                command,
                "spawn-model-timeout",
                $"OMSI kept RoadVehicle 0x{instance.VehiclePointer:X8} alive for {age.TotalSeconds:F1}s, but its visual model never completed materialization (flags=0x{flags:X2} [{DescribeMaterializationFlags(flags)}], required=0x{RequiredMaterializationFlags:X2}).");
        }

        return Fail(
            command,
            "spawn-model-pending",
            $"OMSI created and positioned RoadVehicle 0x{instance.VehiclePointer:X8}; its visual model is still materializing (flags=0x{flags:X2} [{DescribeMaterializationFlags(flags)}], required=0x{RequiredMaterializationFlags:X2}, age={age.TotalMilliseconds:F0}ms).");
    }

    private static bool IsTransientPostSpawnFailure(
        string? errorCode) =>
        string.Equals(
            errorCode,
            "transform-write-failed",
            StringComparison.Ordinal) ||
        string.Equals(
            errorCode,
            "visual-state-write-failed",
            StringComparison.Ordinal) ||
        string.Equals(
            errorCode,
            "motion-readback-unavailable",
            StringComparison.Ordinal) ||
        string.Equals(
            errorCode,
            "spawn-materialization-unconfirmed",
            StringComparison.Ordinal);

    private static string DescribeMaterializationFlags(int flags)
    {
        var names = new List<string>(6);
        if ((flags & (1 << 0)) != 0) names.Add("mainList");
        if ((flags & (1 << 1)) != 0) names.Add("roadVehicleDefinition");
        if ((flags & (1 << 2)) != 0) names.Add("complMapObjDefinition");
        if ((flags & (1 << 3)) != 0) names.Add("complObj");
        if ((flags & (1 << 4)) != 0) names.Add("fileObject");
        if ((flags & (1 << 5)) != 0) names.Add("model");
        return names.Count == 0 ? "none" : string.Join(",", names);
    }

    internal static bool IsSafeOwnedPointer(
        PhysicalVehicleInstance instance,
        out string reason)
    {
        reason = string.Empty;
        if (!instance.PointerWasAbsentBeforeSpawn)
        {
            reason = "pointer-was-not-new";
            return false;
        }

        if (instance.VehiclePointer <= 0)
        {
            reason = "pointer-invalid";
            return false;
        }

        var currentHostVehiclePointer =
            OmsiNativeInterop.GetPlayerVehiclePointer();
        if (instance.VehiclePointer == instance.HostVehiclePointerAtSpawn ||
            (currentHostVehiclePointer != 0 &&
             instance.VehiclePointer == currentHostVehiclePointer))
        {
            reason =
                $"pointer-is-host hostAtSpawn={FormatPointer(instance.HostVehiclePointerAtSpawn)} currentHost={FormatPointer(currentHostVehiclePointer)}";
            return false;
        }

        if (OmsiNativeInterop.IsRoadVehiclePointer(instance.VehiclePointer) != 1)
        {
            reason = "pointer-not-in-RoadVehicles";
            return false;
        }

        return true;
    }

    private static string FormatPointer(int pointer) =>
        pointer == 0 ? "0x00000000" : $"0x{pointer:X8}";

    private static string FormatPointerList(IEnumerable<int> pointers) =>
        "[" + string.Join(",", pointers.Select(FormatPointer)) + "]";

    private static PluginBridgeMessage Update(PluginBridgeMessage command)
    {
        if (!TryGetInstanceId(command, out var instanceId) ||
            !PhysicalVehicleInstanceRegistry.TryGet(instanceId, out var instance))
        {
            return Fail(command, "vehicle-not-owned", "The requested physical vehicle is not owned by NavBR.");
        }

        return IsRemoteCommand(command.Type)
            ? ApplyRemoteTarget(command, instance)
            : ApplyState(command, instance);
    }

    private static PluginBridgeMessage Despawn(PluginBridgeMessage command)
    {
        if (!TryGetInstanceId(command, out var instanceId))
        {
            return Fail(command, "invalid-instance-id", "VehicleInstanceId is required for despawn.");
        }

        if (!PhysicalVehicleInstanceRegistry.TryRemove(instanceId, out var instance))
        {
            PhysicalVehicleMotionController.Remove(instanceId);
            return ExperimentalVehicleCommandProcessor.Result(command, true);
        }

        if (!IsSafeOwnedPointer(instance, out var unsafeReason))
        {
            PhysicalVehicleMotionController.Remove(instanceId);
            PluginLogWriter.Enqueue(
                $"physical-safety skip-despawn id={instanceId} pointer={FormatPointer(instance.VehiclePointer)} reason={unsafeReason}");
            return ExperimentalVehicleCommandProcessor.Result(command, true);
        }

        if (OmsiNativeInterop.MarkVehicleForKilling(instance.VehiclePointer) != 1)
        {
            PhysicalVehicleInstanceRegistry.TryAdd(instance);
            return Fail(command, "despawn-mark-failed", "Could not mark the NavBR-owned OMSI vehicle for removal.");
        }

        PhysicalVehicleMotionController.Remove(instanceId);
        return ExperimentalVehicleCommandProcessor.Result(command, true);
    }

    private static PluginBridgeMessage InitializeRemoteMotion(
        PluginBridgeMessage command,
        PhysicalVehicleInstance instance)
    {
        if (!IsSafeOwnedPointer(instance, out var unsafeReason))
        {
            PhysicalVehicleInstanceRegistry.TryRemove(instance.InstanceId, out _);
            PhysicalVehicleMotionController.Remove(instance.InstanceId);
            return Fail(command, "unsafe-owned-pointer", $"The OMSI vehicle instance is no longer safe to own: {unsafeReason}.");
        }

        if (!PhysicalVehicleMotionController.TryInitialize(
                instance,
                command,
                out var errorCode,
                out var errorMessage))
        {
            return Fail(
                command,
                errorCode ?? "motion-initialize-failed",
                errorMessage ?? "Could not initialize remote physical vehicle smoothing.");
        }

        if (OmsiNativeInterop.ReadRoadVehiclePosition(
                instance.VehiclePointer,
                out var actualX,
                out var actualY,
                out var actualZ) != 1)
        {
            return Fail(
                command,
                "spawn-materialization-unconfirmed",
                "OMSI created the RoadVehicle but its live position could not be read back.");
        }

        var actualTileIndex =
            OmsiNativeInterop.ReadRoadVehicleTileIndex(instance.VehiclePointer);
        if (command.MapTileIndex is int expectedTileIndex &&
            expectedTileIndex >= 0 &&
            actualTileIndex != expectedTileIndex)
        {
            return Fail(
                command,
                "spawn-tile-mismatch",
                $"OMSI created the RoadVehicle on Kachel {actualTileIndex}, expected {expectedTileIndex}.");
        }

        if (command.LocalX is double expectedX &&
            command.LocalY is double expectedY &&
            command.LocalZ is double expectedZ)
        {
            var dx = actualX - expectedX;
            var dy = actualY - expectedY;
            var dz = actualZ - expectedZ;
            var readbackDistance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (!double.IsFinite(readbackDistance) || readbackDistance > 3d)
            {
                return Fail(
                    command,
                    "spawn-transform-mismatch",
                    $"OMSI RoadVehicle readback differs from the requested local pose by {readbackDistance:F2} m.");
            }
        }

        return ExperimentalVehicleCommandProcessor.Result(
            command,
            true,
            localX: actualX,
            localY: actualY,
            localZ: actualZ,
            mapTileIndex: actualTileIndex >= 0 ? actualTileIndex : null);
    }

    private static PluginBridgeMessage ApplyRemoteTarget(
        PluginBridgeMessage command,
        PhysicalVehicleInstance instance)
    {
        if (!IsSafeOwnedPointer(instance, out var unsafeReason))
        {
            PhysicalVehicleInstanceRegistry.TryRemove(instance.InstanceId, out _);
            PhysicalVehicleMotionController.Remove(instance.InstanceId);
            return Fail(command, "unsafe-owned-pointer", $"The OMSI vehicle instance is no longer safe to own: {unsafeReason}.");
        }

        if (!TryResolveLocalRemoteTile(
                command,
                out var localizedCommand,
                out var tileErrorCode,
                out var tileErrorMessage))
        {
            return Fail(
                command,
                tileErrorCode,
                tileErrorMessage);
        }

        return PhysicalVehicleMotionController.TrySetTarget(
                instance,
                localizedCommand,
                out var errorCode,
                out var errorMessage)
            ? ExperimentalVehicleCommandProcessor.Result(localizedCommand, true)
            : Fail(
                localizedCommand,
                errorCode ?? "motion-target-failed",
                errorMessage ?? "Could not update the remote physical vehicle smoothing target.");
    }

    private static PluginBridgeMessage ApplyState(
        PluginBridgeMessage command,
        PhysicalVehicleInstance instance)
    {
        if (!IsSafeOwnedPointer(instance, out var unsafeReason))
        {
            PhysicalVehicleInstanceRegistry.TryRemove(instance.InstanceId, out _);
            return Fail(command, "unsafe-owned-pointer", $"The OMSI vehicle instance is no longer safe to own: {unsafeReason}.");
        }

        if (!TryReadPose(command, out var pose))
        {
            return Fail(command, "invalid-pose", "Physical vehicle updates require finite local position and quaternion values.");
        }

        var speedMps = command.SpeedKph is double speedKph && double.IsFinite(speedKph)
            ? (float)Math.Clamp(Math.Abs(speedKph) / 3.6d, 0d, 150d)
            : 0f;

        if (OmsiNativeInterop.SetVehicleTransform(
                instance.VehiclePointer,
                pose.X,
                pose.Y,
                pose.Z,
                pose.RotationX,
                pose.RotationY,
                pose.RotationZ,
                pose.RotationW,
                speedMps,
                command.MapTileIndex is int mapTileIndex &&
                (mapTileIndex == OmsiNativeInterop.HostPlayerTileSentinel ||
                 mapTileIndex >= 0)
                    ? mapTileIndex
                    : -1) != 1)
        {
            return Fail(command, "transform-write-failed", "OMSI rejected the guarded vehicle transform write.");
        }

        if (OmsiNativeInterop.SetVehicleVisualState(
                instance.VehiclePointer,
                command.LightFlags ?? 0,
                command.TurnSignal ?? 0) != 1)
        {
            return Fail(command, "visual-state-write-failed", "OMSI rejected the guarded vehicle visual-state write.");
        }

        return ExperimentalVehicleCommandProcessor.Result(command, true);
    }

    private static bool TryResolveLocalRemoteTile(
        PluginBridgeMessage command,
        out PluginBridgeMessage localizedCommand,
        out string errorCode,
        out string errorMessage)
    {
        localizedCommand = command;
        errorCode = string.Empty;
        errorMessage = string.Empty;

        // Simulator validation runs inside the same local OMSI process as the
        // real reference player. Its synthetic offsets are intentionally
        // placed around that player, so the authoritative physical Kachel is
        // the host RoadVehicle's live tile, not the navigation GridX/GridY
        // copied through multiplayer telemetry. Using the live tile here also
        // avoids false tile-grid-unavailable failures when the read-only
        // telemetry profile cannot reconstruct KachelInfo grid coordinates.
        if (command.PlayerId?.StartsWith(
                "sim-",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            var hostVehicle = OmsiNativeInterop.GetPlayerVehiclePointer();
            var hostTileIndex = hostVehicle != 0
                ? OmsiNativeInterop.ReadRoadVehicleTileIndex(hostVehicle)
                : -1;
            if (hostTileIndex >= 0 &&
                OmsiNativeInterop.IsMapTileIndexValid(hostTileIndex) == 1)
            {
                localizedCommand = command with
                {
                    MapTileIndex = hostTileIndex
                };
                return true;
            }

            // Grundorf and some other OMSI maps can expose a perfectly valid
            // RoadVehicle.Kachel pointer that is absent from Map.Kacheln, so
            // there is no portable integer Kachel index. ReadPlayerVehicleGrid
            // succeeding proves the live pointer can be resolved through
            // KachelInfos; the -2 selector tells the native transform layer to
            // copy that exact host pointer instead of fabricating an index.
            if (hostVehicle != 0 &&
                OmsiNativeInterop.ReadPlayerVehicleGrid(
                    out _,
                    out _,
                    out _) == 1)
            {
                localizedCommand = command with
                {
                    MapTileIndex = OmsiNativeInterop.HostPlayerTileSentinel
                };
                return true;
            }
        }

        if (command.GridX is not int gridX ||
            command.GridY is not int gridY)
        {
            var simulatorFallback =
                command.PlayerId?.StartsWith(
                    "sim-",
                    StringComparison.OrdinalIgnoreCase) == true &&
                command.MapTileIndex is int inheritedTileIndex &&
                inheritedTileIndex >= 0 &&
                OmsiNativeInterop.IsMapTileIndexValid(inheritedTileIndex) == 1;
            if (simulatorFallback)
            {
                localizedCommand = command;
                return true;
            }

            errorCode = "tile-grid-missing";
            errorMessage =
                "Remote physical vehicle telemetry did not include a stable OMSI GridX/GridY tile identity.";
            return false;
        }

        var localTileIndex =
            OmsiNativeInterop.ResolveMapTileIndex(gridX, gridY);
        if (localTileIndex < 0 ||
            OmsiNativeInterop.IsMapTileIndexValid(localTileIndex) != 1)
        {
            errorCode = "tile-grid-unavailable";
            errorMessage =
                $"OMSI grid ({gridX}, {gridY}) is not currently loaded in the local map.";
            return false;
        }

        // MapTileIndex is an index into the local process' currently loaded
        // Kachel list and is not portable across multiplayer clients. Resolve
        // the remote stable grid coordinates into this OMSI process instead.
        localizedCommand = command with
        {
            MapTileIndex = localTileIndex
        };
        return true;
    }

    private static bool TryReadPose(PluginBridgeMessage command, out VehiclePose pose)
    {
        pose = default;
        if (command.LocalX is not double x || !double.IsFinite(x) ||
            command.LocalY is not double y || !double.IsFinite(y) ||
            command.LocalZ is not double z || !double.IsFinite(z) ||
            command.RotationX is not double rotationX || !double.IsFinite(rotationX) ||
            command.RotationY is not double rotationY || !double.IsFinite(rotationY) ||
            command.RotationZ is not double rotationZ || !double.IsFinite(rotationZ) ||
            command.RotationW is not double rotationW || !double.IsFinite(rotationW))
        {
            return false;
        }

        if (Math.Abs(x) > 100000d || Math.Abs(y) > 100000d || Math.Abs(z) > 100000d)
        {
            return false;
        }

        var length = Math.Sqrt(
            rotationX * rotationX +
            rotationY * rotationY +
            rotationZ * rotationZ +
            rotationW * rotationW);
        if (!double.IsFinite(length) || length < 0.0001d)
        {
            return false;
        }

        pose = new VehiclePose(
            (float)x,
            (float)y,
            (float)z,
            (float)rotationX,
            (float)rotationY,
            (float)rotationZ,
            (float)rotationW);
        return true;
    }

    private static bool TryResolveVehiclePath(string? value, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 1024)
        {
            return false;
        }

        var candidate = value.Trim().Replace('/', '\\').TrimStart('\\');
        if (!candidate.StartsWith("Vehicles\\", StringComparison.OrdinalIgnoreCase) ||
            candidate.Contains("..", StringComparison.Ordinal) ||
            !(candidate.EndsWith(".bus", StringComparison.OrdinalIgnoreCase) ||
              candidate.EndsWith(".ovh", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        try
        {
            var executable = Environment.ProcessPath;
            var omsiRoot = string.IsNullOrWhiteSpace(executable)
                ? null
                : Path.GetDirectoryName(executable);
            if (string.IsNullOrWhiteSpace(omsiRoot))
            {
                return false;
            }

            var root = Path.GetFullPath(omsiRoot);
            var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(root, candidate));
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(fullPath))
            {
                return false;
            }

            relativePath = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetInstanceId(PluginBridgeMessage command, out string instanceId)
    {
        instanceId = command.VehicleInstanceId?.Trim() ?? string.Empty;
        return instanceId.Length is > 0 and <= 128;
    }

    private static bool IsRemoteCommand(string type) =>
        string.Equals(type, PluginBridgeProtocol.SpawnRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.UpdateRemoteVehicle, StringComparison.Ordinal);

    private static bool IsSpawn(string type) =>
        string.Equals(type, PluginBridgeProtocol.SpawnRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.SpawnGhostVehicle, StringComparison.Ordinal);

    private static bool IsUpdate(string type) =>
        string.Equals(type, PluginBridgeProtocol.UpdateRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.UpdateGhostVehicle, StringComparison.Ordinal);

    private static bool IsDespawn(string type) =>
        string.Equals(type, PluginBridgeProtocol.DespawnRemoteVehicle, StringComparison.Ordinal) ||
        string.Equals(type, PluginBridgeProtocol.DespawnGhostVehicle, StringComparison.Ordinal);

    private static PluginBridgeMessage Fail(
        PluginBridgeMessage command,
        string code,
        string message,
        int? remoteVehicleCount = null) =>
        ExperimentalVehicleCommandProcessor.Result(
            command,
            false,
            code,
            message,
            remoteVehicleCount);

    private readonly record struct VehiclePose(
        float X,
        float Y,
        float Z,
        float RotationX,
        float RotationY,
        float RotationZ,
        float RotationW);
}