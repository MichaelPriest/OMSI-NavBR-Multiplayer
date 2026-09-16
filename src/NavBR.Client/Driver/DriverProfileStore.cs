using System.IO;
using System.Text.Json;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Driver;

internal sealed record DriverProfileData(
    string DisplayName,
    string? CompanyName = null,
    double TotalDrivingSeconds = 0d,
    double TotalDistanceKm = 0d,
    int Trips = 0,
    double HighestSpeedKph = 0d,
    string? LastMap = null,
    string? LastLine = null,
    string? LastRoute = null,
    DateTimeOffset? LastDrivenAt = null)
{
    public double AverageMovingSpeedKph =>
        TotalDrivingSeconds > 1d
            ? TotalDistanceKm / (TotalDrivingSeconds / 3600d)
            : 0d;
}

internal static class DriverProfileStore
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "driver-profile.json");

    private static DriverProfileData? _cached;

    public static event Action<DriverProfileData>? ProfileChanged;

    public static DriverProfileData Load()
    {
        lock (Sync)
        {
            _cached ??= LoadCore();
            return _cached;
        }
    }

    public static void Save(DriverProfileData profile)
    {
        profile = Normalize(profile);
        lock (Sync)
        {
            _cached = profile;
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
        }

        ProfileChanged?.Invoke(profile);
    }

    public static void Update(Func<DriverProfileData, DriverProfileData> update)
    {
        DriverProfileData updated;
        lock (Sync)
        {
            _cached ??= LoadCore();
            updated = Normalize(update(_cached));
            _cached = updated;
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }));
        }

        ProfileChanged?.Invoke(updated);
    }

    private static DriverProfileData LoadCore()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var parsed = JsonSerializer.Deserialize<DriverProfileData>(File.ReadAllText(FilePath));
                if (parsed is not null)
                {
                    return Normalize(parsed);
                }
            }
        }
        catch
        {
            // A damaged local profile must never prevent NavBR from starting.
        }

        var multiplayerName = MultiplayerSettingsStore.Load().DisplayName;
        return Normalize(new DriverProfileData(
            string.IsNullOrWhiteSpace(multiplayerName) ? "Driver" : multiplayerName.Trim()));
    }

    private static DriverProfileData Normalize(DriverProfileData profile) => profile with
    {
        DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? "Driver" : profile.DisplayName.Trim(),
        CompanyName = string.IsNullOrWhiteSpace(profile.CompanyName) ? null : profile.CompanyName.Trim(),
        TotalDrivingSeconds = Math.Max(0d, double.IsFinite(profile.TotalDrivingSeconds) ? profile.TotalDrivingSeconds : 0d),
        TotalDistanceKm = Math.Max(0d, double.IsFinite(profile.TotalDistanceKm) ? profile.TotalDistanceKm : 0d),
        Trips = Math.Max(0, profile.Trips),
        HighestSpeedKph = Math.Clamp(double.IsFinite(profile.HighestSpeedKph) ? profile.HighestSpeedKph : 0d, 0d, 220d)
    };
}
