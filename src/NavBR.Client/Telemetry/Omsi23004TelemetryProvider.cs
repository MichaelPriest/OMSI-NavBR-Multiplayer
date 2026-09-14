using System.Diagnostics;
using NavBR.Client.Omsi;
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

            // Absolute vehicle position comes from OmsiMapObjInst.AbsPosition.Position
            // (D3DMatrix _30/_31/_32).
            var absolutePosition = memory.ReadVector3(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleAbsPositionOffset +
                Omsi23004MemoryProfile.MatrixTranslationOffset));

            var rotation = memory.ReadQuaternion(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleRotationOffset));

            var velocity = memory.ReadVector3(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleVelocityOffset));

            var speedMps = Math.Sqrt(
                velocity.X * velocity.X +
                velocity.Y * velocity.Y +
                velocity.Z * velocity.Z);

            var groundSpeed = Math.Abs(memory.ReadSingle(nint.Add(
                vehicleAddress,
                Omsi23004MemoryProfile.VehicleGroundSpeedOffset)));

            if (!double.IsFinite(speedMps) || speedMps > 150)
            {
                speedMps = groundSpeed;
            }

            var mapName = TryReadMapName(memory, out var mapLoaded);
            var heading = QuaternionToHeadingDegrees(rotation);

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

            LastErrorCode = TelemetryErrorCode.None;
            return new VehicleTelemetry(
                PlayerId: playerId,
                Timestamp: DateTimeOffset.UtcNow,
                MapName: mapName,
                VehicleName: null,
                Line: null,
                Route: null,
                X: absolutePosition.X,
                Y: absolutePosition.Y,
                Z: absolutePosition.Z,
                HeadingDegrees: heading,
                SpeedKph: speedMps * 3.6,
                IsInGame: mapLoaded,
                GridX: gridX,
                GridY: gridY,
                TileX: tileX,
                TileY: tileY);
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

    private static nint ResolvePlayerVehicleAddress(
        ReadOnlyProcessMemory memory,
        int playerVehicleIndex)
    {
        var omsiListAddress = memory.ReadInt32(
            memory.AddressFromRva(Omsi23004MemoryProfile.RoadVehiclesListRva));
        if (omsiListAddress <= 0x10000)
        {
            return nint.Zero;
        }

        var fListAddress = memory.ReadInt32(nint.Add(
            new nint(omsiListAddress),
            Omsi23004MemoryProfile.OmsiListFListOffset));
        if (fListAddress <= 0x10000)
        {
            return nint.Zero;
        }

        var itemsAddress = memory.ReadInt32(nint.Add(
            new nint(fListAddress),
            Omsi23004MemoryProfile.TListItemsOffset));
        if (itemsAddress <= 0x10000)
        {
            return nint.Zero;
        }

        var vehicleAddress = memory.ReadInt32(nint.Add(
            new nint(itemsAddress),
            checked(playerVehicleIndex * sizeof(int))));

        return vehicleAddress > 0x10000
            ? new nint(vehicleAddress)
            : nint.Zero;
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
            var mapAddress = memory.ReadInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.MapPointerRva));
            var navigationVehicleAddress = memory.ReadInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.NavigationVehiclePointerRva));

            if (mapAddress <= 0x10000 || navigationVehicleAddress <= 0x10000)
            {
                return false;
            }

            var mapPointer = new nint(mapAddress);
            var navigationVehiclePointer = new nint(navigationVehicleAddress);

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
            // Navigation coordinates are supplementary. A map/layout mismatch
            // must not take down the primary telemetry provider.
            return false;
        }
    }

    private static string? TryReadMapName(
        ReadOnlyProcessMemory memory,
        out bool mapLoaded)
    {
        mapLoaded = false;

        var mapAddress = memory.ReadInt32(
            memory.AddressFromRva(Omsi23004MemoryProfile.MapPointerRva));
        if (mapAddress <= 0x10000)
        {
            return null;
        }

        var mapPointer = new nint(mapAddress);
        mapLoaded = memory.ReadByte(nint.Add(
            mapPointer,
            Omsi23004MemoryProfile.MapLoadedOffset)) != 0;

        // TMap + 0x150 is the canonical map name used by OMSI. Keep the
        // adjacent friendly-name field only as a fallback for installations
        // where the canonical field is blank.
        var mapName = memory.ReadDelphiUnicodeStringField(nint.Add(
            mapPointer,
            Omsi23004MemoryProfile.MapNameOffset));

        if (!string.IsNullOrWhiteSpace(mapName))
        {
            return mapName;
        }

        return memory.ReadDelphiUnicodeStringField(nint.Add(
            mapPointer,
            Omsi23004MemoryProfile.MapFriendlyNameOffset));
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
