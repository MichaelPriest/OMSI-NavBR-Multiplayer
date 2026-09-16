using NavBR.Client.Omsi;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Telemetry;

/// <summary>
/// Read-only alpha.11 capture of nearby OMSI road traffic. The room host can
/// publish these compact states so clients reproduce the same relevant traffic
/// instead of each simulator generating a different local scene.
/// </summary>
internal static class OmsiRoadTrafficReader
{
    public const int DefaultMaxVehicles = 48;
    public const double DefaultRadiusMeters = 900d;

    public static IReadOnlyList<TrafficVehicleState> Read(
        ReadOnlyProcessMemory memory,
        OmsiProcessInfo processInfo,
        int maxVehicles = DefaultMaxVehicles,
        double radiusMeters = DefaultRadiusMeters)
    {
        if (!processInfo.IsOmsi23004Exact || maxVehicles <= 0 || radiusMeters <= 0d)
        {
            return Array.Empty<TrafficVehicleState>();
        }

        maxVehicles = Math.Clamp(maxVehicles, 1, DefaultMaxVehicles);
        radiusMeters = Math.Clamp(radiusMeters, 100d, 2_500d);

        try
        {
            var playerIndex = memory.ReadInt32(
                memory.AddressFromRva(Omsi23004MemoryProfile.PlayerVehicleIndexRva));
            if (playerIndex < 0 || playerIndex > 10_000)
            {
                return Array.Empty<TrafficVehicleState>();
            }

            if (!TryResolveRoadVehicleList(memory, out var itemsPointer, out var count) ||
                !TryResolveVehicleAddress(memory, itemsPointer, count, playerIndex, out var playerAddress))
            {
                return Array.Empty<TrafficVehicleState>();
            }

            var playerAbs = memory.ReadVector3(nint.Add(
                playerAddress,
                Omsi23004MemoryProfile.VehicleAbsPositionOffset +
                Omsi23004MemoryProfile.MatrixTranslationOffset));
            if (!IsFinite(playerAbs))
            {
                return Array.Empty<TrafficVehicleState>();
            }

            var radiusSquared = radiusMeters * radiusMeters;
            var candidates = new List<(double DistanceSquared, TrafficVehicleState Vehicle)>(
                Math.Min(maxVehicles * 2, 96));

            for (var listIndex = 0; listIndex < count; listIndex++)
            {
                if (listIndex == playerIndex)
                {
                    continue;
                }

                try
                {
                    if (!TryResolveVehicleAddress(memory, itemsPointer, count, listIndex, out var vehicleAddress))
                    {
                        continue;
                    }

                    // Physical player buses use the dedicated multiplayer path.
                    // Traffic replication only takes OMSI AI road vehicles.
                    if (memory.ReadByte(nint.Add(
                            vehicleAddress,
                            Omsi23004MemoryProfile.VehiclePaiOffset)) == 0 ||
                        memory.ReadByte(nint.Add(
                            vehicleAddress,
                            Omsi23004MemoryProfile.MovingVehicleUserTrainOffset)) != 0)
                    {
                        continue;
                    }

                    var identity = OmsiVehicleIdentityReader.Read(memory, processInfo, vehicleAddress);
                    if (string.IsNullOrWhiteSpace(identity.RelativePath) ||
                        (!identity.RelativePath.EndsWith(".ovh", StringComparison.OrdinalIgnoreCase) &&
                         !identity.RelativePath.EndsWith(".bus", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var absolute = memory.ReadVector3(nint.Add(
                        vehicleAddress,
                        Omsi23004MemoryProfile.VehicleAbsPositionOffset +
                        Omsi23004MemoryProfile.MatrixTranslationOffset));
                    var local = memory.ReadVector3(nint.Add(
                        vehicleAddress,
                        Omsi23004MemoryProfile.VehiclePositionOffset));
                    var rotation = memory.ReadQuaternion(nint.Add(
                        vehicleAddress,
                        Omsi23004MemoryProfile.VehicleRotationOffset));

                    if (!IsFinite(absolute) || !IsFinite(local) || !IsFinite(rotation))
                    {
                        continue;
                    }

                    var dx = absolute.X - playerAbs.X;
                    var dy = absolute.Y - playerAbs.Y;
                    var dz = absolute.Z - playerAbs.Z;
                    var distanceSquared = dx * dx + dy * dy + dz * dz;
                    if (!double.IsFinite(distanceSquared) || distanceSquared > radiusSquared)
                    {
                        continue;
                    }

                    var speedKph = Math.Abs(memory.ReadSingle(nint.Add(
                        vehicleAddress,
                        Omsi23004MemoryProfile.VehicleGroundSpeedOffset))) * 3.6d;
                    if (!double.IsFinite(speedKph) || speedKph > 250d)
                    {
                        speedKph = 0d;
                    }

                    var lightFlags = ReadLightFlags(memory, vehicleAddress);
                    var turnSignal = ReadTurnSignal(memory, vehicleAddress);
                    var runtimeIndex = memory.ReadInt32(nint.Add(
                        vehicleAddress,
                        Omsi23004MemoryProfile.MovingVehicleIndexOffset));
                    var trafficId = runtimeIndex >= 0
                        ? $"rv:{runtimeIndex}"
                        : $"list:{listIndex}";

                    candidates.Add((
                        distanceSquared,
                        new TrafficVehicleState(
                            trafficId,
                            identity.RelativePath,
                            identity.CompatibilityId,
                            absolute.X,
                            absolute.Y,
                            absolute.Z,
                            local.X,
                            local.Y,
                            local.Z,
                            rotation.X,
                            rotation.Y,
                            rotation.Z,
                            rotation.W,
                            speedKph,
                            lightFlags,
                            turnSignal)));
                }
                catch
                {
                    // One malformed/addon vehicle must not cancel the complete
                    // traffic snapshot. Continue with the remaining vehicles.
                }
            }

            return candidates
                .OrderBy(candidate => candidate.DistanceSquared)
                .Take(maxVehicles)
                .Select(candidate => candidate.Vehicle)
                .ToArray();
        }
        catch
        {
            return Array.Empty<TrafficVehicleState>();
        }
    }

    private static bool TryResolveRoadVehicleList(
        ReadOnlyProcessMemory memory,
        out nint itemsPointer,
        out int count)
    {
        itemsPointer = nint.Zero;
        count = 0;

        var omsiListAddress = memory.ReadUInt32(
            memory.AddressFromRva(Omsi23004MemoryProfile.RoadVehiclesListRva));
        if (omsiListAddress <= 0x10000u)
        {
            return false;
        }

        var omsiListPointer = ReadOnlyProcessMemory.PointerFromUInt32(omsiListAddress);
        count = memory.ReadInt32(nint.Add(
            omsiListPointer,
            Omsi23004MemoryProfile.OmsiListCountOffset));
        if (count is <= 0 or > 4_096)
        {
            return false;
        }

        var fListAddress = memory.ReadUInt32(nint.Add(
            omsiListPointer,
            Omsi23004MemoryProfile.OmsiListFListOffset));
        if (fListAddress <= 0x10000u)
        {
            return false;
        }

        var fListPointer = ReadOnlyProcessMemory.PointerFromUInt32(fListAddress);
        var itemsAddress = memory.ReadUInt32(nint.Add(
            fListPointer,
            Omsi23004MemoryProfile.TListItemsOffset));
        if (itemsAddress <= 0x10000u)
        {
            return false;
        }

        itemsPointer = ReadOnlyProcessMemory.PointerFromUInt32(itemsAddress);
        return true;
    }

    private static bool TryResolveVehicleAddress(
        ReadOnlyProcessMemory memory,
        nint itemsPointer,
        int count,
        int index,
        out nint vehicleAddress)
    {
        vehicleAddress = nint.Zero;
        if (index < 0 || index >= count)
        {
            return false;
        }

        var rawAddress = memory.ReadUInt32(nint.Add(
            itemsPointer,
            checked(index * sizeof(int))));
        if (rawAddress <= 0x10000u)
        {
            return false;
        }

        vehicleAddress = ReadOnlyProcessMemory.PointerFromUInt32(rawAddress);
        return true;
    }

    private static int ReadLightFlags(ReadOnlyProcessMemory memory, nint vehicleAddress)
    {
        var flags = VehicleLightFlags.None;

        if (ReadPositiveFloat(memory, vehicleAddress, Omsi23004MemoryProfile.VehicleAiLightOffset))
        {
            flags |= VehicleLightFlags.Position | VehicleLightFlags.LowBeam;
        }

        if (ReadPositiveFloat(memory, vehicleAddress, Omsi23004MemoryProfile.VehicleAiInteriorLightOffset))
        {
            flags |= VehicleLightFlags.Interior;
        }

        if (ReadPositiveFloat(memory, vehicleAddress, Omsi23004MemoryProfile.VehicleAiBrakeLightOffset))
        {
            flags |= VehicleLightFlags.Brake;
        }

        var left = ReadPositiveFloat(memory, vehicleAddress, Omsi23004MemoryProfile.VehicleAiBlinkerLeftOffset);
        var right = ReadPositiveFloat(memory, vehicleAddress, Omsi23004MemoryProfile.VehicleAiBlinkerRightOffset);
        if (left && right)
        {
            flags |= VehicleLightFlags.Hazard;
        }

        return (int)flags;
    }

    private static int ReadTurnSignal(ReadOnlyProcessMemory memory, nint vehicleAddress)
    {
        var left = ReadPositiveFloat(memory, vehicleAddress, Omsi23004MemoryProfile.VehicleAiBlinkerLeftOffset);
        var right = ReadPositiveFloat(memory, vehicleAddress, Omsi23004MemoryProfile.VehicleAiBlinkerRightOffset);

        if (left && right)
        {
            return (int)TurnSignalState.Hazard;
        }

        if (left)
        {
            return (int)TurnSignalState.Left;
        }

        return right ? (int)TurnSignalState.Right : (int)TurnSignalState.Off;
    }

    private static bool ReadPositiveFloat(
        ReadOnlyProcessMemory memory,
        nint vehicleAddress,
        int offset)
    {
        var value = memory.ReadSingle(nint.Add(vehicleAddress, offset));
        return float.IsFinite(value) && value > 0.05f;
    }

    private static bool IsFinite(MemoryVector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

    private static bool IsFinite(MemoryQuaternion value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) &&
        float.IsFinite(value.W);
}
