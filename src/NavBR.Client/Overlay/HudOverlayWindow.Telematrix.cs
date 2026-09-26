using System.Globalization;
using System.Windows;
using System.Windows.Media;
using NavBR.Client.Multiplayer;
using NavBR.Client.PluginBridge;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private string? _telematrixPrimaryTerminus;
    private string? _telematrixSecondaryTerminus;
    private string? _telematrixCandidateTerminus;
    private DateTimeOffset _telematrixCandidateSinceUtc = DateTimeOffset.MinValue;
    private string _telematrixDirection = "?";

    private void RefreshTelematrixPanel()
    {
        var settings = MultiplayerSettingsStore.Load();
        ApplyTelematrixSettings(settings);

        if (!settings.TelematrixWidgetEnabled)
        {
            return;
        }

        var telemetry = _localTelemetry;
        var operational = LocalOmsiOperationalSnapshotStore.Latest;
        var operationalFresh =
            operational is not null &&
            DateTimeOffset.UtcNow - operational.CapturedAtUtc <= TimeSpan.FromSeconds(2);

        TelematrixLineText.Text =
            settings.TelematrixAutoDirection
                ? FirstNonBlank(
                    telemetry?.Line,
                    operationalFresh ? operational?.IbisLineCourse : null,
                    settings.TelematrixManualLine) ?? "—"
                : FirstNonBlank(
                    settings.TelematrixManualLine,
                    telemetry?.Line,
                    operationalFresh ? operational?.IbisLineCourse : null) ?? "—";

        if (settings.TelematrixAutoDirection)
        {
            var terminus = FirstNonBlank(
                operationalFresh ? operational?.IbisTerminusName : null,
                telemetry?.DestinationName);
            UpdateTelematrixDirection(terminus);
        }
        else
        {
            _telematrixDirection =
                string.Equals(
                    settings.TelematrixManualDirection,
                    "TS",
                    StringComparison.OrdinalIgnoreCase)
                    ? "TS"
                    : "TP";
        }

        TelematrixDirectionText.Text = _telematrixDirection;

        TelematrixSpeedText.Text =
            telemetry is null
                ? "—"
                : Math.Clamp(telemetry.SpeedKph, 0d, 999d)
                    .ToString("F0", CultureInfo.CurrentCulture);

        TelematrixTempText.Text =
            operationalFresh &&
            operational?.CabinTemperatureC is double temperature &&
            double.IsFinite(temperature)
                ? $"{temperature:F1} °C"
                : "— °C";

        TelematrixPassengersText.Text =
            operationalFresh &&
            operational?.PassengerCount is int passengers &&
            passengers >= 0
                ? passengers.ToString(CultureInfo.CurrentCulture)
                : "—";

        var scheduleActive =
            operationalFresh ? operational?.ScheduleActive : null;
        TelematrixDelayText.Text = FormatTelematrixDelay(
            scheduleActive,
            operationalFresh ? operational : null,
            telemetry?.DelaySeconds);

        if (operationalFresh &&
            TryFormatSimulationClock(operational, out var clock))
        {
            TelematrixClockText.Text = clock;
        }
        else
        {
            TelematrixClockText.Text = "--:--";
        }

        TelematrixDateText.Text =
            operationalFresh &&
            operational?.SimulationDay is int day &&
            operational.SimulationMonth is int month &&
            operational.SimulationYear is int year &&
            day is >= 1 and <= 31 &&
            month is >= 1 and <= 12 &&
            year is >= 1900 and <= 3000
                ? $"{day:00}/{month:00}/{year:0000}"
                : "--/--/----";

        var paused =
            operationalFresh && operational?.SimulationPaused == true;
        TelematrixMonitorText.Text = paused
            ? "SIMULAÇÃO PAUSADA"
            : scheduleActive == false
                ? "SEM HORÁRIO ATIVO"
                : "Monitoramento OMSI local";
    }

    private void ApplyTelematrixSettings(MultiplayerSettings settings)
    {
        TelematrixPanel.Visibility =
            settings.TelematrixWidgetEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;

        var (background, border, accent, soft) =
            settings.TelematrixTheme switch
            {
                1 => (
                    Color.FromArgb(236, 28, 18, 6),
                    Color.FromArgb(150, 255, 166, 41),
                    Color.FromRgb(255, 176, 54),
                    Color.FromArgb(50, 255, 166, 41)),
                2 => (
                    Color.FromArgb(236, 7, 19, 31),
                    Color.FromArgb(150, 119, 210, 255),
                    Color.FromRgb(145, 222, 255),
                    Color.FromArgb(50, 119, 210, 255)),
                _ => (
                    Color.FromArgb(236, 10, 23, 26),
                    Color.FromArgb(150, 79, 217, 180),
                    Color.FromRgb(105, 230, 192),
                    Color.FromArgb(50, 79, 217, 180))
            };

        TelematrixPanel.Background = new SolidColorBrush(background);
        TelematrixPanel.BorderBrush = new SolidColorBrush(border);
        TelematrixBrandText.Foreground = new SolidColorBrush(accent);
        TelematrixDirectionText.Foreground = new SolidColorBrush(accent);

        if (TelematrixDirectionText.Parent is Border directionBadge)
        {
            directionBadge.Background = new SolidColorBrush(soft);
        }

        switch (settings.TelematrixSize)
        {
            case 1:
                TelematrixPanel.Height = 108d;
                TelematrixPanel.Padding = new Thickness(14d, 12d, 14d, 12d);
                TelematrixLineText.FontSize = 28d;
                TelematrixSpeedText.FontSize = 26d;
                break;
            case 2:
                TelematrixPanel.Height = 76d;
                TelematrixPanel.Padding = new Thickness(10d, 6d, 10d, 6d);
                TelematrixLineText.FontSize = 20d;
                TelematrixSpeedText.FontSize = 19d;
                break;
            default:
                TelematrixPanel.Height = 92d;
                TelematrixPanel.Padding = new Thickness(12d, 9d, 12d, 9d);
                TelematrixLineText.FontSize = 24d;
                TelematrixSpeedText.FontSize = 22d;
                break;
        }
    }

    private void OpenTelematrixConfig()
    {
        SetLocalPushToTalk(false);
        if (ChatInputPanel.Visibility == Visibility.Visible)
        {
            CloseChatInput();
        }

        var settings = MultiplayerSettingsStore.Load();
        TelematrixConfigLineBox.Text =
            FirstNonBlank(
                settings.TelematrixManualLine,
                _localTelemetry?.Line,
                LocalOmsiOperationalSnapshotStore.Latest?.IbisLineCourse)
            ?? string.Empty;
        TelematrixTpRadio.IsChecked =
            !string.Equals(
                settings.TelematrixManualDirection,
                "TS",
                StringComparison.OrdinalIgnoreCase);
        TelematrixTsRadio.IsChecked =
            string.Equals(
                settings.TelematrixManualDirection,
                "TS",
                StringComparison.OrdinalIgnoreCase);
        TelematrixAutoDirectionCheck.IsChecked =
            settings.TelematrixAutoDirection;

        _chatInteractive = true;
        TelematrixConfigPanel.Visibility = Visibility.Visible;
        SetInteractive(true);
        Show();
        Activate();
        TelematrixConfigLineBox.Focus();
        TelematrixConfigLineBox.SelectAll();
    }

    private void CloseTelematrixConfig(bool save)
    {
        if (save)
        {
            var settings = MultiplayerSettingsStore.Load();
            var line = NormalizeTelematrixText(
                TelematrixConfigLineBox.Text);
            var direction =
                TelematrixTsRadio.IsChecked == true
                    ? "TS"
                    : "TP";

            MultiplayerSettingsStore.Save(
                settings with
                {
                    TelematrixManualLine = line,
                    TelematrixManualDirection = direction,
                    TelematrixAutoDirection =
                        TelematrixAutoDirectionCheck.IsChecked == true
                });
        }

        TelematrixConfigPanel.Visibility = Visibility.Collapsed;
        _chatInteractive = false;
        SetInteractive(false);
        RestoreOmsiFocus();
        RefreshTelematrixPanel();
    }

    private void TelematrixConfigDoneButton_Click(
        object sender,
        RoutedEventArgs e) =>
        CloseTelematrixConfig(save: true);

    private void TelematrixConfigCancelButton_Click(
        object sender,
        RoutedEventArgs e) =>
        CloseTelematrixConfig(save: false);

    private void TelematrixConfigInput_KeyDown(
        object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            CloseTelematrixConfig(save: true);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            CloseTelematrixConfig(save: false);
            e.Handled = true;
        }
    }

    private void ToggleTelematrixWidget()
    {
        var settings = MultiplayerSettingsStore.Load();
        MultiplayerSettingsStore.Save(
            settings with
            {
                TelematrixWidgetEnabled = !settings.TelematrixWidgetEnabled
            });
    }

    private void CycleTelematrixTheme()
    {
        var settings = MultiplayerSettingsStore.Load();
        MultiplayerSettingsStore.Save(
            settings with
            {
                TelematrixTheme = (settings.TelematrixTheme + 1) % 3
            });
    }

    private void CycleTelematrixSize()
    {
        var settings = MultiplayerSettingsStore.Load();
        MultiplayerSettingsStore.Save(
            settings with
            {
                TelematrixSize = (settings.TelematrixSize + 1) % 3
            });
    }

    private void UpdateTelematrixDirection(string? terminus)
    {
        terminus = NormalizeTelematrixText(terminus);
        if (terminus is null)
        {
            _telematrixDirection = "?";
            return;
        }

        if (string.Equals(
                terminus,
                _telematrixPrimaryTerminus,
                StringComparison.OrdinalIgnoreCase))
        {
            _telematrixDirection = "TP";
            _telematrixCandidateTerminus = null;
            return;
        }

        if (string.Equals(
                terminus,
                _telematrixSecondaryTerminus,
                StringComparison.OrdinalIgnoreCase))
        {
            _telematrixDirection = "TS";
            _telematrixCandidateTerminus = null;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (!string.Equals(
                terminus,
                _telematrixCandidateTerminus,
                StringComparison.OrdinalIgnoreCase))
        {
            _telematrixCandidateTerminus = terminus;
            _telematrixCandidateSinceUtc = now;
            _telematrixDirection = "?";
            return;
        }

        // Match the source plugin's one-second stability rule before learning
        // a new terminal association.
        if (now - _telematrixCandidateSinceUtc < TimeSpan.FromSeconds(1))
        {
            _telematrixDirection = "?";
            return;
        }

        if (_telematrixPrimaryTerminus is null)
        {
            _telematrixPrimaryTerminus = terminus;
            _telematrixDirection = "TP";
        }
        else if (_telematrixSecondaryTerminus is null)
        {
            _telematrixSecondaryTerminus = terminus;
            _telematrixDirection = "TS";
        }
        else
        {
            _telematrixDirection = "?";
        }
    }

    private static string FormatTelematrixDelay(
        bool? scheduleActive,
        LocalOmsiOperationalSnapshot? operational,
        int? delaySeconds)
    {
        if (scheduleActive == false)
        {
            return "SEM HORÁRIO";
        }

        var state = NormalizeTelematrixText(operational?.IbisDelayState);
        var minutesText = NormalizeTelematrixText(operational?.IbisDelayMinutes);
        var secondsText = NormalizeTelematrixText(operational?.IbisDelaySeconds);

        if (minutesText is not null)
        {
            var suffix = state is null ? string.Empty : $" • {state}";
            return secondsText is null
                ? $"{minutesText} min{suffix}"
                : $"{minutesText}:{secondsText.PadLeft(2, '0')}{suffix}";
        }

        if (delaySeconds is not int seconds)
        {
            return scheduleActive == true ? "SEM DADOS" : "SEM HORÁRIO";
        }

        var minutes = seconds / 60d;
        if (Math.Abs(minutes) < 0.05d)
        {
            return "NO HORÁRIO";
        }

        return minutes > 0d
            ? $"+{minutes:F1} min • ATRASADO"
            : $"{minutes:F1} min • ADIANTADO";
    }

    private static bool TryFormatSimulationClock(
        LocalOmsiOperationalSnapshot operational,
        out string clock)
    {
        clock = "--:--";
        if (operational.SimulationTime is not double raw ||
            !double.IsFinite(raw) ||
            raw < 0d)
        {
            return false;
        }

        double totalMinutes;
        if (raw <= 24.5d)
        {
            totalMinutes = raw * 60d;
        }
        else if (raw <= 86_400d)
        {
            totalMinutes = raw / 60d;
        }
        else
        {
            return false;
        }

        var normalized =
            ((int)Math.Round(totalMinutes) % (24 * 60) + 24 * 60) %
            (24 * 60);
        var hours = normalized / 60;
        var minutes = normalized % 60;
        clock = $"{hours:00}:{minutes:00}";
        return true;
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.Select(NormalizeTelematrixText).FirstOrDefault(value => value is not null);

    private static string? NormalizeTelematrixText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
