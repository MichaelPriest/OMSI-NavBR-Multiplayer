using System.IO;
using System.Text.Json;

namespace NavBR.Client.Operations;

internal sealed record FleetVehicleData(
    string Id,
    string FleetNumber,
    string VehicleModel,
    string? Livery = null,
    DateTimeOffset? AddedAt = null,
    DateTimeOffset? LastUsedAt = null);

internal sealed record VirtualCompanyData(
    string Name = "",
    string ShortName = "",
    string? BaseMap = null,
    IReadOnlyList<FleetVehicleData>? Fleet = null)
{
    public IReadOnlyList<FleetVehicleData> Vehicles => Fleet ?? Array.Empty<FleetVehicleData>();
}

internal static class VirtualCompanyStore
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "virtual-company.json");
    private static VirtualCompanyData? _cached;

    public static event Action<VirtualCompanyData>? CompanyChanged;

    public static VirtualCompanyData Load()
    {
        lock (Sync)
        {
            _cached ??= LoadCore();
            return _cached;
        }
    }

    public static void Save(VirtualCompanyData company)
    {
        company = Normalize(company);
        lock (Sync)
        {
            _cached = company;
            Persist(company);
        }
        CompanyChanged?.Invoke(company);
    }

    public static FleetVehicleData RegisterVehicle(
        string fleetNumber,
        string vehicleModel,
        string? livery = null)
    {
        var now = DateTimeOffset.UtcNow;
        FleetVehicleData registered;
        VirtualCompanyData updated;

        lock (Sync)
        {
            _cached ??= LoadCore();
            var vehicles = _cached.Vehicles.ToList();
            var normalizedNumber = string.IsNullOrWhiteSpace(fleetNumber)
                ? GenerateFleetNumber(_cached, vehicles.Count + 1)
                : fleetNumber.Trim();
            var normalizedModel = string.IsNullOrWhiteSpace(vehicleModel) ? "OMSI Bus" : vehicleModel.Trim();

            var existingIndex = vehicles.FindIndex(vehicle =>
                string.Equals(vehicle.FleetNumber, normalizedNumber, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                registered = vehicles[existingIndex] with
                {
                    VehicleModel = normalizedModel,
                    Livery = string.IsNullOrWhiteSpace(livery) ? vehicles[existingIndex].Livery : livery.Trim(),
                    LastUsedAt = now
                };
                vehicles[existingIndex] = registered;
            }
            else
            {
                registered = new FleetVehicleData(
                    Guid.NewGuid().ToString("N"),
                    normalizedNumber,
                    normalizedModel,
                    string.IsNullOrWhiteSpace(livery) ? null : livery.Trim(),
                    now,
                    now);
                vehicles.Add(registered);
            }

            updated = Normalize(_cached with { Fleet = vehicles });
            _cached = updated;
            Persist(updated);
        }

        CompanyChanged?.Invoke(updated);
        return registered;
    }

    public static void RemoveVehicle(string vehicleId)
    {
        VirtualCompanyData updated;
        lock (Sync)
        {
            _cached ??= LoadCore();
            updated = Normalize(_cached with
            {
                Fleet = _cached.Vehicles
                    .Where(vehicle => !string.Equals(vehicle.Id, vehicleId, StringComparison.OrdinalIgnoreCase))
                    .ToArray()
            });
            _cached = updated;
            Persist(updated);
        }
        CompanyChanged?.Invoke(updated);
    }

    private static VirtualCompanyData LoadCore()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var parsed = JsonSerializer.Deserialize<VirtualCompanyData>(File.ReadAllText(FilePath));
                if (parsed is not null)
                {
                    return Normalize(parsed);
                }
            }
        }
        catch
        {
            // Local company data is optional; a damaged file must not stop NavBR.
        }
        return new VirtualCompanyData();
    }

    private static VirtualCompanyData Normalize(VirtualCompanyData company)
    {
        var fleet = company.Vehicles
            .Where(vehicle => !string.IsNullOrWhiteSpace(vehicle.Id))
            .Select(vehicle => vehicle with
            {
                FleetNumber = string.IsNullOrWhiteSpace(vehicle.FleetNumber) ? "BUS" : vehicle.FleetNumber.Trim(),
                VehicleModel = string.IsNullOrWhiteSpace(vehicle.VehicleModel) ? "OMSI Bus" : vehicle.VehicleModel.Trim(),
                Livery = string.IsNullOrWhiteSpace(vehicle.Livery) ? null : vehicle.Livery.Trim()
            })
            .Take(500)
            .ToArray();

        return company with
        {
            Name = company.Name?.Trim() ?? string.Empty,
            ShortName = company.ShortName?.Trim() ?? string.Empty,
            BaseMap = string.IsNullOrWhiteSpace(company.BaseMap) ? null : company.BaseMap.Trim(),
            Fleet = fleet
        };
    }

    private static string GenerateFleetNumber(VirtualCompanyData company, int ordinal)
    {
        var prefix = string.IsNullOrWhiteSpace(company.ShortName)
            ? "NB"
            : new string(company.ShortName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "NB";
        }
        return $"{prefix}-{ordinal:000}";
    }

    private static void Persist(VirtualCompanyData company)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(
            FilePath,
            JsonSerializer.Serialize(company, new JsonSerializerOptions { WriteIndented = true }));
    }
}
