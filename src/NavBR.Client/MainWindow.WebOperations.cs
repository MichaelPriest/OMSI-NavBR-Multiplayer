using NavBR.Client.Driver;
using NavBR.Client.Operations;

namespace NavBR.Client;

public partial class MainWindow
{
    private object BuildWebOperationsState()
    {
        var telemetry = _lastTelemetry;
        var session = DispatcherSessionFeed.Snapshot();
        var reports = DispatcherOperationalFeed.Snapshot();
        var company = VirtualCompanyStore.Load();
        var profile = DriverProfileStore.Load();
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
