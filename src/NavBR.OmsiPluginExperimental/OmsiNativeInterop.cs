using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NavBR.OmsiPluginExperimental;

/// <summary>
/// Narrow P/Invoke boundary for the x86 helper that translates ordinary C
/// calls into OMSI's Borland register ABI. Nothing here is invoked unless the
/// physical-write backend is explicitly enabled and the runtime passes the
/// 2.3.004 guards.
/// </summary>
internal static class OmsiNativeInterop
{
    private const string LibraryName = "NavBR.OmsiInterop.dll";
    private const int ExpectedAbiVersion = 1;
    private const int ExpectedStateInteropVersion = 14;
    internal const int HostPlayerTileSentinel = -2;
    private const int MaxReasonableHumans = 8192;
    private const int MaxReasonableRoadVehicles = 4096;
    private static readonly object ShimLoadSync = new();
    private static nint _shimHandle;

    public static bool IsCandidateOmsi23004Runtime
    {
        get
        {
            if (Environment.Is64BitProcess ||
                !string.Equals(
                    Environment.ProcessPath is { } path
                        ? Path.GetFileNameWithoutExtension(path)
                        : null,
                    "Omsi",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var executable = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executable))
                {
                    return false;
                }

                var fileVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion;
                if (IsOmsi23004Version(fileVersion))
                {
                    return true;
                }

                // Keep the plugin runtime gate consistent with the desktop
                // detector. Patched/repacked OMSI executables can retain stale
                // PE FileVersion metadata while logfile.txt reports the actual
                // running simulator version.
                var omsiRoot = Path.GetDirectoryName(executable);
                return !string.IsNullOrWhiteSpace(omsiRoot) &&
                       IsOmsi23004Version(TryReadRuntimeVersionFromLog(omsiRoot));
            }
            catch
            {
                return false;
            }
        }
    }

    // Compatibility is runtime/ABI based, never Steam-authentication based.
    // A non-Steam or repacked installation is not rejected for its source.
    // We still require the OMSI 2.3.004 ABI/version evidence before enabling
    // native writes because using these offsets on another build is unsafe.
    private static bool IsOmsi23004Version(string? version) =>
        !string.IsNullOrWhiteSpace(version) &&
        (version.Contains("2.3.004", StringComparison.OrdinalIgnoreCase) ||
         version.Contains("2.3.4", StringComparison.OrdinalIgnoreCase));

    private static string? TryReadRuntimeVersionFromLog(string omsiRoot)
    {
        var logfilePath = Path.Combine(omsiRoot, "logfile.txt");
        if (!File.Exists(logfilePath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(
                logfilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(
                stream,
                detectEncodingFromByteOrderMarks: true);

            for (var index = 0; index < 64 && reader.ReadLine() is { } line; index++)
            {
                var marker = line.IndexOf(
                    "Version:",
                    StringComparison.OrdinalIgnoreCase);
                if (marker < 0)
                {
                    continue;
                }

                var value = line[(marker + "Version:".Length)..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    public static bool IsShimReady
    {
        get
        {
            if (!IsCandidateOmsi23004Runtime || !EnsureShimLoaded())
            {
                return false;
            }

            try
            {
                return GetAbiVersion() == ExpectedAbiVersion &&
                       GetStateInteropVersion() == ExpectedStateInteropVersion &&
                       ProbePhysicalVehicleBackend() == 1;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }
    }

    public static bool IsRoleplayShimReady
    {
        get
        {
            if (!IsCandidateOmsi23004Runtime || !EnsureShimLoaded())
            {
                return false;
            }

            try
            {
                return GetAbiVersion() == ExpectedAbiVersion &&
                       GetStateInteropVersion() == ExpectedStateInteropVersion &&
                       ProbeRoleplayHumanControl() == 1;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }
    }

    internal static bool TrySnapshotRoadVehicles(out int[] vehiclePointers)
    {
        vehiclePointers = [];
        if (!IsShimReady)
        {
            return false;
        }

        try
        {
            var count = GetRoadVehicleCount();
            if (count < 0 || count > MaxReasonableRoadVehicles)
            {
                return false;
            }

            if (count == 0)
            {
                return true;
            }

            var pointers = new int[count];
            for (var index = 0; index < count; index++)
            {
                var pointer = GetRoadVehicleAt(index);
                if (pointer == 0)
                {
                    return false;
                }

                pointers[index] = pointer;
            }

            vehiclePointers = pointers;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    internal static bool TryReadRoadVehicleRenderDiagnostics(
        int vehiclePointer,
        out RoadVehicleRenderDiagnostics diagnostics)
    {
        diagnostics = default;
        if (!IsShimReady)
        {
            return false;
        }

        try
        {
            if (ReadRoadVehicleRenderDiagnostics(
                    vehiclePointer,
                    out var visibleLogical,
                    out var visibleLogicalRenderThread,
                    out var roadVehicleDefinitionPointer,
                    out var complObjPointer,
                    out var complObjVisible,
                    out var complObjRenderMe,
                    out var modelStringPointer,
                    out var kachelPointer,
                    out var mapTileIndex,
                    out var renderX,
                    out var renderY,
                    out var renderZ,
                    out var hostDistance) != 1)
            {
                return false;
            }

            diagnostics = new RoadVehicleRenderDiagnostics(
                visibleLogical,
                visibleLogicalRenderThread,
                roadVehicleDefinitionPointer,
                complObjPointer,
                complObjVisible,
                complObjRenderMe,
                modelStringPointer,
                kachelPointer,
                mapTileIndex,
                renderX,
                renderY,
                renderZ,
                hostDistance);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    internal static bool TrySnapshotTempRoadVehicles(
        int tempList,
        out int[] vehiclePointers)
    {
        vehiclePointers = [];
        if (!IsShimReady || tempList == 0)
        {
            return false;
        }

        try
        {
            var count = GetTempRoadVehicleCount(tempList);
            if (count < 0 || count > 32)
            {
                return false;
            }

            if (count == 0)
            {
                return true;
            }

            var pointers = new int[count];
            for (var index = 0; index < count; index++)
            {
                var pointer = GetTempRoadVehicleAt(tempList, index);
                if (pointer == 0)
                {
                    return false;
                }

                pointers[index] = pointer;
            }

            vehiclePointers = pointers;
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    internal static bool TrySnapshotHumans(out int[] humanPointers)
    {
        humanPointers = [];
        if (!IsRoleplayShimReady)
        {
            return false;
        }

        try
        {
            var count = GetHumanCount();
            if (count < 0 || count > MaxReasonableHumans)
            {
                return false;
            }

            if (count == 0)
            {
                return true;
            }

            var pointers = new List<int>(count);
            for (var index = 0; index < count; index++)
            {
                var pointer = GetHumanAt(index);
                if (pointer != 0 && IsHumanPointer(pointer) == 1)
                {
                    pointers.Add(pointer);
                }
            }

            humanPointers = pointers.ToArray();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    internal static bool TryRunPlaceRandomBusProbe(
        out RandomBusProbeResult result)
    {
        result = new RandomBusProbeResult(
            false,
            int.MinValue,
            -1,
            -1,
            0,
            "probe-unavailable");

        if (!IsShimReady ||
            !TrySnapshotRoadVehicles(out var before))
        {
            return false;
        }

        try
        {
            var rawReturn = PlaceRandomBusProbe(
                aiType: 0,
                group: 1,
                vehicleType: -1,
                scheduled: 0,
                tour: 0,
                line: 0);

            if (!TrySnapshotRoadVehicles(out var after))
            {
                result = result with
                {
                    RawReturn = rawReturn,
                    BeforeCount = before.Length,
                    Detail = "after-snapshot-unavailable"
                };
                return false;
            }

            var known = new HashSet<int>(before);
            var added = after
                .Where(pointer => pointer != 0 && !known.Contains(pointer))
                .Distinct()
                .ToArray();

            foreach (var pointer in added)
            {
                if (IsRoadVehiclePointer(pointer) == 1)
                {
                    _ = MarkVehicleForKilling(pointer);
                }
            }

            result = new RandomBusProbeResult(
                true,
                rawReturn,
                before.Length,
                after.Length,
                added.Length,
                added.Length > 0
                    ? "roadvehicles-delta-observed"
                    : "no-roadvehicles-delta");
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    internal static bool TryFindNewRoadVehicles(
        IReadOnlyCollection<int> before,
        out int[] vehiclePointers)
    {
        vehiclePointers = [];
        if (!TrySnapshotRoadVehicles(out var after))
        {
            return false;
        }

        var known = new HashSet<int>(before);
        var added = after
            .Where(pointer => pointer != 0 && !known.Contains(pointer))
            .Distinct()
            .ToArray();

        if (added.Length == 0)
        {
            return false;
        }

        var valid = added
            .Where(pointer => IsRoadVehiclePointer(pointer) == 1)
            .ToArray();
        vehiclePointers = valid;
        return valid.Length == added.Length;
    }

    internal static bool TryResolveMakeVehicleResult(
        int makeVehicleResult,
        IReadOnlyCollection<int> before,
        out int vehiclePointer)
    {
        vehiclePointer = 0;
        if (!IsShimReady)
        {
            return false;
        }

        try
        {
            var known = new HashSet<int>(before);

            // Some reverse-engineering references expose MakeVehicle's return
            // value as the created object/ID. Accept a direct pointer only when
            // it is a live RoadVehicle and was not present before this spawn.
            if (makeVehicleResult > 0 &&
                !known.Contains(makeVehicleResult) &&
                IsRoadVehiclePointer(makeVehicleResult) == 1)
            {
                vehiclePointer = makeVehicleResult;
                return true;
            }

            var count = GetRoadVehicleCount();
            if (count <= 0 || count > MaxReasonableRoadVehicles)
            {
                return false;
            }

            // Other wrappers describe the same return as a vehicle ID/index.
            // Validate both zero-based and one-based interpretations against
            // the live main list and the pre-spawn snapshot before accepting.
            // This is intentionally a fallback: the direct pointer path above
            // remains authoritative whenever OMSI exposes one.
            var candidateIndexes = new[]
            {
                makeVehicleResult,
                makeVehicleResult - 1
            }
            .Distinct()
            .Where(index => index >= 0 && index < count);

            foreach (var index in candidateIndexes)
            {
                var pointer = GetRoadVehicleAt(index);
                if (pointer != 0 &&
                    !known.Contains(pointer) &&
                    IsRoadVehiclePointer(pointer) == 1)
                {
                    vehiclePointer = pointer;
                    return true;
                }
            }

            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static bool EnsureShimLoaded()
    {
        lock (ShimLoadSync)
        {
            if (_shimHandle != 0)
            {
                return true;
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

                var shimPath = Path.Combine(omsiRoot, "plugins", LibraryName);
                if (!File.Exists(shimPath))
                {
                    return false;
                }

                _shimHandle = NativeLibrary.Load(shimPath);
                return _shimHandle != 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (FileLoadException)
            {
                return false;
            }
        }
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetAbiVersion")]
    private static extern int GetAbiVersion();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetStateInteropVersion")]
    private static extern int GetStateInteropVersion();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetLastVehicleTransformFailureStage")]
    internal static extern int GetLastVehicleTransformFailureStage();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ProbeOmsi23004Addresses")]
    private static extern int ProbeOmsi23004Addresses();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ProbeRoleplayHumanControl")]
    private static extern int ProbeRoleplayHumanControl();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ProbePhysicalVehicleBackend")]
    private static extern int ProbePhysicalVehicleBackend();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetImageBase")]
    internal static extern uint GetImageBase();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetProgramManager")]
    internal static extern int GetProgramManager();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetRoadVehicleTypes")]
    internal static extern int GetRoadVehicleTypes();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetRoadVehicleCount")]
    private static extern int GetRoadVehicleCount();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetRoadVehicleAt")]
    private static extern int GetRoadVehicleAt(int index);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetTempRoadVehicleCount")]
    private static extern int GetTempRoadVehicleCount(int tempList);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetTempRoadVehicleAt")]
    private static extern int GetTempRoadVehicleAt(int tempList, int index);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetRoadVehicleMaterializationFlags")]
    internal static extern int GetRoadVehicleMaterializationFlags(int vehiclePointer);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ReadRoadVehicleRenderDiagnostics")]
    private static extern int ReadRoadVehicleRenderDiagnostics(
        int vehiclePointer,
        out int visibleLogical,
        out int visibleLogicalRenderThread,
        out int roadVehicleDefinitionPointer,
        out int complObjPointer,
        out int complObjVisible,
        out int complObjRenderMe,
        out int modelStringPointer,
        out int kachelPointer,
        out int mapTileIndex,
        out float renderX,
        out float renderY,
        out float renderZ,
        out float hostDistance);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_IsRoadVehiclePointer")]
    internal static extern int IsRoadVehiclePointer(int vehiclePointer);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_IsMapTileIndexValid")]
    internal static extern int IsMapTileIndexValid(int mapTileIndex);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ResolveMapTileIndex")]
    internal static extern int ResolveMapTileIndex(int gridX, int gridY);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetHumanCount")]
    private static extern int GetHumanCount();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetHumanAt")]
    private static extern int GetHumanAt(int index);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_IsHumanPointer")]
    internal static extern int IsHumanPointer(int humanPointer);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_IsHumanControllable")]
    internal static extern int IsHumanControllable(int humanPointer);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetPlayerVehiclePointer")]
    internal static extern int GetPlayerVehiclePointer();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ReadRoadVehiclePosition")]
    internal static extern int ReadRoadVehiclePosition(
        int vehiclePointer,
        out float x,
        out float y,
        out float z);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ReadPlayerVehicleGrid")]
    internal static extern int ReadPlayerVehicleGrid(
        out int gridX,
        out int gridY,
        out int mapTileIndex);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ReadRoadVehicleTileIndex")]
    internal static extern int ReadRoadVehicleTileIndex(int vehiclePointer);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_IsPlayerBusDriverHuman")]
    internal static extern int IsPlayerBusDriverHuman(
        int humanPointer,
        int definitionPointer);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ReadHumanPose")]
    internal static extern int ReadHumanPose(
        int humanPointer,
        out float x,
        out float y,
        out float z,
        out float heading,
        out float speed);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ReadHumanDriverState")]
    internal static extern int ReadHumanDriverState(
        int humanPointer,
        out int myBus,
        out byte fixDriver,
        out byte renderMe,
        out byte inWorld);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_DetachHumanForRoleplay")]
    internal static extern int DetachHumanForRoleplay(int humanPointer);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_RestoreHumanDriverState")]
    internal static extern int RestoreHumanDriverState(
        int humanPointer,
        int myBus,
        byte fixDriver,
        byte renderMe,
        byte inWorld);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ReadHumanAiState")]
    internal static extern int ReadHumanAiState(
        int humanPointer,
        out byte aiMode,
        out byte aiModeEx,
        out byte aiSubMode,
        out float sollSpeed,
        out float actSpeed);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ReadHumanAnimationState")]
    internal static extern int ReadHumanAnimationState(
        int humanPointer,
        out float lastMovedDist,
        out float state);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ReadHumanActivityState")]
    internal static extern int ReadHumanActivityState(
        int humanPointer,
        out byte activityLeg,
        out byte activityArmUmbrella,
        out byte activityArmKi,
        out byte activityHeadKi);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_SetHumanTransform")]
    internal static extern int SetHumanTransform(
        int humanPointer,
        float x,
        float y,
        float z,
        float headingDegrees,
        float speedMps);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_RestoreHumanAiState")]
    internal static extern int RestoreHumanAiState(
        int humanPointer,
        byte aiMode,
        byte aiModeEx,
        byte aiSubMode,
        float sollSpeed,
        float actSpeed);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_PlaceRandomBusProbe")]
    private static extern int PlaceRandomBusProbe(
        int aiType,
        int group,
        int vehicleType,
        int scheduled,
        int tour,
        int line);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_GetMem")]
    internal static extern int GetMem(int bytes);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_FreeMem")]
    internal static extern int FreeMem(int address);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_LockMakeVehicle")]
    internal static extern int LockMakeVehicle(int programManager);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_UnlockMakeVehicle")]
    internal static extern int UnlockMakeVehicle(int programManager);

    [DllImport(
        LibraryName,
        CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Unicode,
        EntryPoint = "NavBR_AllocateAnsiString")]
    internal static extern int AllocateAnsiString(string value);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_FreeAnsiString")]
    internal static extern int FreeAnsiString(int stringData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_TriggerRoadVehicle")]
    internal static extern int TriggerRoadVehicle(
        int vehiclePointer,
        int triggerAnsiString,
        int active);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_TempRoadVehicleListCreate")]
    internal static extern int TempRoadVehicleListCreate(int capacity);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_CopyTempRoadVehicleListIntoMain")]
    internal static extern int CopyTempRoadVehicleListIntoMain(int tempList);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_MakeVehicle")]
    internal static extern int MakeVehicle(
        int programManager,
        int vehicleList,
        int roadVehicleTypes,
        int onlyVehicleList,
        int cs,
        int timetableTimeBits,
        int situationLoad,
        int dialog,
        int setDriver,
        int thread,
        int licensePlateIndex,
        int initCall,
        int startDay,
        int trainBuildDirection,
        int reverse,
        int groupHof,
        int type,
        int tour,
        int line,
        int paintScheme,
        int scheduled,
        int aiRoadVehicle,
        int randomLicensePlate,
        int randomPaintScheme,
        int filenameAnsiString);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_SetVehicleTransform")]
    internal static extern int SetVehicleTransform(
        int vehiclePointer,
        float x,
        float y,
        float z,
        float rotationX,
        float rotationY,
        float rotationZ,
        float rotationW,
        float groundSpeedMps,
        int mapTileIndex);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_SetVehicleVisualState")]
    internal static extern int SetVehicleVisualState(
        int vehiclePointer,
        int lightFlags,
        int turnSignal);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_MarkVehicleForKilling")]
    internal static extern int MarkVehicleForKilling(int vehiclePointer);

    internal readonly record struct RoadVehicleRenderDiagnostics(
        int VisibleLogical,
        int VisibleLogicalRenderThread,
        int RoadVehicleDefinitionPointer,
        int ComplObjPointer,
        int ComplObjVisible,
        int ComplObjRenderMe,
        int ModelStringPointer,
        int KachelPointer,
        int MapTileIndex,
        float RenderX,
        float RenderY,
        float RenderZ,
        float HostDistance);

    internal sealed record RandomBusProbeResult(
        bool Invoked,
        int RawReturn,
        int BeforeCount,
        int AfterCount,
        int DeltaCount,
        string Detail);
}
