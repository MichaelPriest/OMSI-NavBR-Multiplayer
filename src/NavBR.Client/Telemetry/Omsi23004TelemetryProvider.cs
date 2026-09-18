using System.Diagnostics;
using System.Numerics;
using NavBR.Client.Omsi;
using NavBR.Client.Multiplayer;
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

    internal IReadOnlyList<RoleplayCharacterOption> ReadRoleplayCharacterOptions()
    {
        var memory = _memory;
        var processInfo = _processInfo;
        if (memory is null ||
            processInfo is null ||
            !processInfo.IsOmsi23004Exact)
        {
            return Array.Empty<RoleplayCharacterOption>();
        }

        try
        {
            var mapAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.MapPointerRva));
            if (mapAddress <= 0x10000u)
            {
                return Array.Empty<RoleplayCharacterOption>();
            }

            var mapPointer = ReadOnlyProcessMemory.PointerFromUInt32(mapAddress);
            if (memory.ReadByte(nint.Add(
                    mapPointer,
                    Omsi23004MemoryProfile.MapLoadedOffset)) == 0)
            {
                return Array.Empty<RoleplayCharacterOption>();
            }

            var listAddress = memory.ReadUInt32(nint.Add(
                mapPointer,
                Omsi23004MemoryProfile.MapDriversListOffset));
            if (listAddress <= 0x10000u)
            {
                return Array.Empty<RoleplayCharacterOption>();
            }

            var listPointer = ReadOnlyProcessMemory.PointerFromUInt32(listAddress);
            var count = memory.ReadInt32(nint.Add(
                listPointer,
                Omsi23004MemoryProfile.StringListCountOffset));
            var itemsAddress = memory.ReadUInt32(nint.Add(
                listPointer,
                Omsi23004MemoryProfile.StringListItemsArrayOffset));

            if (count is <= 0 or > 512 || itemsAddress <= 0x10000u)
            {
                return Array.Empty<RoleplayCharacterOption>();
            }

            var itemsPointer = ReadOnlyProcessMemory.PointerFromUInt32(itemsAddress);
            var result = new List<RoleplayCharacterOption>(Math.Min(count, 128));
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeDriverDefinitionPointer =
                TryReadActiveRoleplayDriverDefinitionPointer(memory);

            for (var index = 0; index < count && result.Count < 128; index++)
            {
                var itemPointer = nint.Add(
                    itemsPointer,
                    index * Omsi23004MemoryProfile.StringItemSize);
                var source = memory.ReadDelphiUnicodeStringField(
                                 nint.Add(itemPointer, Omsi23004MemoryProfile.StringItemTextOffset),
                                 maxCharacters: 512)
                             ?? memory.ReadDelphiAnsiStringField(
                                 nint.Add(itemPointer, Omsi23004MemoryProfile.StringItemTextOffset),
                                 maxCharacters: 512);
                var definitionPointer = memory.ReadUInt32(nint.Add(
                    itemPointer,
                    Omsi23004MemoryProfile.StringItemObjectOffset));

                source = source?.Trim();
                if (string.IsNullOrWhiteSpace(source) || definitionPointer <= 0x10000u)
                {
                    continue;
                }

                var display = BuildRoleplayCharacterDisplayName(source);
                var idBase = source.Replace('\\', '/').Trim().ToLowerInvariant();
                var id = idBase;
                var suffix = 2;
                while (!usedIds.Add(id))
                {
                    id = $"{idBase}#{suffix++}";
                }

                result.Add(new RoleplayCharacterOption(
                    id,
                    display,
                    source,
                    unchecked((int)definitionPointer),
                    IsActiveDriver:
                        activeDriverDefinitionPointer == unchecked((int)definitionPointer)));
            }

            return result;
        }
        catch
        {
            return Array.Empty<RoleplayCharacterOption>();
        }
    }

    private static int? TryReadActiveRoleplayDriverDefinitionPointer(
        ReadOnlyProcessMemory memory)
    {
        try
        {
            var playerVehicleIndex = memory.ReadInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.PlayerVehicleIndexRva));
            if (playerVehicleIndex < 0 || playerVehicleIndex > 10000)
            {
                return null;
            }

            var vehicleAddress = ResolvePlayerVehicleAddress(memory, playerVehicleIndex);
            if (vehicleAddress == nint.Zero)
            {
                return null;
            }

            var humansAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.HumansArrayRva));
            if (humansAddress <= 0x10000u)
            {
                return null;
            }

            var humansPointer = ReadOnlyProcessMemory.PointerFromUInt32(humansAddress);
            var count = memory.ReadInt32(nint.Subtract(humansPointer, sizeof(int)));
            if (count is <= 0 or > 8192)
            {
                return null;
            }

            var vehiclePointer = unchecked((uint)vehicleAddress.ToInt64());
            for (var index = 0; index < count; index++)
            {
                var humanAddress = memory.ReadUInt32(nint.Add(
                    humansPointer,
                    checked(index * sizeof(int))));
                if (humanAddress <= 0x10000u)
                {
                    continue;
                }

                var humanPointer = ReadOnlyProcessMemory.PointerFromUInt32(humanAddress);
                var myBus = memory.ReadUInt32(nint.Add(
                    humanPointer,
                    Omsi23004MemoryProfile.HumanMyBusOffset));
                if (myBus != vehiclePointer)
                {
                    continue;
                }

                var definition = memory.ReadUInt32(nint.Add(
                    humanPointer,
                    Omsi23004MemoryProfile.HumanDefinitionOffset));
                return definition > 0x10000u
                    ? unchecked((int)definition)
                    : null;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string BuildRoleplayCharacterDisplayName(string source)
    {
        try
        {
            var normalized = source.Replace('\\', '/').TrimEnd('/');
            var fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
            var display = Path.GetFileNameWithoutExtension(fileName);
            if (!string.IsNullOrWhiteSpace(display))
            {
                return display.Replace('_', ' ').Trim();
            }
        }
        catch
        {
        }

        return source;
    }

    internal OmsiCameraProjectionSnapshot? ReadCameraProjection()
    {
        var memory = _memory;
        var processInfo = _processInfo;
        if (memory is null ||
            processInfo is null ||
            !processInfo.IsOmsi23004Exact ||
            Omsi23004MemoryProfile.CameraPointerRva == 0)
        {
            return null;
        }

        try
        {
            if (Process.GetProcessById(memory.ProcessId).HasExited)
            {
                return null;
            }

            var cameraAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.CameraPointerRva));
            if (cameraAddress <= 0x10000u)
            {
                return null;
            }

            var cameraPointer = ReadOnlyProcessMemory.PointerFromUInt32(cameraAddress);
            var view = memory.ReadMatrix4x4(nint.Add(
                cameraPointer,
                Omsi23004MemoryProfile.CameraViewMatrixOffset));
            var projection = memory.ReadMatrix4x4(nint.Add(
                cameraPointer,
                Omsi23004MemoryProfile.CameraProjectionMatrixOffset));

            return IsFinite(view) && IsFinite(projection)
                ? new OmsiCameraProjectionSnapshot(view, projection, DateTimeOffset.UtcNow)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsFinite(Matrix4x4 matrix) =>
        float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) &&
        float.IsFinite(matrix.M13) && float.IsFinite(matrix.M14) &&
        float.IsFinite(matrix.M21) && float.IsFinite(matrix.M22) &&
        float.IsFinite(matrix.M23) && float.IsFinite(matrix.M24) &&
        float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32) &&
        float.IsFinite(matrix.M33) && float.IsFinite(matrix.M34) &&
        float.IsFinite(matrix.M41) && float.IsFinite(matrix.M42) &&
        float.IsFinite(matrix.M43) && float.IsFinite(matrix.M44);

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

            // OMSI maintains three useful motion values here. Tacho is the
            // speedometer/script-facing speed and follows the same km/h unit as
            // the built-in Velocity variable used by bus scripts. Groundspeed
            // and the physical velocity vector stay as independent fallbacks.
            var tachoKph = Math.Abs(memory.ReadSingle(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleTachoOffset)));

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

            var speedKph = ResolveVehicleSpeedKph(
                tachoKph,
                linearSpeedMps * 3.6d,
                groundSpeedMps * 3.6d);

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
                SpeedKph: speedKph,
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

    private static double ResolveVehicleSpeedKph(
        double tachoKph,
        double linearSpeedKph,
        double groundSpeedKph)
    {
        const double maximumPlausibleKph = 220d;
        const double movingThresholdKph = 0.5d;

        var tachoValid = double.IsFinite(tachoKph) &&
                         tachoKph >= 0d &&
                         tachoKph <= maximumPlausibleKph;
        var linearValid = double.IsFinite(linearSpeedKph) &&
                          linearSpeedKph >= 0d &&
                          linearSpeedKph <= maximumPlausibleKph;
        var groundValid = double.IsFinite(groundSpeedKph) &&
                          groundSpeedKph >= 0d &&
                          groundSpeedKph <= maximumPlausibleKph;

        // Prefer OMSI's speedometer value. This is closest to what the driver
        // sees in the bus and avoids unit/axis differences between vehicles.
        if (tachoValid && tachoKph >= movingThresholdKph)
        {
            return tachoKph;
        }

        // Some buses can leave Tacho at zero during initialization. In that
        // case, use the physics values only while they clearly indicate motion.
        if (groundValid && groundSpeedKph >= movingThresholdKph)
        {
            return groundSpeedKph;
        }

        if (linearValid && linearSpeedKph >= movingThresholdKph)
        {
            return linearSpeedKph;
        }

        // When all sources agree that the vehicle is effectively stopped, keep
        // a clean zero instead of exposing floating-point jitter in HUD/hardware.
        if (tachoValid || groundValid || linearValid)
        {
            return 0d;
        }

        return 0d;
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
