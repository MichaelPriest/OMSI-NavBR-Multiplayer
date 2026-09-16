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
    private const int ExpectedAbiVersion = 2;
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

                var version = FileVersionInfo.GetVersionInfo(executable).FileVersion;
                return !string.IsNullOrWhiteSpace(version) &&
                       (version.Contains("2.3.004", StringComparison.OrdinalIgnoreCase) ||
                        version.Contains("2.3.4", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
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
                       ProbeOmsi23004Addresses() == 1;
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

    internal static bool TryFindNewRoadVehicle(
        IReadOnlyCollection<int> before,
        out int vehiclePointer)
    {
        vehiclePointer = 0;
        if (!TrySnapshotRoadVehicles(out var after))
        {
            return false;
        }

        var known = new HashSet<int>(before);
        var added = after.Where(pointer => !known.Contains(pointer)).Distinct().ToArray();
        if (added.Length != 1 || added[0] == 0 || IsRoadVehiclePointer(added[0]) != 1)
        {
            return false;
        }

        vehiclePointer = added[0];
        return true;
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

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_ProbeOmsi23004Addresses")]
    private static extern int ProbeOmsi23004Addresses();

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

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_IsRoadVehiclePointer")]
    internal static extern int IsRoadVehiclePointer(int vehiclePointer);

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
        float groundSpeedMps);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_SetVehicleVisualState")]
    internal static extern int SetVehicleVisualState(
        int vehiclePointer,
        int lightFlags,
        int turnSignal);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NavBR_MarkVehicleForKilling")]
    internal static extern int MarkVehicleForKilling(int vehiclePointer);
}
