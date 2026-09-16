using System.Diagnostics;
using NavBR.Client.Omsi;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Telemetry;

/// <summary>
/// External, read-only telemetry provider for supported OMSI executables.
/// No Steam API and no code injection are required.
/// </summary>
public sealed class Omsi23004TelemetryProvider : ITelemetryProvider
{
    private ReadOnlyProcessMemory? _memory;
    private OmsiProcessInfo? _processInfo;

    public bool IsAttached => _memory is not null;
    public int? AttachedProcessId => _memory?.ProcessId;
    public TelemetryErrorCode LastErrorCode { get; private set; }

    public Task<bool> AttachAsync(
        OmsiProcessInfo processInfo,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DisposeMemory();

        if (!processInfo.IsTelemetrySupported)
        {
            LastErrorCode = TelemetryErrorCode.UnsupportedVersion;
            return Task.FromResult(false);
        }

        try
        {
            _memory = ReadOnlyProcessMemory.Open(processInfo);
            _processInfo = processInfo;
            LastErrorCode = TelemetryErrorCode.None;
            return Task.FromResult(true);
        }
        catch
        {
            DisposeMemory();
            LastErrorCode = TelemetryErrorCode.AttachFailed;
            return Task.FromResult(false);
        }
    }

    public IReadOnlyList<TrafficVehicleState> ReadRoadTraffic(
        int maxVehicles = OmsiRoadTrafficReader.DefaultMaxVehicles,
        double radiusMeters = OmsiRoadTrafficReader.DefaultRadiusMeters)
    {
        var memory = _memory;
        var processInfo = _processInfo;
        if (memory is null || processInfo is null || !processInfo.IsOmsi23004Exact)
        {
            return Array.Empty<TrafficVehicleState>();
        }

        try
        {
            if (Process.GetProcessById(memory.ProcessId).HasExited)
            {
                return Array.Empty<TrafficVehicleState>();
            }

            return OmsiRoadTrafficReader.Read(
                memory,
                processInfo,
                maxVehicles,
                radiusMeters);
        }
        catch
        {
            return Array.Empty<TrafficVehicleState>();
        }
    }

    public VehicleTelemetry? Read(string playerId)
    {
        var memory = _memory;
        if (memory is null)
        {
            LastErrorCode = TelemetryErrorCode.AttachFailed;
            return null;
        }

        try
        {
            if (Process.GetProcessById(memory.ProcessId).HasExited)
            {
                LastErrorCode = TelemetryErrorCode.ProcessExited;
                DisposeMemory();
                return null;
            }

            var playerVehicleIndex = memory.ReadInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.PlayerVehicleIndexRva));

            if (playerVehicleIndex < 0 || playerVehicleIndex > 10000)
            {
                LastErrorCode = TelemetryErrorCode.NoVehicle;
                return null;
            }

            var vehicleAddress = ResolvePlayerVehicleAddress(memory, playerVehicleIndex);
            if (vehicleAddress == nint.Zero)
            {
                LastErrorCode = TelemetryErrorCode.NoVehicle;
                return null;
            }

