using Microsoft.Win32;
using NavBR.Client.Driver;
using NavBR.Client.Operations;

namespace NavBR.Client;

public partial class MainWindow
{
    private DriverProfileImportResult? _webPendingDriverProfileImport;
    private string? _webDriverProfileTransferNotice;

    private object BuildWebOperationsState()
    {
        var telemetry = _lastTelemetry;
        var session = DispatcherSessionFeed.Snapshot();
        var reports = DispatcherOperationalFeed.Snapshot();
        var company = VirtualCompanyStore.Load();
        var profile = DriverProfileStore.Load();
        var tripHistory = DriverTripHistoryStore.Load();
        var now = DateTimeOffset.UtcNow;

        return new
        {
            connected = session.Connected,
            roomId = session.RoomId,
            updatedAtUtc = session.UpdatedAt,
            canManageReports = DispatcherOperationalFeed.CanManageReports,
            localOperation = telemetry is null
                ? null
                : new
                {
                    inGame = telemetry.IsInGame,
                    mapName = telemetry.MapName,
                    vehicleName = telemetry.VehicleName,
                    line = telemetry.Line,
                    route = telemetry.Route,
                    destination = telemetry.DestinationName,
                    nextStop = telemetry.NextStopName,
                    currentStreet = telemetry.CurrentStreetName,
                    speedKph = telemetry.SpeedKph,
                    delaySeconds = telemetry.DelaySeconds,
                    doors = telemetry.Doors.ToString(),
                    stopRequested = telemetry.StopRequested,
                    headingDegrees = telemetry.HeadingDegrees
                },
            drivers = session.RemoteDrivers
                .Select(driver =>
                {
                    var latestReport = DispatcherOperationalFeed.LatestForPlayer(driver.PlayerId);
                    return new
                    {
                        playerId = driver.PlayerId,
                        displayName = driver.DisplayName,
                        roomId = driver.RoomId,
                        mapName = driver.MapName,
                        vehicleName = driver.VehicleName,
                        line = driver.Line,
                        route = driver.Route,
                        destination = driver.Destination,
                        nextStop = driver.NextStop,
                        speedKph = driver.SpeedKph,
                        delaySeconds = driver.DelaySeconds,
                        headingDegrees = driver.HeadingDegrees,
                        receivedAtUtc = driver.ReceivedAtUtc,
                        stale = now - driver.ReceivedAtUtc > TimeSpan.FromSeconds(10d),
                        latestReport = latestReport is null
                            ? null
                            : new
                            {
                                reportId = latestReport.ReportId,
                                kind = latestReport.Kind.ToString(),
                                severity = latestReport.Severity.ToString(),
                                status = latestReport.Status.ToString(),
                                message = latestReport.Message
                            }
                    };
                })
                .ToArray(),
            reports = reports
                .Select(report => new
                {
                    reportId = report.ReportId,
                    roomId = report.RoomId,
                    playerId = report.PlayerId,
                    displayName = report.DisplayName,
                    kind = report.Kind.ToString(),
                    severity = report.Severity.ToString(),
                    status = report.Status.ToString(),
                    message = report.Message,
                    createdAtUtc = report.CreatedAtUtc,
                    updatedAtUtc = report.UpdatedAtUtc,
                    acknowledgedByPlayerId = report.AcknowledgedByPlayerId
                })
                .ToArray(),
            company = new
            {
                name = company.Name,
                shortName = company.ShortName,
                baseMap = company.BaseMap,
                fleet = company.Vehicles
                    .OrderBy(vehicle => vehicle.FleetNumber, StringComparer.CurrentCultureIgnoreCase)
                    .Select(vehicle => new
                    {
                        id = vehicle.Id,
                        fleetNumber = vehicle.FleetNumber,
                        vehicleModel = vehicle.VehicleModel,
                        livery = vehicle.Livery,
                        addedAt = vehicle.AddedAt,
                        lastUsedAt = vehicle.LastUsedAt
                    })
                    .ToArray()
            },
            profile = new
            {
                displayName = profile.DisplayName,
                companyName = profile.CompanyName,
                totalDrivingSeconds = profile.TotalDrivingSeconds,
                totalDistanceKm = profile.TotalDistanceKm,
                trips = profile.Trips,
                highestSpeedKph = profile.HighestSpeedKph,
                averageMovingSpeedKph = profile.AverageMovingSpeedKph,
                lastMap = profile.LastMap,
                lastLine = profile.LastLine,
                lastRoute = profile.LastRoute,
                lastDrivenAt = profile.LastDrivenAt
            },
            tripHistory = tripHistory
                .Select(trip => new
                {
                    startedAtUtc = trip.StartedAtUtc,
                    endedAtUtc = trip.EndedAtUtc,
                    drivingSeconds = trip.DrivingSeconds,
                    distanceKm = trip.DistanceKm,
                    highestSpeedKph = trip.HighestSpeedKph,
                    mapName = trip.MapName,
                    line = trip.Line,
                    route = trip.Route,
                    vehicleName = trip.VehicleName
                })
                .ToArray(),
            profileTransfer = new
            {
                notice = _webDriverProfileTransferNotice,
                pending = _webPendingDriverProfileImport is null
                    ? null
                    : new
                    {
                        displayName = _webPendingDriverProfileImport.Profile.DisplayName,
                        companyName = _webPendingDriverProfileImport.Profile.CompanyName,
                        includesTripHistory = _webPendingDriverProfileImport.IncludesTripHistory,
                        tripCount = _webPendingDriverProfileImport.TripHistory?.Count ?? 0,
                        sourceVersion = _webPendingDriverProfileImport.SourceVersion
                    }
            }
        };
    }

