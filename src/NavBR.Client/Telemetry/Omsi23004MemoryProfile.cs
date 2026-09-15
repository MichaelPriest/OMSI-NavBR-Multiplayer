using NavBR.Client.Omsi;

namespace NavBR.Client.Telemetry;

/// <summary>
/// Read-only memory layout used by the supported OMSI executables.
/// Global addresses are stored as RVAs so the reader also works when the
/// executable is not loaded at its preferred 0x00400000 image base.
/// </summary>
internal static class Omsi23004MemoryProfile
{
    private const int PreferredImageBase = 0x00400000;

    public static int RoadVehiclesListRva { get; private set; } = 0x00861508 - PreferredImageBase;
    public static int PlayerVehicleIndexRva { get; private set; } = 0x00861740 - PreferredImageBase;
    public static int MapPointerRva { get; private set; } = 0x00861588 - PreferredImageBase;
    public static int TimeTableManagerRva { get; private set; } = 0x008614E8 - PreferredImageBase;
    public static int NavigationVehiclePointerRva { get; private set; } = 0x00862F28 - PreferredImageBase;

    public static bool ConfigureFor(OmsiProcessInfo processInfo)
    {
        if (processInfo.IsOmsi22032)
        {
            RoadVehiclesListRva = 0x00861504 - PreferredImageBase;
            PlayerVehicleIndexRva = 0x0086173C - PreferredImageBase;
            MapPointerRva = 0x00861584 - PreferredImageBase;
            TimeTableManagerRva = 0x008614E4 - PreferredImageBase;
            NavigationVehiclePointerRva = 0x00862F24 - PreferredImageBase;
            return true;
        }

        if (processInfo.IsOmsi23004Exact)
        {
            RoadVehiclesListRva = 0x00861508 - PreferredImageBase;
            PlayerVehicleIndexRva = 0x00861740 - PreferredImageBase;
            MapPointerRva = 0x00861588 - PreferredImageBase;
            TimeTableManagerRva = 0x008614E8 - PreferredImageBase;
            NavigationVehiclePointerRva = 0x00862F28 - PreferredImageBase;
            return true;
        }

        return false;
    }

    // OmsiMapObjInst / OmsiPhysObjInst / OmsiMovingMapObjInst fields.
    public const int VehiclePositionOffset = 0x004;
    public const int VehicleRotationOffset = 0x050;
    public const int VehicleKachelOffset = 0x074;
    public const int VehicleAbsPositionOffset = 0x078;
    public const int VehicleVelocityOffset = 0x1C0;
    public const int MovingVehicleIndexOffset = 0x258;
    public const int MovingVehicleUserTrainOffset = 0x26C;
    public const int VehicleGroundSpeedOffset = 0x428;

    // File/object identity. OmsiComplMapObjInst.MyFileObject points to the
    // source object entry and OmsiRoadVehicleInst.RoadVehicle points to the
    // loaded .bus/.ovh definition. These reads are used only for compatibility
    // fingerprints; the external telemetry provider remains read-only.
    public const int VehicleFileObjectOffset = 0x1E8;
    public const int FileObjectPathOffset = 0x018;
    public const int RoadVehicleDefinitionOffset = 0x710;
    public const int RoadVehicleFriendlyNameOffset = 0x19C;
    public const int RoadVehicleMyPathOffset = 0x1A8;

    // Driver controls / AI visual state exposed on OmsiVehicleInst. These are
    // read-only in NavBR and guarded by range checks before entering telemetry.
    public const int VehicleThrottleOffset = 0x5DC;
    public const int VehicleBrakePedalOffset = 0x5E0;
    public const int VehicleAiLightOffset = 0x634;
    public const int VehicleAiInteriorLightOffset = 0x638;
    public const int VehicleAiBlinkerLeftOffset = 0x63C;
    public const int VehicleAiBlinkerRightOffset = 0x640;
    public const int VehicleAiBrakeLightOffset = 0x644;

    // Active timetable state on TRVInst (read-only).
    public const int VehicleScheduleInfoValidOffset = 0x65C;
    public const int VehicleScheduleLineIndexOffset = 0x660;
    public const int VehicleScheduleTripIndexOffset = 0x66C;
    public const int VehicleScheduleTargetIndexOffset = 0x674;
    public const int VehicleScheduleNextStopOffset = 0x680;
    public const int VehicleScheduleNextStopIndexOffset = 0x6A8;
    public const int VehicleScheduleNextStopNameOffset = 0x6AC;
    public const int VehicleScheduleDelayOffset = 0x6BC;

    // OmsiRoadVehicleInst runtime state.
    public const int VehicleCurrentStationOffset = 0x7A0;
    public const int VehicleFuelPercentOffset = 0x7CC;

    // TTimeTableMan dynamic arrays and TTTTrip layout.
    public const int TimeTableTripsOffset = 0x00C;
    public const int TimeTableLinesOffset = 0x018;
    public const int TripRecordSize = 0x028;
    public const int TripFilenameOffset = 0x000;
    public const int TripTargetOffset = 0x008;
    public const int TripLineNameOffset = 0x00C;
    public const int TripBusStopsOffset = 0x018;
    public const int TripTrackNameOffset = 0x01C;
    public const int TripTrackIndexOffset = 0x020;
    public const int TripStationLinkListOffset = 0x024;

    public const int MatrixTranslationOffset = 0x030;

    // OmsiMap fields.
    public const int MapLoadedOffset = 0x120;
    public const int CurrentGridXOffset = 0x144;
    public const int CurrentGridYOffset = 0x148;
    public const int MapNameOffset = 0x150;
    public const int MapFriendlyNameOffset = 0x158;

    public const int NavigationTileXOffset = 0x018;
    public const int NavigationTileYOffset = 0x020;

    // OmsiMyOmsiList<T> -> TList backing store.
    public const int OmsiListFListOffset = 0x028;
    public const int OmsiListCountOffset = 0x02C;
    public const int TListItemsOffset = 0x004;
}
