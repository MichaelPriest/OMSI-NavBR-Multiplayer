namespace NavBR.Client.Telemetry;

/// <summary>
/// Read-only memory layout used by OMSI 2.3.004.
/// Global addresses are stored as RVAs so the reader also works when the
/// executable is not loaded at its preferred 0x00400000 image base.
/// </summary>
internal static class Omsi23004MemoryProfile
{
    private const int PreferredImageBase = 0x00400000;

    // OMSI globals.
    public const int RoadVehiclesListRva = 0x00861508 - PreferredImageBase;
    public const int PlayerVehicleIndexRva = 0x00861740 - PreferredImageBase;
    public const int MapPointerRva = 0x00861588 - PreferredImageBase;
    public const int NavigationVehiclePointerRva = 0x00862F28 - PreferredImageBase;

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
    // 2.3.004 layouts. These are read-only and are kept separate from the
    // absolute transform so the roadmap renderer can work in map-tile space.
    public const int NavigationTileXOffset = 0x018;
    public const int NavigationTileYOffset = 0x020;

    // TMyOMSIList / TList chain used by the road-vehicle collection.
    public const int OmsiListFListOffset = 0x028;
    public const int TListItemsOffset = 0x004;
}
