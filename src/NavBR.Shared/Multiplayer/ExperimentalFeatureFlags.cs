namespace NavBR.Shared.Multiplayer;

public static class ExperimentalFeatureFlags
{
    private const string EnableFileName = "experimental-physical-vehicles.enabled";
    private const string WritesEnvironmentVariable = "NAVBR_OMSI_EXPERIMENTAL_WRITES";
    private const string BackendEnvironmentVariable = "NAVBR_OMSI_PHYSICAL_BACKEND";
    private const string RoleplayEnableFileName = "experimental-roleplay-character.enabled";
    private const string RoleplayEnvironmentVariable = "NAVBR_OMSI_ROLEPLAY_CHARACTER";
    private const string MobileVehicleControlsEnableFileName = "experimental-mobile-vehicle-controls.enabled";
    private const string MobileVehicleControlsEnvironmentVariable = "NAVBR_OMSI_MOBILE_VEHICLE_CONTROLS";

    public static string PhysicalVehiclesFlagPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer",
        EnableFileName);

    public static string RoleplayCharacterFlagPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer",
        RoleplayEnableFileName);

    public static string MobileVehicleControlsFlagPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer",
        MobileVehicleControlsEnableFileName);

    public static bool PhysicalVehiclesEnabled
    {
        get
        {
            if (File.Exists(PhysicalVehiclesFlagPath))
            {
                return true;
            }

            return IsTruthy(Environment.GetEnvironmentVariable(WritesEnvironmentVariable)) &&
                   IsTruthy(Environment.GetEnvironmentVariable(BackendEnvironmentVariable));
        }
    }

    public static void SetPhysicalVehiclesEnabled(bool enabled)
    {
        var value = enabled ? "1" : null;

        // Process scope covers OMSI launched as a child after the option is
        // changed. User scope covers a separately launched OMSI after restart.
        Environment.SetEnvironmentVariable(WritesEnvironmentVariable, value);
        Environment.SetEnvironmentVariable(BackendEnvironmentVariable, value);
        TrySetUserEnvironmentVariable(WritesEnvironmentVariable, value);
        TrySetUserEnvironmentVariable(BackendEnvironmentVariable, value);

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

    public static bool RoleplayCharacterEnabled =>
        File.Exists(RoleplayCharacterFlagPath) ||
        IsTruthy(Environment.GetEnvironmentVariable(RoleplayEnvironmentVariable));

    public static bool MobileVehicleControlsEnabled =>
        File.Exists(MobileVehicleControlsFlagPath) ||
        IsTruthy(Environment.GetEnvironmentVariable(MobileVehicleControlsEnvironmentVariable));

    public static void SetMobileVehicleControlsEnabled(bool enabled)
    {
        var value = enabled ? "1" : null;
        Environment.SetEnvironmentVariable(MobileVehicleControlsEnvironmentVariable, value);
        TrySetUserEnvironmentVariable(MobileVehicleControlsEnvironmentVariable, value);

        var path = MobileVehicleControlsFlagPath;
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

    public static void SetRoleplayCharacterEnabled(bool enabled)
    {
        var value = enabled ? "1" : null;
        Environment.SetEnvironmentVariable(RoleplayEnvironmentVariable, value);
        TrySetUserEnvironmentVariable(RoleplayEnvironmentVariable, value);

        var path = RoleplayCharacterFlagPath;
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

    private static void TrySetUserEnvironmentVariable(string name, string? value)
    {
        try
        {
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
        }
        catch (Exception)
        {
            // The marker file and process-level value still provide a safe
            // fallback when user environment persistence is restricted.
        }
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
