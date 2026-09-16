namespace NavBR.Shared.Multiplayer;

public static class ExperimentalFeatureFlags
{
    private const string EnableFileName = "experimental-physical-vehicles.enabled";

    public static string PhysicalVehiclesFlagPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer",
        EnableFileName);

    public static bool PhysicalVehiclesEnabled
    {
        get
        {
            if (File.Exists(PhysicalVehiclesFlagPath))
            {
                return true;
            }

            // Keep the original developer opt-in path for controlled tests.
            return IsTruthy(Environment.GetEnvironmentVariable("NAVBR_OMSI_EXPERIMENTAL_WRITES")) &&
                   IsTruthy(Environment.GetEnvironmentVariable("NAVBR_OMSI_PHYSICAL_BACKEND"));
        }
    }

    public static void SetPhysicalVehiclesEnabled(bool enabled)
    {
        var path = PhysicalVehiclesFlagPath;
        if (enabled)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                path,
                $"enabled=1{Environment.NewLine}updatedUtc={DateTimeOffset.UtcNow:O}{Environment.NewLine}");
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
