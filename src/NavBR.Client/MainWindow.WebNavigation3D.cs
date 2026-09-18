using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;
using NavBR.Shared.Telemetry;

namespace NavBR.Client;

public partial class MainWindow
{
    private object BuildWebNavigation3DState()
    {
        var telemetry = _lastTelemetry;
        var map = GetActiveMapForOperations();
        var layout = _webNavigationLayout;

        if (telemetry is null ||
            map is null ||
            !telemetry.IsInGame ||
            layout?.TileSize is not double tileSize ||
            layout.WorldWidth is not double worldWidth ||
            layout.WorldHeight is not double worldHeight)
        {
            return BuildUnavailableWebNavigation3D(map, telemetry);
        }

        var roadmapPath = ResolveWebNavigation3DRoadmapPath(map);
        var roadmapAvailable = File.Exists(roadmapPath);
        var minWorldX = layout.MinGridX * tileSize;
        var minWorldY = layout.MinGridY * tileSize;
        var maxWorldX = minWorldX + worldWidth;
        var maxWorldY = minWorldY + worldHeight;

        var routePoints = DecimateRoute(_webNavigationRoute, 900)
            .Select(point => new
            {
                x = point.GridX * tileSize + point.TileX,
                y = point.GridY * tileSize + point.TileY
            })
            .ToArray();

        object? localVehicle = TryGetWebNavigation3DPosition(
            telemetry,
            tileSize,
            out var localX,
            out var localY)
            ? new
            {
                x = localX,
                y = localY,
                headingDegrees = telemetry.HeadingDegrees,
                speedKph = telemetry.SpeedKph,
                line = telemetry.Line
            }
            : null;

        object? localRoleplayCharacter = null;
        var roleplayState = _roleplayCharacterController?.CurrentState;
        if (localVehicle is not null &&
            roleplayState is { IsActive: true } &&
            telemetry.LocalX is double busLocalX &&
            telemetry.LocalY is double busLocalY &&
            double.IsFinite(busLocalX) &&
            double.IsFinite(busLocalY))
        {
            // Vehicle and human native Position fields use the same OMSI local
            // coordinate frame. Anchor the character to the already-resolved
            // navigation world position by applying only the native local delta;
            // this remains stable when the bus sits near a tile boundary.
            var roleplayWorldX = localX + (roleplayState.LocalX - busLocalX);
            var roleplayWorldY = localY + (roleplayState.LocalY - busLocalY);
            if (double.IsFinite(roleplayWorldX) && double.IsFinite(roleplayWorldY))
            {
                localRoleplayCharacter = new
                {
                    x = roleplayWorldX,
                    y = roleplayWorldY,
                    z = roleplayState.LocalZ,
                    headingDegrees = roleplayState.HeadingDegrees,
                    speedMps = roleplayState.SpeedMps,
                    activity = roleplayState.Activity.ToString(),
                    characterName = roleplayState.CharacterName
                };
            }
        }

        var remoteVehicles = Navigation3DSessionFeed.Snapshot()
            .Where(item =>
                IsWebNavigation3DCompatible(map, item.Frame) &&
                item.Frame.Telemetry.IsInGame)
            .Select(item =>
            {
                var remoteTelemetry = item.Frame.Telemetry;
                if (!TryGetWebNavigation3DPosition(
                        remoteTelemetry,
                        tileSize,
                        out var x,
                        out var y))
                {
                    return null;
                }

                return new
                {
                    playerId = item.Frame.Player.PlayerId,
                    displayName = item.Frame.Player.DisplayName,
                    x,
                    y,
                    headingDegrees = remoteTelemetry.HeadingDegrees,
                    speedKph = remoteTelemetry.SpeedKph,
                    line = remoteTelemetry.Line
                };
            })
            .Where(item => item is not null)
            .ToArray();

        return new
        {
            available = roadmapAvailable && localVehicle is not null,
            mapName = map.DisplayName,
            mapFolder = map.FolderName,
            roadmapAvailable,
            roadmapUrl = roadmapAvailable
                ? "https://navbr-map.local/texture/map/whole.roadmap.bmp"
                : null,
            bounds = new
            {
                minX = minWorldX,
                minY = minWorldY,
                maxX = maxWorldX,
                maxY = maxWorldY
            },
            routePoints,
            localVehicle,
            localRoleplayCharacter,
            remoteVehicles,
            routeAvailable = routePoints.Length >= 2,
            remoteCount = remoteVehicles.Length
        };
    }

    private object BuildUnavailableWebNavigation3D(
        OmsiMapInfo? map,
        VehicleTelemetry? telemetry)
    {
        var roadmapPath = map is null
            ? null
            : ResolveWebNavigation3DRoadmapPath(map);
        var roadmapAvailable = !string.IsNullOrWhiteSpace(roadmapPath) &&
                               File.Exists(roadmapPath);

        return new
        {
            available = false,
            mapName = map?.DisplayName ?? telemetry?.MapName,
            mapFolder = map?.FolderName,
            roadmapAvailable,
            roadmapUrl = roadmapAvailable
                ? "https://navbr-map.local/texture/map/whole.roadmap.bmp"
                : null,
            bounds = null as object,
            routePoints = Array.Empty<object>(),
            localVehicle = null as object,
            localRoleplayCharacter = null as object,
            remoteVehicles = Array.Empty<object>(),
            routeAvailable = false,
            remoteCount = 0
        };
    }

    internal string? GetActiveWebMapResourceDirectory()
    {
        var map = GetActiveMapForOperations();
        return map is not null && Directory.Exists(map.DirectoryPath)
            ? map.DirectoryPath
            : null;
    }

    private static string ResolveWebNavigation3DRoadmapPath(OmsiMapInfo map) =>
        Path.Combine(
            map.DirectoryPath,
            "texture",
            "map",
            "whole.roadmap.bmp");

    private static bool TryGetWebNavigation3DPosition(
        VehicleTelemetry telemetry,
        double tileSize,
        out double x,
        out double y)
    {
        x = 0d;
        y = 0d;

        if (telemetry.GridX is not int gridX ||
            telemetry.GridY is not int gridY ||
            telemetry.TileX is not double tileX ||
            telemetry.TileY is not double tileY)
        {
            return false;
        }

        x = gridX * tileSize + tileX;
        y = gridY * tileSize + tileY;
        return double.IsFinite(x) && double.IsFinite(y);
    }

    private static bool IsWebNavigation3DCompatible(
        OmsiMapInfo localMap,
        PlayerTelemetryFrame frame)
    {
        var remoteMap = frame.Telemetry.MapName ?? frame.Player.MapName;
        if (!string.IsNullOrWhiteSpace(remoteMap) &&
            NormalizeWebNavigation3DMap(remoteMap) != NormalizeWebNavigation3DMap(localMap.DisplayName) &&
            NormalizeWebNavigation3DMap(remoteMap) != NormalizeWebNavigation3DMap(localMap.FolderName))
        {
            return false;
        }

        var remoteCompatibility =
            frame.Telemetry.MapCompatibilityId ??
            frame.Player.MapCompatibilityId;

        return string.IsNullOrWhiteSpace(localMap.CompatibilityId) ||
               string.IsNullOrWhiteSpace(remoteCompatibility) ||
               string.Equals(
                   localMap.CompatibilityId,
                   remoteCompatibility,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeWebNavigation3DMap(string value) => new(
        value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
}
