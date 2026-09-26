using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Telemetry;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private const double RemoteNameplateMaxDistanceMeters = 350d;
    private const double RemoteBusNameHeightMeters = 3.4d;

    private readonly Dictionary<string, Border> _remote3DNameplates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NameplateMotionState> _remote3DNameplateMotion =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _activePhysicalRemotePlayers =
        new(StringComparer.OrdinalIgnoreCase);

    private OmsiCameraProjectionSnapshot? _cameraProjection;

    internal void UpdateCameraProjection(OmsiCameraProjectionSnapshot? projection)
    {
        _cameraProjection = projection;
        RenderRemoteNameplates();
    }

    public void SetRemotePhysicalVehicleActive(string playerId, bool active)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        if (active)
        {
            _activePhysicalRemotePlayers.Add(playerId);
        }
        else
        {
            _activePhysicalRemotePlayers.Remove(playerId);
            HideRemoteNameplate(playerId);
        }
    }

    private void RenderRemoteNameplates()
    {
        var projection = _cameraProjection;
        var local = _localTelemetry;

        if (projection is null ||
            local is null ||
            !local.IsInGame ||
            DateTimeOffset.UtcNow - projection.Value.CapturedAtUtc > TimeSpan.FromSeconds(2d) ||
            local.LocalX is not double localX ||
            local.LocalY is not double localY ||
            local.LocalZ is not double localZ ||
            !TryGetOmsiClientViewport(out var viewport))
        {
            HideAllRemoteNameplates();
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in _smoothedHudFrames)
        {
            var playerId = pair.Key;
            var frame = pair.Value;
            var remote = frame.Telemetry;

            if (!_activePhysicalRemotePlayers.Contains(playerId) ||
                !remote.IsInGame ||
                now - remote.Timestamp > TimeSpan.FromSeconds(3d) ||
                remote.LocalX is not double remoteX ||
                remote.LocalY is not double remoteY ||
                remote.LocalZ is not double remoteZ)
            {
                HideRemoteNameplate(playerId);
                continue;
            }

            var dx = remoteX - localX;
            var dy = remoteY - localY;
            var dz = remoteZ - localZ;
            var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (!double.IsFinite(distance) || distance > RemoteNameplateMaxDistanceMeters)
            {
                HideRemoteNameplate(playerId);
                continue;
            }

            // OMSI/D3D uses Y as the vertical axis. Keep the label directly
            // above the physical bus instead of offsetting it along road Z.
            var point = new Vector3(
                (float)remoteX,
                (float)(remoteY + RemoteBusNameHeightMeters),
                (float)remoteZ);

            if (!TryProjectToViewport(
                    point,
                    projection.Value,
                    viewport,
                    out var screenX,
                    out var screenY))
            {
                HideRemoteNameplate(playerId);
                continue;
            }

            var motion = GetOrCreateNameplateMotion(playerId);
            motion.SetTarget(screenX, screenY);
            var position = motion.Step(0.34d);

            var plate = GetOrCreateRemoteNameplate(playerId, frame.Player.DisplayName);
            UpdateRemoteNameplateText(plate, frame.Player.DisplayName);
            plate.Opacity = Math.Clamp(1d - Math.Max(0d, distance - 80d) / 540d, 0.55d, 1d);
            plate.Visibility = Visibility.Visible;
            plate.Measure(new Size(190d, 60d));

            var desired = plate.DesiredSize;
            var maxLeft = Math.Max(viewport.Left, viewport.Right - desired.Width);
            var maxTop = Math.Max(viewport.Top, viewport.Bottom - desired.Height);
            Canvas.SetLeft(
                plate,
                Math.Clamp(position.X - desired.Width / 2d, viewport.Left, maxLeft));
            Canvas.SetTop(
                plate,
                Math.Clamp(position.Y - desired.Height, viewport.Top, maxTop));

            visible.Add(playerId);
        }

        foreach (var playerId in _remote3DNameplates.Keys.ToArray())
        {
            if (!visible.Contains(playerId))
            {
                HideRemoteNameplate(playerId);
            }
        }
    }

    private Border GetOrCreateRemoteNameplate(string playerId, string displayName)
    {
        if (_remote3DNameplates.TryGetValue(playerId, out var existing))
        {
            return existing;
        }

        var name = new TextBlock
        {
            Text = NormalizeRemoteDisplayName(displayName),
            Foreground = Brushes.White,
            FontSize = 12d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 170d,
            Tag = "remote-3d-name"
        };

        var plate = new Border
        {
            Padding = new Thickness(8d, 4d, 8d, 4d),
            Background = new SolidColorBrush(Color.FromArgb(205, 4, 15, 23)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(220, 113, 198, 255)),
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(6d),
            Child = name,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };

        Panel.SetZIndex(plate, 610);
        RemotePlayerNameCanvas.Children.Add(plate);
        _remote3DNameplates[playerId] = plate;
        return plate;
    }

    private static void UpdateRemoteNameplateText(Border plate, string displayName)
    {
        if (plate.Child is TextBlock text)
        {
            text.Text = NormalizeRemoteDisplayName(displayName);
        }
    }

    private NameplateMotionState GetOrCreateNameplateMotion(string playerId)
    {
        if (_remote3DNameplateMotion.TryGetValue(playerId, out var state))
        {
            return state;
        }

        state = new NameplateMotionState();
        _remote3DNameplateMotion[playerId] = state;
        return state;
    }

    private void RemoveRemoteNameplate(string playerId)
    {
        _activePhysicalRemotePlayers.Remove(playerId);
        _remote3DNameplateMotion.Remove(playerId);
        if (_remote3DNameplates.Remove(playerId, out var plate))
        {
            RemotePlayerNameCanvas.Children.Remove(plate);
        }
    }

    private void ClearRemoteNameplates()
    {
        foreach (var plate in _remote3DNameplates.Values)
        {
            RemotePlayerNameCanvas.Children.Remove(plate);
        }

        _remote3DNameplates.Clear();
        _remote3DNameplateMotion.Clear();
        _activePhysicalRemotePlayers.Clear();
    }

    private void HideRemoteNameplate(string playerId)
    {
        if (_remote3DNameplates.TryGetValue(playerId, out var plate))
        {
            plate.Visibility = Visibility.Collapsed;
        }
    }

    private void HideAllRemoteNameplates()
    {
        foreach (var plate in _remote3DNameplates.Values)
        {
            plate.Visibility = Visibility.Collapsed;
        }
    }

    private static bool TryProjectToViewport(
        Vector3 world,
        OmsiCameraProjectionSnapshot camera,
        ClientViewport viewport,
        out double screenX,
        out double screenY)
    {
        screenX = 0d;
        screenY = 0d;

        var viewPosition = Vector4.Transform(new Vector4(world, 1f), camera.View);
        var clip = Vector4.Transform(viewPosition, camera.Projection);

        if (!float.IsFinite(clip.X) ||
            !float.IsFinite(clip.Y) ||
            !float.IsFinite(clip.Z) ||
            !float.IsFinite(clip.W) ||
            clip.W <= 0.001f)
        {
            return false;
        }

        var ndcX = clip.X / clip.W;
        var ndcY = clip.Y / clip.W;
        var ndcZ = clip.Z / clip.W;
        if (!float.IsFinite(ndcX) ||
            !float.IsFinite(ndcY) ||
            !float.IsFinite(ndcZ) ||
            ndcZ < -0.02f ||
            ndcZ > 1.02f ||
            ndcX < -1.10f ||
            ndcX > 1.10f ||
            ndcY < -1.10f ||
            ndcY > 1.10f)
        {
            return false;
        }

        screenX = viewport.Left + (ndcX + 1d) * 0.5d * viewport.Width;
        screenY = viewport.Top + (1d - ndcY) * 0.5d * viewport.Height;
        return double.IsFinite(screenX) && double.IsFinite(screenY);
    }

    private bool TryGetOmsiClientViewport(out ClientViewport viewport)
    {
        viewport = default;
        if (_omsiWindowHandle == IntPtr.Zero ||
            !GetClientRect(_omsiWindowHandle, out var clientRect) ||
            !GetWindowRect(_omsiWindowHandle, out var windowRect))
        {
            return false;
        }

        var origin = new NativePoint();
        if (!ClientToScreen(_omsiWindowHandle, ref origin))
        {
            return false;
        }

        var dpi = GetDpiForWindow(_omsiWindowHandle);
        var scale = dpi > 0 ? 96d / dpi : 1d;
        var width = Math.Max(1d, (clientRect.Right - clientRect.Left) * scale);
        var height = Math.Max(1d, (clientRect.Bottom - clientRect.Top) * scale);
        viewport = new ClientViewport(
            (origin.X - windowRect.Left) * scale,
            (origin.Y - windowRect.Top) * scale,
            width,
            height);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint point);

    private readonly record struct ClientViewport(double Left, double Top, double Width, double Height)
    {
        public double Right => Left + Width;
        public double Bottom => Top + Height;
    }

    private sealed class NameplateMotionState
    {
        private bool _initialized;
        private double _x;
        private double _y;
        private double _targetX;
        private double _targetY;

        public void SetTarget(double x, double y)
        {
            if (!_initialized)
            {
                _initialized = true;
                _x = _targetX = x;
                _y = _targetY = y;
                return;
            }

            _targetX = x;
            _targetY = y;
        }

        public (double X, double Y) Step(double alpha)
        {
            alpha = Math.Clamp(alpha, 0d, 1d);
            _x += (_targetX - _x) * alpha;
            _y += (_targetY - _y) * alpha;
            return (_x, _y);
        }
    }
}
