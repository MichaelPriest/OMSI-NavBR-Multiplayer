namespace NavBR.OmsiPluginExperimental;

internal static class PhysicalVehicleInstanceRegistry
{
    // Keep the public Alpha.11 experiment bounded to the same initial room
    // target. This limits pointer ownership, OMSI object pressure and any
    // unresolved temporary allocations while the community validates 3D sync.
    private const int MaxOwnedVehicles = 32;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, PhysicalVehicleInstance> Entries =
        new(StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string? instanceId, out PhysicalVehicleInstance instance)
    {
        instance = default;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        lock (Sync)
        {
            return Entries.TryGetValue(instanceId, out instance);
        }
    }

    public static bool TryAdd(PhysicalVehicleInstance instance)
    {
        var ownedPointers = instance.GetOwnedVehiclePointers();
        if (string.IsNullOrWhiteSpace(instance.InstanceId) ||
            instance.InstanceId.Length > 128 ||
            instance.VehiclePointer <= 0 ||
            ownedPointers.Length is <= 0 or > 16 ||
            ownedPointers.Any(pointer => pointer <= 0) ||
            ownedPointers.Distinct().Count() != ownedPointers.Length ||
            !ownedPointers.Contains(instance.VehiclePointer) ||
            string.IsNullOrWhiteSpace(instance.VehiclePath))
        {
            return false;
        }

        lock (Sync)
        {
            if (Entries.ContainsKey(instance.InstanceId) || Entries.Count >= MaxOwnedVehicles)
            {
                return false;
            }

            Entries.Add(instance.InstanceId, instance);
            return true;
        }
    }

    public static bool TryRemove(string? instanceId, out PhysicalVehicleInstance instance)
    {
        instance = default;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        lock (Sync)
        {
            return Entries.Remove(instanceId, out instance);
        }
    }

    public static PhysicalVehicleInstance[] Snapshot()
    {
        lock (Sync)
        {
            return Entries.Values.ToArray();
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Entries.Clear();
        }
    }
}

internal readonly record struct PhysicalVehicleInstance(
    string InstanceId,
    int VehiclePointer,
    string VehiclePath,
    DateTimeOffset CreatedAtUtc,
    int HostVehiclePointerAtSpawn,
    bool PointerWasAbsentBeforeSpawn,
    int[]? VehiclePointers = null)
{
    public int[] GetOwnedVehiclePointers() =>
        VehiclePointers is { Length: > 0 }
            ? VehiclePointers
            : [VehiclePointer];

    public int PartCount =>
        VehiclePointers is { Length: > 0 }
            ? VehiclePointers.Length
            : 1;
}
