using System.Collections.Concurrent;
using System.Security.Cryptography;
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
        }

        if (RoleplayCharacterCommandProcessor.IsRuntimeSupported)
        {
            capabilities.Add(PluginBridgeProtocol.CapabilityCharacterPossession);
            capabilities.Add(PluginBridgeProtocol.CapabilityCharacterTransform);
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
        string? errorMessage = null) =>
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
            ErrorMessage: errorMessage);
}

internal static class PhysicalVehicleBackend
{
    private const int MaxRetainedVehiclePathStrings = 256;
    private const int MaxVehicleDefinitionsToScan = 10_000;
    private static int _retainedVehiclePathStrings;
    private static readonly ConcurrentDictionary<string, FingerprintCacheEntry> VehicleFingerprintCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> VehiclePathByCompatibilityId =
        new(StringComparer.OrdinalIgnoreCase);

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

        if (!TryResolveVehiclePath(
                command.VehiclePath,
                command.VehicleCompatibilityId,
                out var vehiclePath))
        {
            return Fail(
                command,
                "invalid-vehicle-path",
                "The remote vehicle asset could not be resolved by its reported Vehicles path or sha256 compatibility fingerprint.");
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
        try
        {
            locked = OmsiNativeInterop.LockMakeVehicle(programManager) == 1;
            if (!locked)
            {
                return Fail(command, "makevehicle-lock-failed", "Could not enter OMSI's MakeVehicle critical section.");
            }

            handedToOmsi = true;
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

            if (handedToOmsi)
            {
                Interlocked.Increment(ref _retainedVehiclePathStrings);
            }
            else
            {
                _ = OmsiNativeInterop.FreeAnsiString(filename);
            }
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

    private static bool TryResolveVehiclePath(
        string? value,
        string? compatibilityId,
        out string relativePath)
    {
        relativePath = string.Empty;

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

            var hasFingerprint = TryNormalizeVehicleCompatibilityId(
                compatibilityId,
                out var normalizedCompatibilityId);

            if (TryNormalizeVehicleRelativePath(value, out var candidate))
            {
                var fullPath = Path.GetFullPath(Path.Combine(root, candidate));
                if (fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(fullPath) &&
                    (!hasFingerprint ||
                     TryFingerprintVehicle(fullPath, out var candidateCompatibilityId) &&
                     string.Equals(
                         candidateCompatibilityId,
                         normalizedCompatibilityId,
                         StringComparison.OrdinalIgnoreCase)))
                {
                    relativePath = candidate;
                    if (hasFingerprint)
                    {
                        VehiclePathByCompatibilityId[normalizedCompatibilityId] = candidate;
                    }

                    return true;
                }
            }

            if (!hasFingerprint)
            {
                return false;
            }

            if (VehiclePathByCompatibilityId.TryGetValue(
                    normalizedCompatibilityId,
                    out var cachedRelativePath) &&
                TryNormalizeVehicleRelativePath(
                    cachedRelativePath,
                    out cachedRelativePath))
            {
                var cachedFullPath = Path.GetFullPath(
                    Path.Combine(root, cachedRelativePath));
                if (cachedFullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(cachedFullPath) &&
                    TryFingerprintVehicle(
                        cachedFullPath,
                        out var cachedCompatibilityId) &&
                    string.Equals(
                        cachedCompatibilityId,
                        normalizedCompatibilityId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = cachedRelativePath;
                    return true;
                }

                VehiclePathByCompatibilityId.TryRemove(
                    normalizedCompatibilityId,
                    out _);
            }

            var vehiclesRoot = Path.Combine(root, "Vehicles");
            if (!Directory.Exists(vehiclesRoot))
            {
                return false;
            }

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            var inspected = 0;
            foreach (var fullPath in Directory.EnumerateFiles(
                         vehiclesRoot,
                         "*.*",
                         options))
            {
                if (++inspected > MaxVehicleDefinitionsToScan)
                {
                    break;
                }

                if (!(fullPath.EndsWith(".bus", StringComparison.OrdinalIgnoreCase) ||
                      fullPath.EndsWith(".ovh", StringComparison.OrdinalIgnoreCase)) ||
                    !TryFingerprintVehicle(
                        fullPath,
                        out var localCompatibilityId) ||
                    !string.Equals(
                        localCompatibilityId,
                        normalizedCompatibilityId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var localRelativePath = Path.GetRelativePath(root, fullPath)
                    .Replace('/', '\\');
                if (!TryNormalizeVehicleRelativePath(
                        localRelativePath,
                        out localRelativePath))
                {
                    continue;
                }

                VehiclePathByCompatibilityId[normalizedCompatibilityId] =
                    localRelativePath;
                relativePath = localRelativePath;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryNormalizeVehicleRelativePath(
        string? value,
        out string relativePath)
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

        relativePath = candidate;
        return true;
    }

    private static bool TryNormalizeVehicleCompatibilityId(
        string? value,
        out string compatibilityId)
    {
        compatibilityId = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        const string prefix = "sha256:";
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal) ||
            normalized.Length != prefix.Length + 64)
        {
            return false;
        }

        foreach (var character in normalized.AsSpan(prefix.Length))
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        compatibilityId = normalized;
        return true;
    }

    private static bool TryFingerprintVehicle(
        string fullPath,
        out string compatibilityId)
    {
        compatibilityId = string.Empty;

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return false;
            }

            if (VehicleFingerprintCache.TryGetValue(
                    fullPath,
                    out var cached) &&
                cached.Length == info.Length &&
                cached.LastWriteUtc == info.LastWriteTimeUtc)
            {
                compatibilityId = cached.CompatibilityId;
                return true;
            }

            using var stream = File.Open(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var hash = Convert.ToHexString(SHA256.HashData(stream))
                .ToLowerInvariant();
            compatibilityId = $"sha256:{hash}";
            VehicleFingerprintCache[fullPath] = new FingerprintCacheEntry(
                info.Length,
                info.LastWriteTimeUtc,
                compatibilityId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record FingerprintCacheEntry(
        long Length,
        DateTime LastWriteUtc,
        string CompatibilityId);

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