    private static async Task HandleWebOperationalReportAsync(string reportId, bool resolve)
    {
        if (string.IsNullOrWhiteSpace(reportId))
        {
            return;
        }

        if (resolve)
        {
            await DispatcherOperationalFeed.ResolveAsync(reportId.Trim());
        }
        else
        {
            await DispatcherOperationalFeed.AcknowledgeAsync(reportId.Trim());
        }
    }

    private void SaveWebCompany(string? name, string? shortName, string? baseMap)
    {
        var company = VirtualCompanyStore.Load();
        VirtualCompanyStore.Save(company with
        {
            Name = name ?? company.Name,
            ShortName = shortName ?? company.ShortName,
            BaseMap = string.IsNullOrWhiteSpace(baseMap) ? null : baseMap.Trim()
        });
    }

    private void RegisterCurrentVehicleFromWeb(string? fleetNumber, string? livery)
    {
        var telemetry = _lastTelemetry;
        if (telemetry is null || string.IsNullOrWhiteSpace(telemetry.VehicleName))
        {
            throw new InvalidOperationException(
                "Nenhum ônibus real do OMSI está disponível para cadastro na frota.");
        }

        VirtualCompanyStore.RegisterVehicle(
            fleetNumber ?? string.Empty,
            telemetry.VehicleName,
            livery);
    }

    private static void RemoveFleetVehicleFromWeb(string? vehicleId)
    {
        if (!string.IsNullOrWhiteSpace(vehicleId))
        {
            VirtualCompanyStore.RemoveVehicle(vehicleId.Trim());
        }
    }

    private void ExportDriverProfileFromWeb()
    {
        _webDriverProfileTransferNotice = null;
        var profile = DriverProfileStore.Load();
        var safeName = string.Concat(
            (string.IsNullOrWhiteSpace(profile.DisplayName) ? "navbr-driver" : profile.DisplayName.Trim())
                .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));

        var dialog = new SaveFileDialog
        {
            Title = "Exportar perfil de motorista NavBR",
            Filter = "NavBR Driver Profile (*.navbr-profile.json)|*.navbr-profile.json|JSON (*.json)|*.json",
            FileName = safeName + ".navbr-profile.json",
            DefaultExt = ".json",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        DriverProfilePortability.ExportToFile(dialog.FileName, profile);
        _webDriverProfileTransferNotice =
            "Perfil do motorista e histórico de viagens exportados com sucesso.";
    }

    private void SelectDriverProfileImportFromWeb()
    {
        _webDriverProfileTransferNotice = null;
        var dialog = new OpenFileDialog
        {
            Title = "Importar perfil de motorista NavBR",
            Filter = "NavBR Driver Profile (*.navbr-profile.json;*.json)|*.navbr-profile.json;*.json|JSON (*.json)|*.json",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _webPendingDriverProfileImport = DriverProfilePortability.ImportFromFile(dialog.FileName);
    }

    private void ApplyDriverProfileImportFromWeb()
    {
        if (_webPendingDriverProfileImport is null)
        {
            return;
        }

        var includesHistory = _webPendingDriverProfileImport.IncludesTripHistory;
        DriverProfilePortability.ApplyImport(_webPendingDriverProfileImport);
        _webPendingDriverProfileImport = null;
        _webDriverProfileTransferNotice = includesHistory
            ? "Perfil do motorista e histórico de viagens importados com sucesso."
            : "Perfil importado com sucesso; o histórico local existente foi preservado.";
    }

    private void CancelDriverProfileImportFromWeb()
    {
        _webPendingDriverProfileImport = null;
        _webDriverProfileTransferNotice = null;
    }

    private static void SaveWebDriverProfile(string? displayName, string? companyName)
    {
        var profile = DriverProfileStore.Load();
        DriverProfileStore.Save(profile with
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? profile.DisplayName
                : displayName.Trim(),
            CompanyName = string.IsNullOrWhiteSpace(companyName)
                ? null
                : companyName.Trim()
        });
    }
}
