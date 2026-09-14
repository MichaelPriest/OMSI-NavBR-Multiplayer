using NavBR.Client.Omsi;

namespace NavBR.Client.Telemetry;

/// <summary>
/// Read-only memory layout used by the supported OMSI executables.
/// Global addresses are stored as RVAs so the reader also works when the
/// executable is not loaded at its preferred 0x00400000 image base.
///
/// OMSI 2.2.032 (tram patch) keeps the same object-field layout used here,
/// but several global pointers are shifted by four bytes compared with
/// 2.3.004. The selected global profile is configured when the process is
/// opened and remains read-only afterwards.
/// </summary>
internal static class Omsi23004MemoryProfile
{
    private const int PreferredImageBase = 0x00400000;

    // OMSI globals. Defaults are the current 2.3.004 profile.
    public static int RoadVehiclesListRva { get; private set; } = 0x00861508 - PreferredImageBase;
    public static int PlayerVehicleIndexRva { get; private set; } = 0x00861740 - PreferredImageBase;
    public static int MapPointerRva { get; private set; } = 0x00861588 - PreferredImageBase;
    public static int NavigationVehiclePointerRva { get; private set; } = 0x00862F28 - PreferredImageBase;

    public static bool ConfigureFor(OmsiProcessInfo processInfo)
    {
        if (processInfo.IsOmsi22032)
        {
            // 2.2.032 / tram patch. TMap and TRVList are documented at
            // 0x861584 and 0x861504 respectively. Adjacent OMSI globals in
            // this build follow the same -4 byte shift from 2.3.004.
            RoadVehiclesListRva = 0x00861504 - PreferredImageBase;
            PlayerVehicleIndexRva = 0x0086173C - PreferredImageBase;
            MapPointerRva = 0x00861584 - PreferredImageBase;
            NavigationVehiclePointerRva = 0x00862F24 - PreferredImageBase;
            return true;
        }

        if (processInfo.IsOmsi23004Exact)
        {
            RoadVehiclesListRva = 0x00861508 - PreferredImageBase;
            PlayerVehicleIndexRva = 0x00861740 - PreferredImageBase;
            MapPointerRva = 0x00861588 - PreferredImageBase;
            NavigationVehiclePointerRva = 0x00862F28 - PreferredImageBase;
            return true;
        }

        return false;
    }

    // OmsiMapObjInst / OmsiPhysObjInst / OmsiMovingMapObjInst fields.
    public const int VehiclePositionOffset = 0x004;
    public const int VehicleRotationOffset = 0x050;
    public const int VehicleAbsPositionOffset = 0x078;
    public const int VehicleVelocityOffset = 0x1C0;
    public const int VehicleGroundSpeedOffset = 0x428;

    // D3DMatrix stores translation in _30/_31/_32 (bytes 0x30/0x34/0x38).
    public const int MatrixTranslationOffset = 0x030;

    // OmsiMap fields.
    public const int MapLoadedOffset = 0x120;
    public const int CurrentGridXOffset = 0x144;
    public const int CurrentGridYOffset = 0x148;
    public const int MapNameOffset = 0x150;
    public const int MapFriendlyNameOffset = 0x158;

    // Navigation/local vehicle coordinates used by OMSI RouteAdvisor-compatible
    // layouts. These are read-only and are kept separate from the absolute
    // transform so the roadmap renderer can work in map-tile space.
    public const int NavigationTileXOffset = 0x018;
    public const int NavigationTileYOffset = 0x020;

    // TMyOMSIList / TList chain used by the road-vehicle collection.
    public const int OmsiListFListOffset = 0x028;
    public const int TListItemsOffset = 0x004;
}
