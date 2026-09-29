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
    private const long VehicleIdentityRefreshMs = 5_000;
    private const long VehicleTileRefreshMs = 2_000;
    private const long MapNameRefreshMs = 5_000;
    private const long NextStopRefreshMs = 750;
    private const long TripTextRefreshMs = 5_000;

    private ReadOnlyProcessMemory? _memory;
    private OmsiProcessInfo? _processInfo;
    private nint _cachedIdentityVehicleAddress;
    private uint _cachedIdentityFileObjectAddress;
    private uint _cachedIdentityDefinitionAddress;
    private long _cachedIdentityTickMs;
    private OmsiVehicleIdentity? _cachedVehicleIdentity;
    private uint _cachedTileMapAddress;
    private uint _cachedVehicleTilePointer;
    private int? _cachedMapTileIndex;
    private int _cachedTileGridX;
    private int _cachedTileGridY;
    private bool _cachedTileHasGrid;
    private long _cachedTileTickMs;
    private bool _tileCacheValid;
    private uint _cachedMapNameAddress;
    private long _cachedMapNameTickMs;
    private string? _cachedMapName;
    private nint _cachedTripVehicleAddress;
    private uint _cachedTripManagerAddress;
    private int _cachedTripIndex = -1;
    private long _cachedTripTextTickMs;
    private string? _cachedTripLine;
    private string? _cachedTripRoute;
    private string? _cachedTripDestination;
    private uint _cachedNextStopStringPointer;
    private long _cachedNextStopTickMs;
    private string? _cachedNextStopName;

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

            var activeDriverDefinitionPointer =
                TryReadActiveRoleplayDriverDefinitionPointer(memory);

            var listAddress = memory.ReadUInt32(nint.Add(
                mapPointer,
                Omsi23004MemoryProfile.MapDriversListOffset));
            if (listAddress <= 0x10000u)
            {
                return BuildActiveRoleplayDriverFallback(
                    activeDriverDefinitionPointer);
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
                return BuildActiveRoleplayDriverFallback(
                    activeDriverDefinitionPointer);
            }

            var itemsPointer = ReadOnlyProcessMemory.PointerFromUInt32(itemsAddress);
            var result = new List<RoleplayCharacterOption>(Math.Min(count + 1, 129));
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            if (activeDriverDefinitionPointer is int activePointer &&
                activePointer > 0 &&
                !result.Any(option => option.IsActiveDriver))
            {
                var fallback = BuildActiveRoleplayDriverFallback(activePointer);
                if (fallback.Count > 0)
                {
                    result.Insert(0, fallback[0]);
                }
            }

            return result;
        }
        catch
        {
            return Array.Empty<RoleplayCharacterOption>();
        }
    }

    private static IReadOnlyList<RoleplayCharacterOption> BuildActiveRoleplayDriverFallback(
        int? definitionPointer)
    {
        if (definitionPointer is not int pointer || pointer <= 0)
        {
            return Array.Empty<RoleplayCharacterOption>();
        }

        return
        [
            new RoleplayCharacterOption(
                $"active-driver:{unchecked((uint)pointer):x8}",
                "Motorista atual",
                "OMSI active driver",
                pointer,
                IsActiveDriver: true)
        ];
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
                if (definition <= 0x10000u)
                {
                    continue;
                }

                // OMSI public reverse-engineering references define
                // AIModeEx value 9 as THAME_DrivingBus. Prefer that live state
                // so passengers attached to the same vehicle are not mistaken
                // for the RP driver.
                var aiModeEx = memory.ReadByte(nint.Add(
                    humanPointer,
                    0x6C5));
                var fixDriver = memory.ReadByte(nint.Add(
                    humanPointer,
                    0x662));

                if (aiModeEx == 9 || fixDriver != 0)
                {
                    return unchecked((int)definition);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
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
            if (!memory.IsProcessAlive)
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
            if (!memory.IsProcessAlive)
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
            // native pose used by physical multiplayer. RoadVehicle.Position is
            // local to RoadVehicle.Kachel, so the Kachel pointer must remain
            // stable across the pose read. Otherwise one active-frame tile
            // transition can combine an old local position with a new grid and
            // move a remote bus by an entire tile.
            var physicalPoseCoherent =
                TryReadVehicleKachelPointer(
                    memory,
                    vehicleAddress,
                    out var physicalPoseTilePointer);

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

            if (physicalPoseCoherent)
            {
                if (!TryReadVehicleKachelPointer(
                        memory,
                        vehicleAddress,
                        out var tilePointerAfterPose))
                {
                    physicalPoseCoherent = false;
                }
                else if (tilePointerAfterPose != physicalPoseTilePointer)
                {
                    // The player crossed a tile while we were reading the pose.
                    // Retry once against the new Kachel; if it changes again,
                    // publish normal world/navigation telemetry but withhold the
                    // physical-local pose for this frame.
                    physicalPoseTilePointer = tilePointerAfterPose;
                    localPosition = memory.ReadVector3(nint.Add(
                        vehicleAddress,
                        Omsi23004MemoryProfile.VehiclePositionOffset));
                    absolutePosition = memory.ReadVector3(nint.Add(
                        vehicleAddress,
                        Omsi23004MemoryProfile.VehicleAbsPositionOffset +
                        Omsi23004MemoryProfile.MatrixTranslationOffset));
                    rotation = memory.ReadQuaternion(nint.Add(
                        vehicleAddress,
                        Omsi23004MemoryProfile.VehicleRotationOffset));

                    physicalPoseCoherent =
                        TryReadVehicleKachelPointer(
                            memory,
                            vehicleAddress,
                            out var tilePointerAfterRetry) &&
                        tilePointerAfterRetry == physicalPoseTilePointer;
                }
            }

            // OMSI maintains three useful motion values here. Tacho is the
            // speedometer/script-facing speed and follows the same km/h unit as
            // the built-in Velocity variable used by bus scripts. Groundspeed
            // and the physical velocity vector stay as independent fallbacks.
            Span<float> speedScalars = stackalloc float[2];
            memory.ReadSingles(
                nint.Add(
                    vehicleAddress,
                    Omsi23004MemoryProfile.VehicleTachoOffset),
                speedScalars);
            var tachoKph = Math.Abs(speedScalars[0]);
            var groundSpeedMps = Math.Abs(speedScalars[1]);

            var velocity = memory.ReadVector3(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleVelocityOffset));

            var linearSpeedMps = Math.Sqrt(
                velocity.X * velocity.X +
                velocity.Y * velocity.Y +
                velocity.Z * velocity.Z);

            var speedKph = ResolveVehicleSpeedKph(
                tachoKph,
                linearSpeedMps * 3.6d,
                groundSpeedMps * 3.6d);

            var mapName = TryReadMapNameCached(memory, out var mapLoaded);
            var heading = QuaternionToHeadingDegrees(rotation);
            var vehicleIdentity = ReadVehicleIdentityCached(
                memory,
                vehicleAddress);

            // These fields already exist in the supported OMSI profile but were
            // previously left at VehicleTelemetry defaults. Read them best-effort
            // so remote physical buses receive real control/visual state instead
            // of permanent zeros.
            TryReadPedalPercents(
                memory,
                vehicleAddress,
                out var throttlePercent,
                out var brakePercent);
            var fuelPercent = TryReadPercent(
                memory,
                nint.Add(vehicleAddress, Omsi23004MemoryProfile.VehicleFuelPercentOffset));
            var visualState = TryReadVehicleVisualState(memory, vehicleAddress);
            var hasPhysicalGrid =
                physicalPoseCoherent &&
                TryReadVehicleTileStateCached(
                    memory,
                    vehicleAddress,
                    physicalPoseTilePointer,
                    out var mapTileIndex,
                    out var vehicleGridX,
                    out var vehicleGridY);
            if (!physicalPoseCoherent)
            {
                mapTileIndex = null;
                vehicleGridX = 0;
                vehicleGridY = 0;
            }

            int? gridX = null;
            int? gridY = null;
            int? physicalGridX = null;
            int? physicalGridY = null;
            double? tileX = null;
            double? tileY = null;

            // NavigationVehicle + Map.CurrentGrid forms one coherent fallback
            // coordinate pair. Never combine its TileX/TileY with another
            // grid source: doing that can shift the vehicle by whole Kacheln
            // and produces false multi-kilometre off-route readings.
            if (TryReadNavigationPosition(
                    memory,
                    out var navigationGridX,
                    out var navigationGridY,
                    out var navigationTileX,
                    out var navigationTileY))
            {
                gridX = navigationGridX;
                gridY = navigationGridY;
                tileX = navigationTileX;
                tileY = navigationTileY;
            }

            // The player's RoadVehicle owns both Kachel and Position. The
            // physical tile/grid state above is cached by the live Map pointer
            // plus RoadVehicle.Kachel, so the expensive Kacheln/KachelInfos
            // scans only run when the player actually changes tile or map.
            if (hasPhysicalGrid &&
                float.IsFinite(localPosition.X) &&
                float.IsFinite(localPosition.Z) &&
                Math.Abs(localPosition.X) <= 1_200f &&
                Math.Abs(localPosition.Z) <= 1_200f)
            {
                gridX = vehicleGridX;
                gridY = vehicleGridY;
                physicalGridX = vehicleGridX;
                physicalGridY = vehicleGridY;
                // OMSI/D3D Position is X,Y,Z with Y vertical. Navigation
                // TileX/TileY is the ground plane, therefore use X/Z.
                tileX = localPosition.X;
                tileY = localPosition.Z;
            }

            string? line = null;
            string? route = null;
            string? nextStopName = null;
            string? destinationName = null;
            TryReadActiveTripCached(
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
                FuelPercent: fuelPercent,
                ThrottlePercent: throttlePercent,
                BrakePercent: brakePercent,
                Lights: visualState.Lights,
                TurnSignal: visualState.TurnSignal,
                VehicleCompatibilityId: vehicleIdentity.CompatibilityId,
                LocalX: physicalPoseCoherent ? localPosition.X : null,
                LocalY: physicalPoseCoherent ? localPosition.Y : null,
                LocalZ: physicalPoseCoherent ? localPosition.Z : null,
                RotationX: physicalPoseCoherent ? rotation.X : null,
                RotationY: physicalPoseCoherent ? rotation.Y : null,
                RotationZ: physicalPoseCoherent ? rotation.Z : null,
                RotationW: physicalPoseCoherent ? rotation.W : null,
                MapTileIndex: mapTileIndex,
                PhysicalGridX: physicalGridX,
                PhysicalGridY: physicalGridY);
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

    private OmsiVehicleIdentity ReadVehicleIdentityCached(
        ReadOnlyProcessMemory memory,
        nint vehicleAddress)
    {
        uint fileObjectAddress = 0;
        uint definitionAddress = 0;
        try
        {
            fileObjectAddress = memory.ReadUInt32(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleFileObjectOffset));
            definitionAddress = memory.ReadUInt32(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.RoadVehicleDefinitionOffset));
        }
        catch
        {
            // The full reader is best-effort and remains the fallback below.
        }

        var now = Environment.TickCount64;
        var stableIdentityPointers =
            fileObjectAddress > 0x10000u ||
            definitionAddress > 0x10000u;
        if (stableIdentityPointers &&
            _cachedVehicleIdentity is not null &&
            _cachedIdentityVehicleAddress == vehicleAddress &&
            _cachedIdentityFileObjectAddress == fileObjectAddress &&
            _cachedIdentityDefinitionAddress == definitionAddress &&
            _cachedIdentityTickMs > 0 &&
            now >= _cachedIdentityTickMs &&
            now - _cachedIdentityTickMs < VehicleIdentityRefreshMs)
        {
            return _cachedVehicleIdentity;
        }

        var identity = OmsiVehicleIdentityReader.Read(
            memory,
            _processInfo,
            vehicleAddress);

        if (stableIdentityPointers)
        {
            _cachedIdentityVehicleAddress = vehicleAddress;
            _cachedIdentityFileObjectAddress = fileObjectAddress;
            _cachedIdentityDefinitionAddress = definitionAddress;
            _cachedIdentityTickMs = now;
            _cachedVehicleIdentity = identity;
        }
        else
        {
            ClearVehicleIdentityCache();
        }

        return identity;
    }

    private void ClearVehicleIdentityCache()
    {
        _cachedIdentityVehicleAddress = nint.Zero;
        _cachedIdentityFileObjectAddress = 0;
        _cachedIdentityDefinitionAddress = 0;
        _cachedIdentityTickMs = 0;
        _cachedVehicleIdentity = null;
    }

    private bool TryReadVehicleTileStateCached(
        ReadOnlyProcessMemory memory,
        nint vehicleAddress,
        uint expectedVehicleTilePointer,
        out int? mapTileIndex,
        out int gridX,
        out int gridY)
    {
        mapTileIndex = null;
        gridX = 0;
        gridY = 0;

        uint vehicleTilePointer;
        uint mapAddress;
        try
        {
            vehicleTilePointer = memory.ReadUInt32(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleKachelOffset));
            mapAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.MapPointerRva));
        }
        catch
        {
            ClearVehicleTileCache();
            return false;
        }

        if (vehicleTilePointer <= 0x10000u ||
            mapAddress <= 0x10000u)
        {
            ClearVehicleTileCache();
            return false;
        }

        // Position/Rotation were read against expectedVehicleTilePointer.
        // Reject the physical pair if OMSI crossed Kachel again before the
        // grid lookup; navigation/world telemetry can still be published.
        if (expectedVehicleTilePointer <= 0x10000u ||
            vehicleTilePointer != expectedVehicleTilePointer)
        {
            return false;
        }

        var now = Environment.TickCount64;
        if (_tileCacheValid &&
            _cachedVehicleTilePointer == vehicleTilePointer &&
            _cachedTileMapAddress == mapAddress &&
            _cachedTileTickMs > 0 &&
            now >= _cachedTileTickMs &&
            now - _cachedTileTickMs < VehicleTileRefreshMs)
        {
            mapTileIndex = _cachedMapTileIndex;
            gridX = _cachedTileGridX;
            gridY = _cachedTileGridY;
            return _cachedTileHasGrid;
        }

        mapTileIndex = TryReadMapTileIndex(memory, vehicleAddress);
        var hasGrid = false;
        if (mapTileIndex is int exactTileIndex &&
            TryReadMapTileGrid(
                memory,
                exactTileIndex,
                out gridX,
                out gridY))
        {
            hasGrid = true;
        }
        else if (TryReadVehicleTileGrid(
                     memory,
                     vehicleAddress,
                     out gridX,
                     out gridY))
        {
            hasGrid = true;
        }

        _cachedTileMapAddress = mapAddress;
        _cachedVehicleTilePointer = vehicleTilePointer;
        _cachedMapTileIndex = mapTileIndex;
        _cachedTileGridX = gridX;
        _cachedTileGridY = gridY;
        _cachedTileHasGrid = hasGrid;
        _cachedTileTickMs = now;
        _tileCacheValid = true;
        return hasGrid;
    }

    private void ClearVehicleTileCache()
    {
        _cachedTileMapAddress = 0;
        _cachedVehicleTilePointer = 0;
        _cachedMapTileIndex = null;
        _cachedTileGridX = 0;
        _cachedTileGridY = 0;
        _cachedTileHasGrid = false;
        _cachedTileTickMs = 0;
        _tileCacheValid = false;
    }

    private static bool TryReadVehicleKachelPointer(
        ReadOnlyProcessMemory memory,
        nint vehicleAddress,
        out uint tilePointer)
    {
        tilePointer = 0;
        try
        {
            tilePointer = memory.ReadUInt32(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleKachelOffset));
            return tilePointer > 0x10000u;
        }
        catch
        {
            tilePointer = 0;
            return false;
        }
    }

    private static bool TryReadVehicleTileGrid(
        ReadOnlyProcessMemory memory,
        nint vehicleAddress,
        out int gridX,
        out int gridY)
    {
        gridX = 0;
        gridY = 0;

        try
        {
            var vehicleTilePointer = memory.ReadUInt32(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleKachelOffset));
            if (vehicleTilePointer <= 0x10000u)
            {
                return false;
            }

            var mapAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.MapPointerRva));
            if (mapAddress <= 0x10000u)
            {
                return false;
            }

            var mapPointer = ReadOnlyProcessMemory.PointerFromUInt32(mapAddress);
            var infosAddress = memory.ReadUInt32(nint.Add(
                mapPointer,
                Omsi23004MemoryProfile.MapKachelInfosOffset));
            if (infosAddress <= 0x10000u)
            {
                return false;
            }

            var infosPointer = ReadOnlyProcessMemory.PointerFromUInt32(infosAddress);
            var infoCount = memory.ReadInt32(nint.Subtract(infosPointer, sizeof(int)));
            if (infoCount <= 0 || infoCount > 200_000)
            {
                return false;
            }

            for (var index = 0; index < infoCount; index++)
            {
                var infoPointer = nint.Add(
                    infosPointer,
                    checked(index * Omsi23004MemoryProfile.MapKachelInfoSize));
                var candidate = memory.ReadUInt32(nint.Add(
                    infoPointer,
                    Omsi23004MemoryProfile.MapKachelInfoTilePointerOffset));
                if (candidate != vehicleTilePointer)
                {
                    continue;
                }

                var x = memory.ReadInt32(nint.Add(
                    infoPointer,
                    Omsi23004MemoryProfile.MapKachelInfoGridXOffset));
                var y = memory.ReadInt32(nint.Add(
                    infoPointer,
                    Omsi23004MemoryProfile.MapKachelInfoGridYOffset));
                if (Math.Abs((long)x) > 100_000L ||
                    Math.Abs((long)y) > 100_000L)
                {
                    return false;
                }

                gridX = x;
                gridY = y;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadMapTileGrid(
        ReadOnlyProcessMemory memory,
        int mapTileIndex,
        out int gridX,
        out int gridY)
    {
        gridX = 0;
        gridY = 0;

        if (!TryReadMapTilePointer(
                memory,
                mapTileIndex,
                out var tilePointer))
        {
            return false;
        }

        try
        {
            var mapAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.MapPointerRva));
            if (mapAddress <= 0x10000u)
            {
                return false;
            }

            var mapPointer = ReadOnlyProcessMemory.PointerFromUInt32(mapAddress);
            var infosAddress = memory.ReadUInt32(nint.Add(
                mapPointer,
                Omsi23004MemoryProfile.MapKachelInfosOffset));
            if (infosAddress <= 0x10000u)
            {
                return false;
            }

            var infosPointer = ReadOnlyProcessMemory.PointerFromUInt32(infosAddress);
            var infoCount = memory.ReadInt32(nint.Subtract(infosPointer, sizeof(int)));
            if (infoCount <= 0 || infoCount > 200_000)
            {
                return false;
            }

            for (var index = 0; index < infoCount; index++)
            {
                var infoPointer = nint.Add(
                    infosPointer,
                    checked(index * Omsi23004MemoryProfile.MapKachelInfoSize));
                var candidate = memory.ReadUInt32(nint.Add(
                    infoPointer,
                    Omsi23004MemoryProfile.MapKachelInfoTilePointerOffset));
                if (candidate != tilePointer)
                {
                    continue;
                }

                var x = memory.ReadInt32(nint.Add(
                    infoPointer,
                    Omsi23004MemoryProfile.MapKachelInfoGridXOffset));
                var y = memory.ReadInt32(nint.Add(
                    infoPointer,
                    Omsi23004MemoryProfile.MapKachelInfoGridYOffset));
                if (Math.Abs((long)x) > 100_000L ||
                    Math.Abs((long)y) > 100_000L)
                {
                    return false;
                }

                gridX = x;
                gridY = y;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadMapTilePointer(
        ReadOnlyProcessMemory memory,
        int mapTileIndex,
        out uint tilePointer)
    {
        tilePointer = 0;
        if (mapTileIndex < 0 || mapTileIndex > 200_000)
        {
            return false;
        }

        try
        {
            var mapAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.MapPointerRva));
            if (mapAddress <= 0x10000u)
            {
                return false;
            }

            var mapPointer = ReadOnlyProcessMemory.PointerFromUInt32(mapAddress);
            if (memory.ReadByte(nint.Add(
                    mapPointer,
                    Omsi23004MemoryProfile.MapLoadedOffset)) == 0)
            {
                return false;
            }

            var tilesAddress = memory.ReadUInt32(nint.Add(
                mapPointer,
                Omsi23004MemoryProfile.MapKachelnOffset));
            if (tilesAddress <= 0x10000u)
            {
                return false;
            }

            var tilesPointer = ReadOnlyProcessMemory.PointerFromUInt32(tilesAddress);
            var count = memory.ReadInt32(nint.Subtract(tilesPointer, sizeof(int)));
            if (count <= 0 || count > 200_000 || mapTileIndex >= count)
            {
                return false;
            }

            var value = memory.ReadUInt32(nint.Add(
                tilesPointer,
                checked(mapTileIndex * sizeof(int))));
            if (value <= 0x10000u)
            {
                return false;
            }

            tilePointer = value;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int? TryReadMapTileIndex(
        ReadOnlyProcessMemory memory,
        nint vehicleAddress)
    {
        try
        {
            var vehicleTilePointer = memory.ReadUInt32(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleKachelOffset));
            if (vehicleTilePointer <= 0x10000u)
            {
                return null;
            }

            var mapAddress = memory.ReadUInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.MapPointerRva));
            if (mapAddress <= 0x10000u)
            {
                return null;
            }

            var mapPointer = ReadOnlyProcessMemory.PointerFromUInt32(mapAddress);
            var tilesAddress = memory.ReadUInt32(nint.Add(
                mapPointer,
                Omsi23004MemoryProfile.MapKachelnOffset));
            if (tilesAddress <= 0x10000u)
            {
                return null;
            }

            var tilesPointer = ReadOnlyProcessMemory.PointerFromUInt32(tilesAddress);
            var count = memory.ReadInt32(nint.Subtract(tilesPointer, sizeof(int)));
            if (count <= 0 || count > 200_000)
            {
                return null;
            }

            for (var index = 0; index < count; index++)
            {
                var candidate = memory.ReadUInt32(nint.Add(
                    tilesPointer,
                    checked(index * sizeof(int))));
                if (candidate == vehicleTilePointer)
                {
                    return index;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static void TryReadPedalPercents(
        ReadOnlyProcessMemory memory,
        nint vehicleAddress,
        out double? throttlePercent,
        out double? brakePercent)
    {
        try
        {
            Span<float> values = stackalloc float[2];
            memory.ReadSingles(
                nint.Add(
                    vehicleAddress,
                    Omsi23004MemoryProfile.VehicleThrottleOffset),
                values);
            throttlePercent = NormalizePercent(values[0]);
            brakePercent = NormalizePercent(values[1]);
            return;
        }
        catch
        {
            // Keep per-field best-effort behavior for unusual addons.
        }

        throttlePercent = TryReadPercent(
            memory,
            nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleThrottleOffset));
        brakePercent = TryReadPercent(
            memory,
            nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleBrakePedalOffset));
    }

    private static double? TryReadPercent(
        ReadOnlyProcessMemory memory,
        nint address)
    {
        try
        {
            return NormalizePercent(memory.ReadSingle(address));
        }
        catch
        {
            return null;
        }
    }

    private static double? NormalizePercent(float rawValue)
    {
        var value = (double)rawValue;
        if (!double.IsFinite(value) || value < -0.05d)
        {
            return null;
        }

        // OMSI control/runtime fields are commonly normalized to 0..1,
        // while a few add-ons expose an already-percent-like value. Accept
        // both shapes without letting malformed memory enter telemetry.
        if (value <= 1.05d)
        {
            return Math.Clamp(value, 0d, 1d) * 100d;
        }

        if (value <= 100d)
        {
            return Math.Clamp(value, 0d, 100d);
        }

        return null;
    }

    private static VehicleVisualTelemetry TryReadVehicleVisualState(
        ReadOnlyProcessMemory memory,
        nint vehicleAddress)
    {
        try
        {
            Span<float> values = stackalloc float[5];
            memory.ReadSingles(
                nint.Add(
                    vehicleAddress,
                    Omsi23004MemoryProfile.VehicleAiLightOffset),
                values);
            return BuildVehicleVisualTelemetry(
                IsVisualFlag(values[0]),
                IsVisualFlag(values[1]),
                IsVisualFlag(values[2]),
                IsVisualFlag(values[3]),
                IsVisualFlag(values[4]));
        }
        catch
        {
            // Fall back to the older per-field reads if an addon exposes an
            // unexpected boundary inside this otherwise contiguous OMSI block.
            try
            {
                return BuildVehicleVisualTelemetry(
                    ReadVisualFlag(
                        memory,
                        nint.Add(
                            vehicleAddress,
                            Omsi23004MemoryProfile.VehicleAiLightOffset)),
                    ReadVisualFlag(
                        memory,
                        nint.Add(
                            vehicleAddress,
                            Omsi23004MemoryProfile.VehicleAiInteriorLightOffset)),
                    ReadVisualFlag(
                        memory,
                        nint.Add(
                            vehicleAddress,
                            Omsi23004MemoryProfile.VehicleAiBlinkerLeftOffset)),
                    ReadVisualFlag(
                        memory,
                        nint.Add(
                            vehicleAddress,
                            Omsi23004MemoryProfile.VehicleAiBlinkerRightOffset)),
                    ReadVisualFlag(
                        memory,
                        nint.Add(
                            vehicleAddress,
                            Omsi23004MemoryProfile.VehicleAiBrakeLightOffset)));
            }
            catch
            {
                return default;
            }
        }
    }

    private static VehicleVisualTelemetry BuildVehicleVisualTelemetry(
        bool external,
        bool interior,
        bool left,
        bool right,
        bool brake)
    {
        var lights = VehicleLightFlags.None;
        if (external)
        {
            lights |= VehicleLightFlags.Position;
        }

        if (interior)
        {
            lights |= VehicleLightFlags.Interior;
        }

        if (brake)
        {
            lights |= VehicleLightFlags.Brake;
        }

        TurnSignalState turnSignal;
        if (left && right)
        {
            lights |= VehicleLightFlags.Hazard;
            turnSignal = TurnSignalState.Hazard;
        }
        else if (left)
        {
            turnSignal = TurnSignalState.Left;
        }
        else if (right)
        {
            turnSignal = TurnSignalState.Right;
        }
        else
        {
            turnSignal = TurnSignalState.Off;
        }

        return new VehicleVisualTelemetry(lights, turnSignal);
    }

    private static bool IsVisualFlag(float value) =>
        float.IsFinite(value) && value > 0.5f;

    private static bool ReadVisualFlag(
        ReadOnlyProcessMemory memory,
        nint address)
    {
        var value = memory.ReadSingle(address);
        return float.IsFinite(value) && value > 0.5f;
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

    private bool TryReadActiveTripCached(
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
                ClearTripCache();
                return false;
            }

            var now = Environment.TickCount64;
            var nextStopFieldAddress = nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleScheduleNextStopNameOffset);
            uint nextStopStringPointer = 0;
            try
            {
                nextStopStringPointer =
                    memory.ReadUInt32(nextStopFieldAddress);
            }
            catch
            {
            }

            if (_cachedTripVehicleAddress == vehicleAddress &&
                _cachedNextStopTickMs > 0 &&
                now >= _cachedNextStopTickMs &&
                now - _cachedNextStopTickMs < NextStopRefreshMs &&
                _cachedNextStopStringPointer == nextStopStringPointer)
            {
                nextStopName = _cachedNextStopName;
            }
            else
            {
                nextStopName =
                    memory.ReadNullTerminatedUnicodeStringField(
                        nextStopFieldAddress,
                        maxCharacters: 128)
                    ?? memory.ReadNullTerminatedAnsiStringField(
                        nextStopFieldAddress,
                        maxCharacters: 128);
                _cachedNextStopStringPointer = nextStopStringPointer;
                _cachedNextStopTickMs = now;
                _cachedNextStopName = nextStopName;
            }

            var tripIndex = memory.ReadInt32(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleScheduleTripIndexOffset));
            if (tripIndex < 0 || tripIndex > 100000)
            {
                return !string.IsNullOrWhiteSpace(nextStopName);
            }

            var timeTableAddress = memory.ReadUInt32(
                memory.AddressFromRva(
                    Omsi23004MemoryProfile.TimeTableManagerRva));
            if (timeTableAddress <= 0x10000u)
            {
                return !string.IsNullOrWhiteSpace(nextStopName);
            }

            var sameTrip =
                _cachedTripVehicleAddress == vehicleAddress &&
                _cachedTripManagerAddress == timeTableAddress &&
                _cachedTripIndex == tripIndex &&
                _cachedTripTextTickMs > 0 &&
                now >= _cachedTripTextTickMs &&
                now - _cachedTripTextTickMs < TripTextRefreshMs;
            if (sameTrip)
            {
                line = _cachedTripLine;
                route = _cachedTripRoute;
                destinationName = _cachedTripDestination;
                return !string.IsNullOrWhiteSpace(line) ||
                       !string.IsNullOrWhiteSpace(route) ||
                       !string.IsNullOrWhiteSpace(nextStopName) ||
                       !string.IsNullOrWhiteSpace(destinationName);
            }

            var timeTablePointer =
                ReadOnlyProcessMemory.PointerFromUInt32(timeTableAddress);
            var tripsAddress = memory.ReadUInt32(nint.Add(
                timeTablePointer,
                Omsi23004MemoryProfile.TimeTableTripsOffset));
            if (tripsAddress <= 0x10000u)
            {
                return !string.IsNullOrWhiteSpace(nextStopName);
            }

            var tripsPointer =
                ReadOnlyProcessMemory.PointerFromUInt32(tripsAddress);
            try
            {
                var count =
                    memory.ReadInt32(nint.Subtract(tripsPointer, sizeof(int)));
                if (count > 0 && count < 100000 && tripIndex >= count)
                {
                    return !string.IsNullOrWhiteSpace(nextStopName);
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
            destinationName =
                memory.ReadNullTerminatedAnsiStringField(nint.Add(
                    tripPointer,
                    Omsi23004MemoryProfile.TripTargetOffset));
            route = !string.IsNullOrWhiteSpace(trackName)
                ? trackName
                : destinationName;

            _cachedTripVehicleAddress = vehicleAddress;
            _cachedTripManagerAddress = timeTableAddress;
            _cachedTripIndex = tripIndex;
            _cachedTripTextTickMs = now;
            _cachedTripLine = line;
            _cachedTripRoute = route;
            _cachedTripDestination = destinationName;

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

    private void ClearTripCache()
    {
        _cachedTripVehicleAddress = nint.Zero;
        _cachedTripManagerAddress = 0;
        _cachedTripIndex = -1;
        _cachedTripTextTickMs = 0;
        _cachedTripLine = null;
        _cachedTripRoute = null;
        _cachedTripDestination = null;
        _cachedNextStopStringPointer = 0;
        _cachedNextStopTickMs = 0;
        _cachedNextStopName = null;
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

    private string? TryReadMapNameCached(
        ReadOnlyProcessMemory memory,
        out bool mapLoaded)
    {
        mapLoaded = false;

        try
        {
            var mapAddress = memory.ReadUInt32(
                memory.AddressFromRva(
                    Omsi23004MemoryProfile.MapPointerRva));
            if (mapAddress <= 0x10000u)
            {
                ClearMapNameCache();
                return null;
            }

            var mapPointer =
                ReadOnlyProcessMemory.PointerFromUInt32(mapAddress);
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

            var now = Environment.TickCount64;
            if (_cachedMapNameAddress == mapAddress &&
                !string.IsNullOrWhiteSpace(_cachedMapName) &&
                _cachedMapNameTickMs > 0 &&
                now >= _cachedMapNameTickMs &&
                now - _cachedMapNameTickMs < MapNameRefreshMs)
            {
                mapLoaded = true;
                return _cachedMapName;
            }

            var mapName = memory.ReadNullTerminatedUnicodeStringField(
                nint.Add(mapPointer, Omsi23004MemoryProfile.MapNameOffset),
                maxCharacters: 128);

            _cachedMapNameAddress = mapAddress;
            _cachedMapNameTickMs = now;
            _cachedMapName = string.IsNullOrWhiteSpace(mapName)
                ? null
                : mapName;

            if (_cachedMapName is not null)
            {
                mapLoaded = true;
            }

            return _cachedMapName;
        }
        catch
        {
            return null;
        }
    }

    private void ClearMapNameCache()
    {
        _cachedMapNameAddress = 0;
        _cachedMapNameTickMs = 0;
        _cachedMapName = null;
    }

    private readonly record struct VehicleVisualTelemetry(
        VehicleLightFlags Lights,
        TurnSignalState TurnSignal);

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
        ClearVehicleIdentityCache();
        ClearVehicleTileCache();
        ClearMapNameCache();
        ClearTripCache();
    }
}
