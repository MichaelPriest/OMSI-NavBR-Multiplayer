using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NavBR.Client.Maps;
using NavBR.Client.Multiplayer;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private const double HudMapWidth = 296d;
    private const double HudMapHeight = 186d;
    private const double HudSourceViewWidth = 900d;

    private readonly Dictionary<string, PlayerTelemetryFrame> _smoothedHudFrames =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RemoteMotionSmoother> _smoothedHudMotion =
        new(StringComparer.OrdinalIgnoreCase);

    private DispatcherTimer? _remoteSmoothingTimer;
    private bool _remoteSmoothingClosedHooked;

    public void UpdateRemotePlayerSmooth(PlayerTelemetryFrame frame)
    {
        _smoothedHudFrames[frame.Player.PlayerId] = frame;
        EnsureRemoteSmoothingTimer();
        RefreshRemoteSmoothingCadence();
    }

    public void RemoveRemotePlayerSmooth(string playerId)
    {
        _smoothedHudFrames.Remove(playerId);
        _smoothedHudMotion.Remove(playerId);
        RefreshRemoteSmoothingCadence();
        RemoveRemotePlayer(playerId);
    }

    public void ClearRemotePlayersSmooth()
    {
        _smoothedHudFrames.Clear();
        _smoothedHudMotion.Clear();
        ClearRemotePlayers();
    }

    private void EnsureRemoteSmoothingTimer()
    {
        if (_remoteSmoothingTimer is null)
        {
            _remoteSmoothingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _remoteSmoothingTimer.Tick += (_, _) => UpdateSmoothedRemotePlayers();
        }

        if (!_remoteSmoothingClosedHooked)
        {
            _remoteSmoothingClosedHooked = true;
            Closed += (_, _) => _remoteSmoothingTimer?.Stop();
        }

        if (!_remoteSmoothingTimer.IsEnabled)
        {
            _remoteSmoothingTimer.Start();
        }
    }

    private void RefreshRemoteSmoothingCadence()
    {
        if (_remoteSmoothingTimer is null)
        {
            return;
        }

        var intervalMs = _smoothedHudFrames.Count switch
        {
            >= 16 => 100d, // 10 FPS in very large rooms.
            >= 7 => 50d,   // 20 FPS in medium rooms.
            _ => 33d       // ~30 FPS for small rooms.
        };

        var desired = TimeSpan.FromMilliseconds(intervalMs);
        if (_remoteSmoothingTimer.Interval != desired)
        {
            _remoteSmoothingTimer.Interval = desired;
        }
    }

    private void UpdateSmoothedRemotePlayers()
    {
        RenderRemoteNameplates();

        var local = _localTelemetry;
        var map = _activeMap;
        var bitmap = _mapBitmap;
        var layout = _mapLayout;

        if (local is null ||
            map is null ||
            bitmap is null ||
            layout is null ||
            local.GridX is not int localGridX ||
            local.GridY is not int localGridY ||
            local.TileX is not double localTileX ||
            local.TileY is not double localTileY ||
            !RoadmapTransform.TryToPixel(
                layout,
                bitmap.PixelWidth,
                bitmap.PixelHeight,
                localGridX,
                localGridY,
                localTileX,
                localTileY,
                out var localPixelX,
                out var localPixelY))
        {
            HideSmoothedHudMarkers();
            return;
        }

        var scale = HudMapWidth / HudSourceViewWidth;
        var now = DateTimeOffset.UtcNow;

        foreach (var pair in _smoothedHudFrames.ToArray())
        {
            var playerId = pair.Key;
            var frame = pair.Value;
            var remote = frame.Telemetry;

            if (now - remote.Timestamp > TimeSpan.FromSeconds(3) ||
                !IsHudMapCompatible(map, local.MapName, frame) ||
                remote.GridX is not int gridX ||
                remote.GridY is not int gridY ||
                remote.TileX is not double tileX ||
                remote.TileY is not double tileY ||
                !RoadmapTransform.TryToPixel(
                    layout,
                    bitmap.PixelWidth,
                    bitmap.PixelHeight,
                    gridX,
                    gridY,
                    tileX,
                    tileY,
                    out var remotePixelX,
                    out var remotePixelY))
            {
                HideHudMarker(playerId);
                continue;
            }

            var targetX = HudMapWidth / 2d + (remotePixelX - localPixelX) * scale;
            var targetY = HudMapHeight / 2d + (remotePixelY - localPixelY) * scale;

            if (targetX < -16d || targetX > HudMapWidth + 16d ||
                targetY < -16d || targetY > HudMapHeight + 16d)
            {
                HideHudMarker(playerId);
                continue;
            }

            var marker = GetOrCreateRemoteMarker(playerId, frame.Player.DisplayName);
            var smoother = GetOrCreateHudSmoother(playerId);
            smoother.SetTarget(targetX, targetY, remote.HeadingDegrees, remote.Timestamp);
            var pose = smoother.Step(positionAlpha: 0.28d, headingAlpha: 0.25d);
            if (!pose.IsValid)
            {
                continue;
            }

            UpdateRemoteMarkerVisual(
                marker,
                frame.Player.DisplayName,
                pose.HeadingDegrees,
                MiniMapHeadingRotation.Angle,
                MiniMapContentScale.ScaleX);
            Canvas.SetLeft(marker, pose.X - marker.Width / 2d);
            Canvas.SetTop(marker, pose.Y - marker.Height / 2d);
            marker.ToolTip = $"{frame.Player.DisplayName} • {remote.SpeedKph:F1} km/h";
            marker.Visibility = Visibility.Visible;
        }
    }

    private RemoteMotionSmoother GetOrCreateHudSmoother(string playerId)
    {
        if (_smoothedHudMotion.TryGetValue(playerId, out var existing))
        {
            return existing;
        }

        var smoother = new RemoteMotionSmoother();
        _smoothedHudMotion[playerId] = smoother;
        return smoother;
    }

    private static bool IsHudMapCompatible(
        OmsiMapInfo localMap,
        string? localMapName,
        PlayerTelemetryFrame frame)
    {
        if (!string.IsNullOrWhiteSpace(localMap.CompatibilityId) &&
            !string.IsNullOrWhiteSpace(frame.Player.MapCompatibilityId) &&
            !string.Equals(
                localMap.CompatibilityId,
                frame.Player.MapCompatibilityId,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(localMapName) &&
               !string.IsNullOrWhiteSpace(frame.Telemetry.MapName) &&
               string.Equals(localMapName, frame.Telemetry.MapName, StringComparison.OrdinalIgnoreCase);
    }

    private void HideHudMarker(string playerId)
    {
        if (_remoteMarkers.TryGetValue(playerId, out var marker))
        {
            marker.Visibility = Visibility.Collapsed;
        }
    }

    private void HideSmoothedHudMarkers()
    {
        foreach (var playerId in _smoothedHudFrames.Keys)
        {
            HideHudMarker(playerId);
        }
    }
}
