using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Client.Driver;
using NavBR.Client.Omsi;
using NavBR.Client.Hardware;
using NavBR.Client.Telemetry;
using NavBR.Shared.Telemetry;

namespace NavBR.Client;

public partial class MainWindow : Window
{
    private const bool RetiredWpfVisualsEnabled = false;
    private const double MinimumRoadmapZoom = 0.02d;
    private const double MaximumRoadmapZoom = 8d;

    private readonly OmsiProcessDetector _detector = new();
    private readonly Omsi23004TelemetryProvider _telemetryProvider = new();
    private readonly OmsiMapCatalog _mapCatalog = new();
    private readonly DispatcherTimer _telemetryTimer;
    private readonly DriverStatisticsService _driverStatisticsService;

    private bool _languageSelectorReady;
    private OmsiProcessInfo? _currentOmsi;
    private VehicleTelemetry? _lastTelemetry;
    private IReadOnlyList<OmsiMapInfo> _installedMaps = Array.Empty<OmsiMapInfo>();
    private string? _loadedRoadmapPath;
    private OmsiMapLayout? _loadedRoadmapLayout;
    private BitmapImage? _loadedRoadmapBitmap;
    private double _roadmapZoom = 1d;
    private bool _roadmapZoomInitialized;
    private double? _lastVehiclePixelX;
    private double? _lastVehiclePixelY;
    private bool _isPanning;
    private Point _panStartPoint;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;
    private string _statusKey = "StatusSearching";
    private string _telemetryStatusKey = "TelemetryWaiting";
    private bool _nativeRuntimeStarted;

    public MainWindow()
    {
        InitializeComponent();

        _telemetryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _telemetryTimer.Tick += (_, _) => PollTelemetry();

        // Driver statistics are a native background service, not a WPF-screen
        // concern. Start them directly so the retired profile installer no
        // longer needs to run on the hidden MainWindow host.
        _driverStatisticsService = new DriverStatisticsService(() => _lastTelemetry);
        _driverStatisticsService.Start();

        ConfigureLanguageSelector();
        ApplyLocalization();
        RenderCurrentState();

        Closed += (_, _) =>
        {
            _driverStatisticsService.Dispose();
            HardwareCockpitBridgeController.Shared.Dispose();
            _telemetryProvider.Dispose();
        };
    }

    private void ConfigureLanguageSelector()
    {
        LanguageComboBox.ItemsSource = LocalizationService.SupportedLanguages;
        LanguageComboBox.DisplayMemberPath = nameof(SupportedLanguage.DisplayName);
        LanguageComboBox.SelectedValuePath = nameof(SupportedLanguage.CultureName);
        LanguageComboBox.SelectedValue = LocalizationService.CurrentCulture.Name;
        _languageSelectorReady = true;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_languageSelectorReady || LanguageComboBox.SelectedValue is not string cultureName)
        {
            return;
        }

        if (!string.Equals(
                cultureName,
                LocalizationService.CurrentCulture.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            LocalizationService.SetCulture(cultureName);
        }

        ApplyLocalization();
        RenderCurrentState();
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.Get("AppTitle");
        TaglineText.Text = LocalizationService.Get("Tagline");
        LanguageLabelText.Text = LocalizationService.Get("LanguageLabel");
        StatusHeadingText.Text = LocalizationService.Get("StatusHeading");
        RefreshButton.Content = LocalizationService.Get("RedetectButton");

        TelemetryHeadingText.Text = LocalizationService.Get("TelemetryHeading");
        MapCaptionText.Text = LocalizationService.Get("MapLabel");
        PositionCaptionText.Text = LocalizationService.Get("PositionLabel");
        HeadingCaptionText.Text = LocalizationService.Get("HeadingLabel");
        SpeedCaptionText.Text = LocalizationService.Get("SpeedLabel");
        GpsHeadingText.Text = LocalizationService.Get("GpsHeading");
        ZoomOutButton.Content = LocalizationService.Get("GpsZoomOut");
        ZoomInButton.Content = LocalizationService.Get("GpsZoomIn");
        FitMapButton.Content = LocalizationService.Get("GpsFitMap");
        FollowButton.Content = LocalizationService.Get("GpsFollowBus");
        TopmostButton.Content = LocalizationService.Get("GpsAlwaysOnTop");

        MilestoneHeadingText.Text = LocalizationService.Get("FirstMilestoneHeading");
        MilestoneBodyText.Text = LocalizationService.Get("FirstMilestoneBody");
        PhaseFooterText.Text = LocalizationService.Get("PhaseFooter");
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshOmsiStatusAsync();
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeRoadmapZoom(0.8d);
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeRoadmapZoom(1.25d);
    }

