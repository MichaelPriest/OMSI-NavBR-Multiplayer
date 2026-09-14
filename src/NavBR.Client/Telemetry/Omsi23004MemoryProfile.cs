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

    // OmsiMapObjInst / OmsiPhysObjInst / OmsiMovingMapObjInst fields.
    public const int VehiclePositionOffset = 0x004;
    public const int VehicleRotationOffset = 0x050;
    public const int VehicleVelocityOffset = 0x1C0;
    public const int VehicleGroundSpeedOffset = 0x428;

    // OmsiMap fields.
    public const int MapLoadedOffset = 0x120;
    public const int MapNameOffset = 0x150;
    public const int MapFriendlyNameOffset = 0x158;

    // TMyOMSIList / TList chain used by the road-vehicle collection.
    public const int OmsiListFListOffset = 0x028;
    public const int TListItemsOffset = 0x004;
}