            // Keep both representations. AbsPosition is useful for cross-tile
            // distance calculations, while Position/Rotation are the exact
            // native pose that the alpha.11 physical multiplayer backend can
            // apply to another OMSI instance without guessing coordinate axes.
            var localPosition = memory.ReadVector3(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehiclePositionOffset));

            var absolutePosition = memory.ReadVector3(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleAbsPositionOffset +
                Omsi23004MemoryProfile.MatrixTranslationOffset));

            var rotation = memory.ReadQuaternion(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleRotationOffset));

            // OmsiPhysObjInst.Velocity lives at 0x174. The previous profile
            // incorrectly read 0x1C0 (Turn_Velocity), which can stay near zero
            // while the bus travels in a straight line and made the HUD report
            // 0 km/h. Groundspeed is an independent OMSI scalar and is used as
            // the preferred display value when it is finite/plausible.
            var velocity = memory.ReadVector3(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleVelocityOffset));

            var linearSpeedMps = Math.Sqrt(
                velocity.X * velocity.X +
                velocity.Y * velocity.Y +
                velocity.Z * velocity.Z);

            var groundSpeedMps = Math.Abs(memory.ReadSingle(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleGroundSpeedOffset)));

            var speedMps = ResolveVehicleSpeedMps(linearSpeedMps, groundSpeedMps);

            var mapName = TryReadMapName(memory, out var mapLoaded);
            var heading = QuaternionToHeadingDegrees(rotation);
            var vehicleIdentity = OmsiVehicleIdentityReader.Read(memory, _processInfo, vehicleAddress);

            int? gridX = null;
            int? gridY = null;
            double? tileX = null;
            double? tileY = null;
            if (TryReadNavigationPosition(memory, out var gx, out var gy, out var tx, out var ty))
            {
                gridX = gx;
                gridY = gy;
                tileX = tx;
                tileY = ty;
            }

            string? line = null;
            string? route = null;
            string? nextStopName = null;
            string? destinationName = null;
            TryReadActiveTrip(
                memory,
                vehicleAddress,
                out line,
                out route,
                out nextStopName,
                out destinationName);

            LastErrorCode = TelemetryErrorCode.None;
            return new VehicleTelemetry(
                PlayerId: playerId,
                Timestamp: DateTimeOffset.UtcNow,
                MapName: mapName,
                VehicleName: vehicleIdentity.Name,
                Line: line,
                Route: route,
                X: absolutePosition.X,
                Y: absolutePosition.Y,
                Z: absolutePosition.Z,
                HeadingDegrees: heading,
                SpeedKph: speedMps * 3.6,
                IsInGame: mapLoaded,
                GridX: gridX,
                GridY: gridY,
                TileX: tileX,
                TileY: tileY,
                NextStopName: nextStopName,
                DestinationName: destinationName,
                VehiclePath: vehicleIdentity.RelativePath,
                VehicleCompatibilityId: vehicleIdentity.CompatibilityId,
                LocalX: localPosition.X,
                LocalY: localPosition.Y,
                LocalZ: localPosition.Z,
                RotationX: rotation.X,
                RotationY: rotation.Y,
                RotationZ: rotation.Z,
                RotationW: rotation.W);
        }
        catch (ArgumentException)
        {
            LastErrorCode = TelemetryErrorCode.ProcessExited;
            DisposeMemory();
            return null;
        }
        catch
        {
            LastErrorCode = TelemetryErrorCode.ReadFailed;
            return null;
        }
    }

    private static double ResolveVehicleSpeedMps(double linearSpeedMps, double groundSpeedMps)
    {
        var linearValid = double.IsFinite(linearSpeedMps) && linearSpeedMps >= 0d && linearSpeedMps <= 150d;
        var groundValid = double.IsFinite(groundSpeedMps) && groundSpeedMps >= 0d && groundSpeedMps <= 150d;

        if (groundValid)
        {
            // Groundspeed is the scalar OMSI itself maintains for the moving
            // vehicle and is the best source for a speedometer/HUD. If a
            // particular vehicle leaves it at zero while the physics velocity
            // is clearly moving, fall back to the vector magnitude.
            if (groundSpeedMps > 0.05d || !linearValid || linearSpeedMps <= 0.05d)
            {
                return groundSpeedMps;
            }
        }

        if (linearValid)
        {
            return linearSpeedMps;
        }

        return groundValid ? groundSpeedMps : 0d;
    }

    private static nint ResolvePlayerVehicleAddress(
        ReadOnlyProcessMemory memory,
        int playerVehicleIndex)
    {
        var omsiListAddress = memory.ReadUInt32(
            memory.AddressFromRva(Omsi23004MemoryProfile.RoadVehiclesListRva));
        if (omsiListAddress <= 0x10000u)
        {
            return nint.Zero;
        }

        var omsiListPointer = ReadOnlyProcessMemory.PointerFromUInt32(omsiListAddress);
        var fListAddress = memory.ReadUInt32(nint.Add(
            omsiListPointer,
            Omsi23004MemoryProfile.OmsiListFListOffset));
        if (fListAddress <= 0x10000u)
        {
            return nint.Zero;
        }

        var fListPointer = ReadOnlyProcessMemory.PointerFromUInt32(fListAddress);
        var itemsAddress = memory.ReadUInt32(nint.Add(
            fListPointer,
            Omsi23004MemoryProfile.TListItemsOffset));
        if (itemsAddress <= 0x10000u)
        {
            return nint.Zero;
        }

        var itemsPointer = ReadOnlyProcessMemory.PointerFromUInt32(itemsAddress);
        var vehicleAddress = memory.ReadUInt32(nint.Add(
            itemsPointer,
            checked(playerVehicleIndex * sizeof(int))));

        return vehicleAddress > 0x10000u
            ? ReadOnlyProcessMemory.PointerFromUInt32(vehicleAddress)
            : nint.Zero;
    }

    private static bool TryReadActiveTrip(
        ReadOnlyProcessMemory memory,
        nint vehicleAddress,
        out string? line,
        out string? route,
        out string? nextStopName,
        out string? destinationName)
    {
        line = null;
        route = null;
        nextStopName = null;
        destinationName = null;

        try
        {
            if (memory.ReadByte(nint.Add(
                    vehicleAddress,
                    Omsi23004MemoryProfile.VehicleScheduleInfoValidOffset)) == 0)
            {
                return false;
            }

            nextStopName = memory.ReadNullTerminatedUnicodeStringField(
                               nint.Add(vehicleAddress, Omsi23004MemoryProfile.VehicleScheduleNextStopNameOffset),
                               maxCharacters: 128)
                           ?? memory.ReadNullTerminatedAnsiStringField(
                               nint.Add(vehicleAddress, Omsi23004MemoryProfile.VehicleScheduleNextStopNameOffset),
                               maxCharacters: 128);

            var tripIndex = memory.ReadInt32(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleScheduleTripIndexOffset));
            if (tripIndex < 0 || tripIndex > 100000)
            {
                return false;
            }

            var timeTableAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.TimeTableManagerRva));
            if (timeTableAddress <= 0x10000u)
            {
                return false;
            }

            var timeTablePointer = ReadOnlyProcessMemory.PointerFromUInt32(timeTableAddress);
            var tripsAddress = memory.ReadUInt32(nint.Add(
                timeTablePointer,
                Omsi23004MemoryProfile.TimeTableTripsOffset));
            if (tripsAddress <= 0x10000u)
            {
                return false;
            }

            var tripsPointer = ReadOnlyProcessMemory.PointerFromUInt32(tripsAddress);
            try
            {
                var count = memory.ReadInt32(nint.Subtract(tripsPointer, sizeof(int)));
                if (count > 0 && count < 100000 && tripIndex >= count)
                {
                    return false;
                }
            }
            catch
            {
                // Bounds check only; patched runtimes can store headers differently.
            }

            var tripPointer = nint.Add(
                tripsPointer,
                checked(tripIndex * Omsi23004MemoryProfile.TripRecordSize));

            line = memory.ReadNullTerminatedAnsiStringField(nint.Add(
                tripPointer,
                Omsi23004MemoryProfile.TripLineNameOffset));
            var trackName = memory.ReadNullTerminatedAnsiStringField(nint.Add(
                tripPointer,
                Omsi23004MemoryProfile.TripTrackNameOffset));
            destinationName = memory.ReadNullTerminatedAnsiStringField(nint.Add(
                tripPointer,
                Omsi23004MemoryProfile.TripTargetOffset));

            route = !string.IsNullOrWhiteSpace(trackName) ? trackName : destinationName;
            return !string.IsNullOrWhiteSpace(line) ||
                   !string.IsNullOrWhiteSpace(route) ||
                   !string.IsNullOrWhiteSpace(nextStopName) ||
                   !string.IsNullOrWhiteSpace(destinationName);
        }
        catch
        {
            line = null;
            route = null;
            nextStopName = null;
            destinationName = null;
            return false;
        }
    }

    private static bool TryReadNavigationPosition(
        ReadOnlyProcessMemory memory,
        out int gridX,
        out int gridY,
        out double tileX,
        out double tileY)
    {
        gridX = 0;
        gridY = 0;
        tileX = 0;
        tileY = 0;

        try
        {
            var mapAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.MapPointerRva));
            var navigationVehicleAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.NavigationVehiclePointerRva));

            if (mapAddress <= 0x10000u || navigationVehicleAddress <= 0x10000u)
            {
                return false;
            }

            var mapPointer = ReadOnlyProcessMemory.PointerFromUInt32(mapAddress);
            var navigationVehiclePointer = ReadOnlyProcessMemory.PointerFromUInt32(navigationVehicleAddress);

            gridX = memory.ReadInt32(nint.Add(
                mapPointer,
                Omsi23004MemoryProfile.CurrentGridXOffset));
            gridY = memory.ReadInt32(nint.Add(
                mapPointer,
                Omsi23004MemoryProfile.CurrentGridYOffset));
            tileX = memory.ReadSingle(nint.Add(
                navigationVehiclePointer,
                Omsi23004MemoryProfile.NavigationTileXOffset));
            tileY = memory.ReadSingle(nint.Add(
                navigationVehiclePointer,
                Omsi23004MemoryProfile.NavigationTileYOffset));

            return double.IsFinite(tileX) && double.IsFinite(tileY);
        }
        catch
        {
            return false;
        }
    }

    private static string? TryReadMapName(
        ReadOnlyProcessMemory memory,
        out bool mapLoaded)
    {
        mapLoaded = false;

        try
        {
            var mapAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.MapPointerRva));
            if (mapAddress <= 0x10000u)
            {
                return null;
            }

            var mapPointer = ReadOnlyProcessMemory.PointerFromUInt32(mapAddress);

            try
            {
                mapLoaded = memory.ReadByte(nint.Add(
                    mapPointer,
                    Omsi23004MemoryProfile.MapLoadedOffset)) != 0;
            }
            catch
            {
                mapLoaded = false;
            }

            var mapName = memory.ReadNullTerminatedUnicodeStringField(
                nint.Add(mapPointer, Omsi23004MemoryProfile.MapNameOffset),
                maxCharacters: 128);

            if (!string.IsNullOrWhiteSpace(mapName))
            {
                mapLoaded = true;
                return mapName;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static double QuaternionToHeadingDegrees(MemoryQuaternion q)
    {
        var sinYaw = 2d * (q.W * q.Y + q.X * q.Z);
        var cosYaw = 1d - 2d * (q.Y * q.Y + q.Z * q.Z);
        var degrees = Math.Atan2(sinYaw, cosYaw) * (180d / Math.PI);
        return (degrees + 360d) % 360d;
    }

    public void Dispose()
    {
        DisposeMemory();
        GC.SuppressFinalize(this);
    }

    private void DisposeMemory()
    {
        _memory?.Dispose();
        _memory = null;
        _processInfo = null;
    }
}