    private void FitMapButton_Click(object sender, RoutedEventArgs e)
    {
        FitRoadmapToViewport();
    }

    private void FollowButton_Changed(object sender, RoutedEventArgs e)
    {
        if (FollowButton.IsChecked == true)
        {
            CenterOnVehicle();
        }
    }

    private void TopmostButton_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = TopmostButton.IsChecked == true;
    }

    internal void StartNativeRuntimeForReact()
    {
        if (_nativeRuntimeStarted)
        {
            return;
        }

        _nativeRuntimeStarted = true;

        // The historical WPF MainWindow is an invisible service host in the
        // React shell. Its Loaded events therefore never fire reliably, so the
        // base HUD must be started explicitly with the native runtime instead
        // of depending on MultiplayerButton_Loaded.
        HookHudLifetimeToMainWindow();
        EnsureHudOverlay();

        _ = RefreshOmsiStatusAsync();
    }

    private async Task RefreshOmsiStatusAsync()
    {
        _telemetryTimer.Stop();
        _telemetryProvider.Dispose();
        _lastTelemetry = null;
        _currentOmsi = null;
        _installedMaps = Array.Empty<OmsiMapInfo>();
        ClearRoadmap();

        _statusKey = "StatusSearching";
        _telemetryStatusKey = "TelemetryWaiting";
        RenderCurrentState();

        var instances = _detector.FindRunningInstances();
        if (instances.Count == 0)
        {
            _statusKey = "OmsiNotRunning";
            _telemetryStatusKey = "TelemetryWaiting";
            RenderCurrentState();
            return;
        }

        _currentOmsi = instances[0];
        _installedMaps = _mapCatalog.Discover(_currentOmsi.InstallDirectory);

        if (!_currentOmsi.IsOmsi23004)
        {
            _statusKey = "UnsupportedVersion";
            _telemetryStatusKey = "UnsupportedVersion";
            RenderCurrentState();
            return;
        }

        _statusKey = "TelemetryConnecting";
        _telemetryStatusKey = "TelemetryConnecting";
        RenderCurrentState();

        var attached = await _telemetryProvider.AttachAsync(_currentOmsi);
        if (!attached)
        {
            _statusKey = ErrorKey(_telemetryProvider.LastErrorCode);
            _telemetryStatusKey = _statusKey;
            RenderCurrentState();
            return;
        }

        _statusKey = "TelemetryConnected";
        _telemetryStatusKey = "TelemetryWaiting";
        PollTelemetry();
        _telemetryTimer.Start();
    }

    private void PollTelemetry()
    {
        if (!_telemetryProvider.IsAttached)
        {
            _telemetryTimer.Stop();
            return;
        }

        var telemetry = _telemetryProvider.Read("local");
        if (telemetry is not null)
        {
            _lastTelemetry = telemetry;
            HardwareCockpitBridgeController.Shared.PublishTelemetry(GetCurrentTelemetryForAlpha11());
            _statusKey = "TelemetryConnected";
            _telemetryStatusKey = telemetry.IsInGame
                ? "TelemetryConnected"
                : "TelemetryReadError";
        }
        else
        {
            _telemetryStatusKey = ErrorKey(_telemetryProvider.LastErrorCode);

            if (_telemetryProvider.LastErrorCode == TelemetryErrorCode.ProcessExited)
            {
                _telemetryTimer.Stop();
                _statusKey = "OmsiNotRunning";
                ClearRoadmap();
            }
        }

        RenderCurrentState();
    }

    private void RenderCurrentState()
    {
        if (!RetiredWpfVisualsEnabled)
        {
            return;
        }

        StatusText.Text = LocalizationService.Get(_statusKey);
        TelemetryStateText.Text = LocalizationService.Get(_telemetryStatusKey);
        RenderGpsState();

        if (_currentOmsi is null)
        {
            InstallPathText.Text = LocalizationService.Get("OpenOmsiInstruction");
            ProcessDetailsText.Text = LocalizationService.Get("NoProcessFound");
        }
        else
        {
            InstallPathText.Text = _currentOmsi.InstallDirectory;
            ProcessDetailsText.Text =
                $"{LocalizationService.Get("PidLabel")}: {_currentOmsi.ProcessId}\n" +
                $"{LocalizationService.Get("VersionLabel")}: {_currentOmsi.FileVersion}\n" +
                $"{LocalizationService.Get("HashLabel")}: {_currentOmsi.Sha256}\n" +
                $"{LocalizationService.Get("ExecutableLabel")}: {_currentOmsi.ExecutablePath}";
        }

        if (_lastTelemetry is null)
        {
            var unavailable = LocalizationService.Get("NotAvailable");
            MapValueText.Text = unavailable;
            PositionValueText.Text = unavailable;
            HeadingValueText.Text = unavailable;
            SpeedValueText.Text = unavailable;
            return;
        }

        var culture = LocalizationService.CurrentCulture;
        MapValueText.Text = string.IsNullOrWhiteSpace(_lastTelemetry.MapName)
            ? LocalizationService.Get("NotAvailable")
            : _lastTelemetry.MapName;

        PositionValueText.Text = string.Format(
            culture,
            "X {0:F2}   Y {1:F2}   Z {2:F2}",
            _lastTelemetry.X,
            _lastTelemetry.Y,
            _lastTelemetry.Z);

        HeadingValueText.Text = string.Format(
            culture,
            "{0:F1}°",
            _lastTelemetry.HeadingDegrees);

        SpeedValueText.Text = string.Format(
            culture,
            "{0:F1} km/h",
            _lastTelemetry.SpeedKph);
    }

    private void RenderGpsState()
    {
        if (_currentOmsi is null)
        {
            GpsStatusText.Text = LocalizationService.Get("GpsWaitingForOmsi");
            InstalledMapsText.Text = string.Empty;
            HideRoadmap();
            return;
        }

        if (_installedMaps.Count == 0)
        {
            GpsStatusText.Text = LocalizationService.Get("GpsNoMaps");
            InstalledMapsText.Text = string.Empty;
            HideRoadmap();
            return;
        }

        var culture = LocalizationService.CurrentCulture;
        var readyMaps = _installedMaps
            .Where(map => !string.IsNullOrWhiteSpace(map.RoadmapPath))
            .OrderBy(map => map.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var missingMaps = _installedMaps
            .Where(map => string.IsNullOrWhiteSpace(map.RoadmapPath))
            .OrderBy(map => map.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        GpsStatusText.Text = string.Format(
            culture,
            LocalizationService.Get("GpsMapsSummary"),
            _installedMaps.Count,
            readyMaps.Length);

        var readyLabel = LocalizationService.Get("GpsRoadmapReady").ToUpper(culture);
        var missingLabel = LocalizationService.Get("GpsRoadmapMissing").ToUpper(culture);
        var rows = new List<string>
        {
            $"✓ {readyLabel} ({readyMaps.Length})"
        };

        if (readyMaps.Length == 0)
        {
            rows.Add("  —");
        }
        else
        {
            foreach (var map in readyMaps)
            {
                var roadmapFile = Path.GetFileName(map.RoadmapPath);
                rows.Add($"  ✓ {map.DisplayName} [{map.FolderName}] | {map.TileCount} tiles | {roadmapFile}");
            }
        }

        rows.Add(string.Empty);
        rows.Add($"⚠ {missingLabel} ({missingMaps.Length})");

        if (missingMaps.Length == 0)
        {
            rows.Add("  —");
        }
        else
        {
            foreach (var map in missingMaps)
            {
                rows.Add($"  ⚠ {map.DisplayName} [{map.FolderName}] | {map.TileCount} tiles");
            }
        }

        InstalledMapsText.Text = string.Join(Environment.NewLine, rows);
        RenderRoadmap();
    }

    private void RenderRoadmap()
    {
        var telemetry = _lastTelemetry;
        if (telemetry is null || string.IsNullOrWhiteSpace(telemetry.MapName))
        {
            HideRoadmap();
            return;
        }

        var map = FindActiveMap(telemetry.MapName);
        if (map is null || string.IsNullOrWhiteSpace(map.RoadmapPath))
        {
            HideRoadmap();
            return;
        }

        if (!string.Equals(_loadedRoadmapPath, map.RoadmapPath, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryLoadRoadmap(map))
            {
                HideRoadmap();
                return;
            }
        }

        RoadmapScrollViewer.Visibility = Visibility.Visible;

        if (_loadedRoadmapBitmap is null ||
            _loadedRoadmapLayout is null ||
            telemetry.GridX is not int gridX ||
            telemetry.GridY is not int gridY ||
            telemetry.TileX is not double tileX ||
            telemetry.TileY is not double tileY ||
            !RoadmapTransform.TryToPixel(
                _loadedRoadmapLayout,
                _loadedRoadmapBitmap.PixelWidth,
                _loadedRoadmapBitmap.PixelHeight,
                gridX,
                gridY,
                tileX,
                tileY,
                out var pixelX,
                out var pixelY) ||
            pixelX < 0 || pixelX > _loadedRoadmapBitmap.PixelWidth ||
            pixelY < 0 || pixelY > _loadedRoadmapBitmap.PixelHeight)
        {
            VehicleMarker.Visibility = Visibility.Collapsed;
            _lastVehiclePixelX = null;
            _lastVehiclePixelY = null;
            return;
        }

        var markerSize = VehicleMarker.Width;
        Canvas.SetLeft(VehicleMarker, pixelX - markerSize / 2d);
        Canvas.SetTop(VehicleMarker, pixelY - markerSize / 2d);
        VehicleHeadingTransform.Angle = telemetry.HeadingDegrees;
        VehicleMarker.Visibility = Visibility.Visible;
        _lastVehiclePixelX = pixelX;
        _lastVehiclePixelY = pixelY;

        if (!_roadmapZoomInitialized)
        {
            _roadmapZoomInitialized = true;
            Dispatcher.BeginInvoke(
                new Action(FitRoadmapToViewport),
                DispatcherPriority.Loaded);
        }
        else if (FollowButton.IsChecked == true && !_isPanning)
        {
            CenterOnVehicle();
        }
    }

    private OmsiMapInfo? FindActiveMap(string activeMapName)
    {
        var exact = _installedMaps.FirstOrDefault(map =>
            string.Equals(map.DisplayName, activeMapName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(map.FolderName, activeMapName, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact;
        }

        var normalizedActive = NormalizeMapName(activeMapName);
        return _installedMaps.FirstOrDefault(map =>
            NormalizeMapName(map.DisplayName) == normalizedActive ||
            NormalizeMapName(map.FolderName) == normalizedActive);
    }

    private bool TryLoadRoadmap(OmsiMapInfo map)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(map.RoadmapPath!, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            _loadedRoadmapPath = map.RoadmapPath;
            _loadedRoadmapBitmap = bitmap;
            _loadedRoadmapLayout = OmsiMapLayoutReader.TryRead(map.GlobalConfigPath);
            _roadmapZoom = 1d;
            _roadmapZoomInitialized = false;
            _lastVehiclePixelX = null;
            _lastVehiclePixelY = null;

            RoadmapCanvas.Width = bitmap.PixelWidth;
            RoadmapCanvas.Height = bitmap.PixelHeight;
            RoadmapImage.Width = bitmap.PixelWidth;
            RoadmapImage.Height = bitmap.PixelHeight;
            RoadmapImage.Source = bitmap;

            var markerSize = Math.Clamp(bitmap.PixelWidth * 0.02d, 42d, 120d);
            VehicleMarker.Width = markerSize;
            VehicleMarker.Height = markerSize;

            ApplyRoadmapZoom(_roadmapZoom);
            RoadmapScrollViewer.Visibility = Visibility.Visible;
            return true;
        }
        catch
        {
            ClearRoadmap();
            return false;
        }
    }

    private void ChangeRoadmapZoom(double multiplier, Point? anchor = null)
    {
        if (_loadedRoadmapBitmap is null || RoadmapScrollViewer.Visibility != Visibility.Visible)
        {
            return;
        }

        var oldZoom = _roadmapZoom;
        var newZoom = Math.Clamp(oldZoom * multiplier, MinimumRoadmapZoom, MaximumRoadmapZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.000001d)
        {
            return;
        }

        var oldHorizontalOffset = RoadmapScrollViewer.HorizontalOffset;
        var oldVerticalOffset = RoadmapScrollViewer.VerticalOffset;
        var anchorPoint = anchor ?? new Point(
            RoadmapScrollViewer.ViewportWidth / 2d,
            RoadmapScrollViewer.ViewportHeight / 2d);

        ApplyRoadmapZoom(newZoom);

        Dispatcher.BeginInvoke(() =>
        {
            if (FollowButton.IsChecked == true)
            {
                CenterOnVehicle();
                return;
            }

            var ratio = newZoom / oldZoom;
            var horizontalOffset = (oldHorizontalOffset + anchorPoint.X) * ratio - anchorPoint.X;
            var verticalOffset = (oldVerticalOffset + anchorPoint.Y) * ratio - anchorPoint.Y;
            RoadmapScrollViewer.ScrollToHorizontalOffset(Math.Max(0d, horizontalOffset));
            RoadmapScrollViewer.ScrollToVerticalOffset(Math.Max(0d, verticalOffset));
        }, DispatcherPriority.Loaded);
    }

    private void ApplyRoadmapZoom(double zoom)
    {
        var bitmap = _loadedRoadmapBitmap;
        if (bitmap is null)
        {
            return;
        }

        _roadmapZoom = Math.Clamp(zoom, MinimumRoadmapZoom, MaximumRoadmapZoom);
        RoadmapViewbox.Width = bitmap.PixelWidth * _roadmapZoom;
        RoadmapViewbox.Height = bitmap.PixelHeight * _roadmapZoom;
    }

    private void FitRoadmapToViewport()
    {
        var bitmap = _loadedRoadmapBitmap;
        if (bitmap is null || RoadmapScrollViewer.Visibility != Visibility.Visible)
        {
            return;
        }

        var viewportWidth = RoadmapScrollViewer.ViewportWidth;
        var viewportHeight = RoadmapScrollViewer.ViewportHeight;

        if (viewportWidth <= 1d)
        {
            viewportWidth = Math.Max(1d, RoadmapScrollViewer.ActualWidth - 20d);
        }

        if (viewportHeight <= 1d)
        {
            viewportHeight = Math.Max(1d, RoadmapScrollViewer.ActualHeight - 20d);
        }

        var zoom = Math.Min(
            viewportWidth / bitmap.PixelWidth,
            viewportHeight / bitmap.PixelHeight);

        ApplyRoadmapZoom(Math.Clamp(zoom, MinimumRoadmapZoom, MaximumRoadmapZoom));
        _roadmapZoomInitialized = true;

        Dispatcher.BeginInvoke(() =>
        {
            if (FollowButton.IsChecked == true)
            {
                CenterOnVehicle();
            }
            else
            {
                RoadmapScrollViewer.ScrollToHome();
            }
        }, DispatcherPriority.Loaded);
    }

    private void CenterOnVehicle()
    {
        if (_lastVehiclePixelX is not double pixelX ||
            _lastVehiclePixelY is not double pixelY ||
            RoadmapScrollViewer.Visibility != Visibility.Visible)
        {
            return;
        }

        var scaledX = pixelX * _roadmapZoom;
        var scaledY = pixelY * _roadmapZoom;
        var horizontalOffset = scaledX - RoadmapScrollViewer.ViewportWidth / 2d;
        var verticalOffset = scaledY - RoadmapScrollViewer.ViewportHeight / 2d;

        RoadmapScrollViewer.ScrollToHorizontalOffset(Math.Max(0d, horizontalOffset));
        RoadmapScrollViewer.ScrollToVerticalOffset(Math.Max(0d, verticalOffset));
    }

    private void RoadmapScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_loadedRoadmapBitmap is null)
        {
            return;
        }

        var anchor = e.GetPosition(RoadmapScrollViewer);
        ChangeRoadmapZoom(e.Delta > 0 ? 1.2d : 1d / 1.2d, anchor);
        e.Handled = true;
    }

    private void RoadmapScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_loadedRoadmapBitmap is null)
        {
            return;
        }

        _isPanning = true;
        _panStartPoint = e.GetPosition(RoadmapScrollViewer);
        _panStartHorizontalOffset = RoadmapScrollViewer.HorizontalOffset;
        _panStartVerticalOffset = RoadmapScrollViewer.VerticalOffset;
        FollowButton.IsChecked = false;
        RoadmapScrollViewer.Cursor = Cursors.Hand;
        RoadmapScrollViewer.CaptureMouse();
        e.Handled = true;
    }

    private void RoadmapScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPoint = e.GetPosition(RoadmapScrollViewer);
        var delta = currentPoint - _panStartPoint;
        RoadmapScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - delta.X);
        RoadmapScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - delta.Y);
        e.Handled = true;
    }

    private void RoadmapScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        RoadmapScrollViewer.ReleaseMouseCapture();
        RoadmapScrollViewer.Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    private void HideRoadmap()
    {
        RoadmapScrollViewer.Visibility = Visibility.Collapsed;
        VehicleMarker.Visibility = Visibility.Collapsed;
    }

    private void ClearRoadmap()
    {
        _loadedRoadmapPath = null;
        _loadedRoadmapLayout = null;
        _loadedRoadmapBitmap = null;
        _lastVehiclePixelX = null;
        _lastVehiclePixelY = null;
        _roadmapZoom = 1d;
        _roadmapZoomInitialized = false;
        _isPanning = false;

        if (!RetiredWpfVisualsEnabled)
        {
            return;
        }

        RoadmapImage.Source = null;
        RoadmapCanvas.Width = 0;
        RoadmapCanvas.Height = 0;
        RoadmapViewbox.Width = 0;
        RoadmapViewbox.Height = 0;
        HideRoadmap();
    }

    private static string NormalizeMapName(string value) =>
        new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

    private static string ErrorKey(TelemetryErrorCode errorCode) => errorCode switch
    {
        TelemetryErrorCode.UnsupportedVersion => "UnsupportedVersion",
        TelemetryErrorCode.AttachFailed => "TelemetryAttachFailed",
        TelemetryErrorCode.NoVehicle => "TelemetryReadError",
        TelemetryErrorCode.ReadFailed => "TelemetryReadError",
        TelemetryErrorCode.ProcessExited => "OmsiNotRunning",
        _ => "TelemetryWaiting"
    };
}
