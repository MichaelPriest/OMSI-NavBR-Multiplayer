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

        // Physical writes stay opt-in while alpha.11 validates the complete
        // create/update/despawn lifecycle against OMSI 2.3.004.
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

internal static class PhysicalVehicleBackend
{
    private static readonly bool BackendOptIn =
        string.Equals(
            Environment.GetEnvironmentVariable("NAVBR_OMSI_PHYSICAL_BACKEND"),
            "1",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            Environment.GetEnvironmentVariable("NAVBR_OMSI_PHYSICAL_BACKEND"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static bool IsAvailable => BackendOptIn && OmsiNativeInterop.IsShimReady;

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
            if (OmsiNativeInterop.IsRoadVehiclePointer(instance.VehiclePointer) == 1)
            {
                OmsiNativeInterop.MarkVehicleForKilling(instance.VehiclePointer);
            }
        }

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
            return ApplyState(command, existing);
        }

        if (!TryResolveVehiclePath(command.VehiclePath, out var vehiclePath))
        {
            return Fail(command, "invalid-vehicle-path", "Vehicle path must resolve to an existing Vehicles\\*.bus or Vehicles\\*.ovh file.");
        }

        if (!OmsiNativeInterop.TrySnapshotRoadVehicles(out var before))
        {
            return Fail(command, "roadvehicles-unavailable", "Could not snapshot the OMSI road vehicle list before spawn.");
        }

        var programManager = OmsiNativeInterop.GetProgramManager();
        var roadVehicleTypes = OmsiNativeInterop.GetRoadVehicleTypes();
        if (programManager == 0 || roadVehicleTypes == 0)
        {
            return Fail(command, "omsi-runtime-unavailable", "OMSI ProgramManager or RoadVehicleTypes is unavailable.");
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
        try
        {
            locked = OmsiNativeInterop.LockMakeVehicle(programManager) == 1;
            if (!locked)
            {
                return Fail(command, "makevehicle-lock-failed", "Could not enter OMSI's MakeVehicle critical section.");
            }

            _ = OmsiNativeInterop.MakeVehicle(
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
                aiRoadVehicle: 0,
                randomLicensePlate: 0,
                randomPaintScheme: 0,
                filenameAnsiString: filename);

            _ = OmsiNativeInterop.CopyTempRoadVehicleListIntoMain(tempList);
        }
        finally
        {
            if (locked)
            {
                _ = OmsiNativeInterop.UnlockMakeVehicle(programManager);
            }

            _ = OmsiNativeInterop.FreeAnsiString(filename);
        }

        if (!OmsiNativeInterop.TryFindNewRoadVehicle(before, out var vehiclePointer))
        {
            return Fail(command, "spawn-pointer-unresolved", "OMSI spawn returned without one identifiable new RoadVehicle instance.");
        }

        var instance = new PhysicalVehicleInstance(
            instanceId,
            vehiclePointer,
            vehiclePath,
            DateTimeOffset.UtcNow);

        if (!PhysicalVehicleInstanceRegistry.TryAdd(instance))
        {
            _ = OmsiNativeInterop.MarkVehicleForKilling(vehiclePointer);
            return Fail(command, "instance-registry-full", "Could not register the newly created NavBR vehicle safely.");
        }

        var applied = ApplyState(command, instance);
        if (applied.Success != true)
        {
            PhysicalVehicleInstanceRegistry.TryRemove(instanceId, out _);
            _ = OmsiNativeInterop.MarkVehicleForKilling(vehiclePointer);
        }

        return applied;
    }

    private static PluginBridgeMessage Update(PluginBridgeMessage command)
    {
        if (!TryGetInstanceId(command, out var instanceId) ||
            !PhysicalVehicleInstanceRegistry.TryGet(instanceId, out var instance))
        {
            return Fail(command, "vehicle-not-owned", "The requested physical vehicle is not owned by NavBR.");
        }

        return ApplyState(command, instance);
    }

    private static PluginBridgeMessage Despawn(PluginBridgeMessage command)
    {
        if (!TryGetInstanceId(command, out var instanceId))
        {
            return Fail(command, "invalid-instance-id", "VehicleInstanceId is required for despawn.");
        }

        if (!PhysicalVehicleInstanceRegistry.TryRemove(instanceId, out var instance))
        {
            return ExperimentalVehicleCommandProcessor.Result(command, true);
        }

        if (OmsiNativeInterop.IsRoadVehiclePointer(instance.VehiclePointer) != 1)
        {
            return ExperimentalVehicleCommandProcessor.Result(command, true);
        }

        if (OmsiNativeInterop.MarkVehicleForKilling(instance.VehiclePointer) != 1)
        {
            // Put the entry back so a later despawn can retry instead of losing
            // ownership of a live OMSI pointer.
            PhysicalVehicleInstanceRegistry.TryAdd(instance);
            return Fail(command, "despawn-mark-failed", "Could not mark the NavBR-owned OMSI vehicle for removal.");
        }

        return ExperimentalVehicleCommandProcessor.Result(command, true);
    }

    private static PluginBridgeMessage ApplyState(
        PluginBridgeMessage command,
        PhysicalVehicleInstance instance)
    {
        if (OmsiNativeInterop.IsRoadVehiclePointer(instance.VehiclePointer) != 1)
        {
            PhysicalVehicleInstanceRegistry.TryRemove(instance.InstanceId, out _);
            return Fail(command, "vehicle-pointer-stale", "The OMSI vehicle instance is no longer present in RoadVehicles.");
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
                speedMps) != 1)
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
        string message) =>
        ExperimentalVehicleCommandProcessor.Result(command, false, code, message);

    private readonly record struct VehiclePose(
        float X,
        float Y,
        float Z,
        float RotationX,
        float RotationY,
        float RotationZ,
        float RotationW);
}
