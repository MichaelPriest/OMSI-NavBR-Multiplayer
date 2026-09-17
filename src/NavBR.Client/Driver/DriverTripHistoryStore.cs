using System.IO;
using System.Text.Json;

namespace NavBR.Client.Driver;

internal sealed record DriverTripHistoryEntry(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    double DrivingSeconds,
    double DistanceKm,
    double HighestSpeedKph,
    string? MapName,
    string? Line,
    string? Route,
    string? VehicleName);

internal sealed record DriverTripHistoryDocument(
    string Schema,
    int Version,
    IReadOnlyList<DriverTripHistoryEntry> Trips);

internal static class DriverTripHistoryStore
{
    private const string Schema = "navbr-driver-trip-history";
    private const int Version = 1;
    private const int MaximumTrips = 250;
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "driver-trip-history.json");

    public static IReadOnlyList<DriverTripHistoryEntry> Load()
    {
        lock (Sync)
        {
            return LoadCore();
        }
    }

    public static void Append(DriverTripHistoryEntry entry)
    {
        entry = Normalize(entry);
        lock (Sync)
        {
            var trips = LoadCore().ToList();
            trips.Insert(0, entry);
            if (trips.Count > MaximumTrips)
            {
                trips.RemoveRange(MaximumTrips, trips.Count - MaximumTrips);
            }

            Directory.CreateDirectory(DirectoryPath);
            var document = new DriverTripHistoryDocument(Schema, Version, trips);
            var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, FilePath, true);
        }
    }

    private static IReadOnlyList<DriverTripHistoryEntry> LoadCore()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return Array.Empty<DriverTripHistoryEntry>();
            }

            var document = JsonSerializer.Deserialize<DriverTripHistoryDocument>(
                File.ReadAllText(FilePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (document is null ||
                !string.Equals(document.Schema, Schema, StringComparison.Ordinal) ||
                document.Version != Version ||
                document.Trips is null)
            {
                return Array.Empty<DriverTripHistoryEntry>();
            }

            return document.Trips
                .Select(Normalize)
                .OrderByDescending(entry => entry.StartedAtUtc)
                .Take(MaximumTrips)
                .ToArray();
        }
        catch
        {
            // A damaged history file must never prevent the client from starting.
            return Array.Empty<DriverTripHistoryEntry>();
        }
    }

    private static DriverTripHistoryEntry Normalize(DriverTripHistoryEntry entry)
    {
        var ended = entry.EndedAtUtc < entry.StartedAtUtc ? entry.StartedAtUtc : entry.EndedAtUtc;
        return entry with
        {
            EndedAtUtc = ended,
            DrivingSeconds = Math.Max(0d, double.IsFinite(entry.DrivingSeconds) ? entry.DrivingSeconds : 0d),
            DistanceKm = Math.Max(0d, double.IsFinite(entry.DistanceKm) ? entry.DistanceKm : 0d),
            HighestSpeedKph = Math.Clamp(double.IsFinite(entry.HighestSpeedKph) ? entry.HighestSpeedKph : 0d, 0d, 220d),
            MapName = Clean(entry.MapName),
            Line = Clean(entry.Line),
            Route = Clean(entry.Route),
            VehicleName = Clean(entry.VehicleName)
        };
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
