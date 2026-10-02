using System.Windows;
using Microsoft.Win32;
using NavBR.Client.Driver;
using NavBR.Client.Network;
using NavBR.Client.Operations;
using NavBR.Shared.Network;

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
        var companyBadge = Application.Current is App app
            ? app.NetworkRuntime.CurrentBadge
            : null;
        var onlineCompany = _webCompanyNetworkSnapshot ?? CompanyNodeStore.LoadCompany();
        var operatorMember = onlineCompany?.Members.FirstOrDefault(member =>
            companyBadge is not null &&
            string.Equals(member.PlayerId, companyBadge.PlayerId, StringComparison.OrdinalIgnoreCase));
        var operatorBadgeVerified = CompanyEmployeeBadgeFactory.MatchesMember(
            companyBadge,
            onlineCompany,
            operatorMember);

        if (operatorBadgeVerified && companyBadge is not null)
        {
            var profileChanged =
                !string.Equals(
                    profile.DisplayName,
                    companyBadge.DisplayName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    profile.CompanyName,
                    companyBadge.CompanyName,
                    StringComparison.Ordinal);
            if (profileChanged)
            {
                profile = profile with
                {
                    DisplayName = companyBadge.DisplayName,
                    CompanyName = companyBadge.CompanyName
                };
                DriverProfileStore.Save(profile);
            }

            var companyChanged =
                !string.Equals(
                    company.Name,
                    companyBadge.CompanyName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    company.ShortName,
                    companyBadge.CompanyShortName,
                    StringComparison.Ordinal);
            if (companyChanged)
            {
                company = company with
                {
                    Name = companyBadge.CompanyName,
                    ShortName = companyBadge.CompanyShortName
                };
                VirtualCompanyStore.Save(company);
            }
        }

        var now = DateTimeOffset.UtcNow;

        return new
        {
            connected = session.Connected,
            roomId = session.RoomId,
            updatedAtUtc = session.UpdatedAt,
            canManageReports = DispatcherOperationalFeed.CanManageReports,
            operatorBadge = BuildWebCompanyBadge(companyBadge),
            operatorBadgeVerified,
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
                    var remoteMember = onlineCompany?.Members.FirstOrDefault(member =>
                        driver.CompanyBadge is not null &&
                        string.Equals(member.PlayerId, driver.CompanyBadge.PlayerId, StringComparison.OrdinalIgnoreCase));
                    var badgeVerified = CompanyEmployeeBadgeFactory.MatchesMember(
                        driver.CompanyBadge,
                        onlineCompany,
                        remoteMember);

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
                        companyBadge = BuildWebCompanyBadge(driver.CompanyBadge),
                        companyBadgeVerified = badgeVerified,
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

    private object BuildWebDriverState()
    {
        var profile = DriverProfileStore.Load();
        var tripHistory = DriverTripHistoryStore.Load();

        return new
        {
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
        var badge = (Application.Current as App)?.NetworkRuntime.CurrentBadge;
        var onlineCompany = _webCompanyNetworkSnapshot ?? CompanyNodeStore.LoadCompany();
        var member = onlineCompany?.Members.FirstOrDefault(item =>
            badge is not null &&
            string.Equals(
                item.PlayerId,
                badge.PlayerId,
                StringComparison.OrdinalIgnoreCase));
        var verified = CompanyEmployeeBadgeFactory.MatchesMember(
            badge,
            onlineCompany,
            member);

        VirtualCompanyStore.Save(company with
        {
            Name = verified && badge is not null
                ? badge.CompanyName
                : name ?? company.Name,
            ShortName = verified && badge is not null
                ? badge.CompanyShortName
                : shortName ?? company.ShortName,
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

        var badge = (Application.Current as App)?.NetworkRuntime.CurrentBadge;
        var onlineCompany = _webCompanyNetworkSnapshot ?? CompanyNodeStore.LoadCompany();
        var member = onlineCompany?.Members.FirstOrDefault(item =>
            badge is not null &&
            string.Equals(
                item.PlayerId,
                badge.PlayerId,
                StringComparison.OrdinalIgnoreCase));
        var verified = CompanyEmployeeBadgeFactory.MatchesMember(
            badge,
            onlineCompany,
            member);

        if (verified && badge is not null)
        {
            var importedProfile = DriverProfileStore.Load();
            DriverProfileStore.Save(importedProfile with
            {
                DisplayName = badge.DisplayName,
                CompanyName = badge.CompanyName
            });
        }

        _webPendingDriverProfileImport = null;
        _webDriverProfileTransferNotice = verified
            ? includesHistory
                ? "Perfil e histórico importados; nome e empresa foram preservados pelo crachá verificado."
                : "Perfil importado; histórico local preservado e identidade mantida pelo crachá verificado."
            : includesHistory
                ? "Perfil do motorista e histórico de viagens importados com sucesso."
                : "Perfil importado com sucesso; o histórico local existente foi preservado.";
    }

    private void CancelDriverProfileImportFromWeb()
    {
        _webPendingDriverProfileImport = null;
        _webDriverProfileTransferNotice = null;
    }

    private void SaveWebDriverProfile(string? displayName, string? companyName)
    {
        var profile = DriverProfileStore.Load();
        var badge = (Application.Current as App)?.NetworkRuntime.CurrentBadge;
        var onlineCompany = _webCompanyNetworkSnapshot ?? CompanyNodeStore.LoadCompany();
        var member = onlineCompany?.Members.FirstOrDefault(item =>
            badge is not null &&
            string.Equals(
                item.PlayerId,
                badge.PlayerId,
                StringComparison.OrdinalIgnoreCase));
        var verified = CompanyEmployeeBadgeFactory.MatchesMember(
            badge,
            onlineCompany,
            member);

        DriverProfileStore.Save(profile with
        {
            DisplayName = verified && badge is not null
                ? badge.DisplayName
                : string.IsNullOrWhiteSpace(displayName)
                    ? profile.DisplayName
                    : displayName.Trim(),
            CompanyName = verified && badge is not null
                ? badge.CompanyName
                : string.IsNullOrWhiteSpace(companyName)
                    ? null
                    : companyName.Trim()
        });
    }
}
