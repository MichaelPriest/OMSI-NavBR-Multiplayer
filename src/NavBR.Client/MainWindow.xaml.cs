using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Maps;
using NavBR.Client.Omsi;
using NavBR.Client.Telemetry;
using NavBR.Shared.Telemetry;

namespace NavBR.Client;

public partial class MainWindow : Window
{
    private readonly OmsiProcessDetector _detector = new();
    private readonly Omsi23004TelemetryProvider _telemetryProvider = new();
    private readonly OmsiMapCatalog _mapCatalog = new();
    private readonly DispatcherTimer _telemetryTimer;

    private bool _languageSelectorReady;
    private OmsiProcessInfo? _currentOmsi;
    private VehicleTelemetry? _lastTelemetry;
    private IReadOnlyList<OmsiMapInfo> _installedMaps = Array.Empty<OmsiMapInfo>();
    private string? _loadedRoadmapPath;
    private OmsiMapLayout? _loadedRoadmapLayout;
    private BitmapImage? _loadedRoadmapBitmap;
    private string _statusKey = "StatusSearching";
    private string _telemetryStatusKey = "TelemetryWaiting";

    public MainWindow()
    {
        InitializeComponent();

        _telemetryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _telemetryTimer.Tick += (_, _) => PollTelemetry();

        ConfigureLanguageSelector();
        ApplyLocalization();
        RenderCurrentState();

        Loaded += async (_, _) => await RefreshOmsiStatusAsync();
        Closed += (_, _) => _telemetryProvider.Dispose();
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

        MilestoneHeadingText.Text = LocalizationService.Get("FirstMilestoneHeading");
        MilestoneBodyText.Text = LocalizationService.Get("FirstMilestoneBody");
        PhaseFooterText.Text = LocalizationService.Get("PhaseFooter");
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshOmsiStatusAsync();
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
        var roadmapCount = _installedMaps.Count(map => !string.IsNullOrWhiteSpace(map.RoadmapPath));
        GpsStatusText.Text = string.Format(
            culture,
            LocalizationService.Get("GpsMapsSummary"),
            _installedMaps.Count,
            roadmapCount);

        const int visibleMapLimit = 10;
        var rows = _installedMaps
            .Take(visibleMapLimit)
            .Select(map =>
            {
                var roadmapStatus = LocalizationService.Get(
                    string.IsNullOrWhiteSpace(map.RoadmapPath)
                        ? "GpsRoadmapMissing"
                        : "GpsRoadmapReady");

                return $"{map.DisplayName} [{map.FolderName}] | {map.TileCount} | {roadmapStatus}";
            })
            .ToList();

        if (_installedMaps.Count > visibleMapLimit)
        {
            rows.Add(string.Format(
                culture,
                LocalizationService.Get("GpsMoreMaps"),
                _installedMaps.Count - visibleMapLimit));
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

        RoadmapViewbox.Visibility = Visibility.Visible;

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
            return;
        }

        var markerSize = VehicleMarker.Width;
        Canvas.SetLeft(VehicleMarker, pixelX - markerSize / 2d);
        Canvas.SetTop(VehicleMarker, pixelY - markerSize / 2d);
        VehicleMarker.Visibility = Visibility.Visible;
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

            RoadmapCanvas.Width = bitmap.PixelWidth;
            RoadmapCanvas.Height = bitmap.PixelHeight;
            RoadmapImage.Width = bitmap.PixelWidth;
            RoadmapImage.Height = bitmap.PixelHeight;
            RoadmapImage.Source = bitmap;

            var markerSize = Math.Clamp(bitmap.PixelWidth * 0.015d, 28d, 90d);
            VehicleMarker.Width = markerSize;
            VehicleMarker.Height = markerSize;
            VehicleMarker.StrokeThickness = Math.Max(3d, markerSize * 0.12d);
            return true;
        }
        catch
        {
            ClearRoadmap();
            return false;
        }
    }

    private void HideRoadmap()
    {
        RoadmapViewbox.Visibility = Visibility.Collapsed;
        VehicleMarker.Visibility = Visibility.Collapsed;
    }

    private void ClearRoadmap()
    {
        _loadedRoadmapPath = null;
        _loadedRoadmapLayout = null;
        _loadedRoadmapBitmap = null;
        RoadmapImage.Source = null;
        RoadmapCanvas.Width = 0;
        RoadmapCanvas.Height = 0;
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
