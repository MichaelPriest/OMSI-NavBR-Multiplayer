using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NavBR.Client.Localization;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private bool _alpha12BusDashboardApplied;
    private TextBlock? _alpha12LineText;
    private TextBlock? _alpha12DestinationText;
    private TextBlock? _alpha12NextStopText;
    private TextBlock? _alpha12StreetText;
    private TextBlock? _alpha12DelayText;
    private TextBlock? _alpha12ClockText;
    private Border? _alpha12StopRequestedPanel;

    private void ApplyAlpha12BusDashboardSkin()
    {
        if (_alpha12BusDashboardApplied)
        {
            return;
        }

        EnsureBusDashboard();
        if (_busDashboardDock is null || _busDashboardDock.Child is not StackPanel stack)
        {
            return;
        }

        _alpha12BusDashboardApplied = true;

        _busDashboardDock.Width = 470d;
        _busDashboardDock.Padding = new Thickness(10d);
        _busDashboardDock.CornerRadius = new CornerRadius(18d);
        _busDashboardDock.Background = new LinearGradientBrush(
            Color.FromArgb(246, 4, 7, 9),
            Color.FromArgb(242, 12, 15, 17),
            90d);
        _busDashboardDock.BorderBrush = new SolidColorBrush(Color.FromArgb(210, 118, 79, 36));
        _busDashboardDock.BorderThickness = new Thickness(1.2d);

        var tripDisplay = BuildAlpha12TripDisplay();
        stack.Children.Insert(Math.Min(1, stack.Children.Count), tripDisplay);

        if (_dashboardSpeedText is not null)
        {
            _dashboardSpeedText.FontFamily = new FontFamily("Bahnschrift");
            _dashboardSpeedText.FontSize = 49d;
            _dashboardSpeedText.Foreground = AmberBrush();
            _dashboardSpeedText.FontWeight = FontWeights.SemiBold;
        }

        if (_dashboardAccelerationText is not null)
        {
            _dashboardAccelerationText.Foreground = new SolidColorBrush(Color.FromRgb(132, 150, 159));
            _dashboardAccelerationText.FontFamily = new FontFamily("Bahnschrift");
        }

        if (_dashboardFuelBar is not null)
        {
            _dashboardFuelBar.Foreground = AmberBrush();
            _dashboardFuelBar.Background = new SolidColorBrush(Color.FromRgb(26, 29, 30));
        }
        if (_dashboardThrottleBar is not null)
        {
            _dashboardThrottleBar.Foreground = new SolidColorBrush(Color.FromRgb(72, 199, 116));
            _dashboardThrottleBar.Background = new SolidColorBrush(Color.FromRgb(26, 29, 30));
        }
        if (_dashboardBrakeBar is not null)
        {
            _dashboardBrakeBar.Foreground = new SolidColorBrush(Color.FromRgb(236, 86, 72));
            _dashboardBrakeBar.Background = new SolidColorBrush(Color.FromRgb(26, 29, 30));
        }

        StyleDashboardSection(_dashboardFuelPanel);
        StyleDashboardSection(_dashboardPedalsPanel);
        StyleDashboardSection(_dashboardStatusPanel);

        foreach (var indicator in _dashboardIndicators.Values)
        {
            indicator.Badge.CornerRadius = new CornerRadius(4d);
            indicator.Badge.Background = new SolidColorBrush(Color.FromRgb(20, 24, 26));
            indicator.Badge.BorderBrush = new SolidColorBrush(Color.FromRgb(48, 53, 55));
            indicator.Label.FontFamily = new FontFamily("Bahnschrift");
            indicator.Label.FontSize = 8.5d;
        }

        if (_busDashboardTimer is not null)
        {
            _busDashboardTimer.Tick += Alpha12BusDashboardTimer_Tick;
        }

        RenderAlpha12BusTripDisplay();
    }

    private Border BuildAlpha12TripDisplay()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var linePanel = new Border
        {
            MinWidth = 74d,
            Padding = new Thickness(10d, 7d, 10d, 7d),
            Margin = new Thickness(0d, 0d, 10d, 0d),
            CornerRadius = new CornerRadius(5d),
            Background = new SolidColorBrush(Color.FromRgb(255, 147, 38)),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "LINHA",
                        Foreground = new SolidColorBrush(Color.FromRgb(46, 27, 10)),
                        FontFamily = new FontFamily("Bahnschrift"),
                        FontSize = 7.5d,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    (_alpha12LineText = new TextBlock
                    {
                        Text = "—",
                        Foreground = Brushes.Black,
                        FontFamily = new FontFamily("Bahnschrift"),
                        FontSize = 20d,
                        FontWeight = FontWeights.Black,
                        HorizontalAlignment = HorizontalAlignment.Center
                    })
                }
            }
        };
        Grid.SetColumn(linePanel, 0);
        top.Children.Add(linePanel);

        var destinationStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        destinationStack.Children.Add(new TextBlock
        {
            Text = "DESTINO",
            Foreground = new SolidColorBrush(Color.FromRgb(108, 122, 128)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 7.5d,
            FontWeight = FontWeights.Bold
        });
        _alpha12DestinationText = new TextBlock
        {
            Text = "SEM DESTINO",
            Foreground = AmberBrush(),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 17d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        destinationStack.Children.Add(_alpha12DestinationText);
        Grid.SetColumn(destinationStack, 1);
        top.Children.Add(destinationStack);

        var clockStack = new StackPanel
        {
            Margin = new Thickness(12d, 0d, 0d, 0d),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _alpha12ClockText = new TextBlock
        {
            Text = "--:--",
            Foreground = new SolidColorBrush(Color.FromRgb(211, 220, 223)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 12d,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _alpha12DelayText = new TextBlock
        {
            Text = "HORÁRIO —",
            Foreground = new SolidColorBrush(Color.FromRgb(120, 136, 143)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 8d,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0d, 3d, 0d, 0d),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        clockStack.Children.Add(_alpha12ClockText);
        clockStack.Children.Add(_alpha12DelayText);
        Grid.SetColumn(clockStack, 2);
        top.Children.Add(clockStack);

        Grid.SetRow(top, 0);
        root.Children.Add(top);

        var nextStopPanel = new Grid { Margin = new Thickness(0d, 10d, 0d, 0d) };
        nextStopPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        nextStopPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nextStopStack = new StackPanel();
        nextStopStack.Children.Add(new TextBlock
        {
            Text = "PRÓXIMA PARADA",
            Foreground = new SolidColorBrush(Color.FromRgb(108, 122, 128)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 7.5d,
            FontWeight = FontWeights.Bold
        });
        _alpha12NextStopText = new TextBlock
        {
            Text = "—",
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 14.5d,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _alpha12StreetText = new TextBlock
        {
            Text = "RUA —",
            Foreground = new SolidColorBrush(Color.FromRgb(127, 144, 152)),
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 9d,
            Margin = new Thickness(0d, 2d, 0d, 0d),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        nextStopStack.Children.Add(_alpha12NextStopText);
        nextStopStack.Children.Add(_alpha12StreetText);
        Grid.SetColumn(nextStopStack, 0);
        nextStopPanel.Children.Add(nextStopStack);

        _alpha12StopRequestedPanel = new Border
        {
            Margin = new Thickness(12d, 0d, 0d, 0d),
            Padding = new Thickness(10d, 6d, 10d, 6d),
            CornerRadius = new CornerRadius(5d),
            Background = new SolidColorBrush(Color.FromRgb(191, 37, 32)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(255, 102, 83)),
            BorderThickness = new Thickness(1d),
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "PARADA\nSOLICITADA",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Bahnschrift"),
                FontSize = 9d,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            }
        };
        Grid.SetColumn(_alpha12StopRequestedPanel, 1);
        nextStopPanel.Children.Add(_alpha12StopRequestedPanel);

        Grid.SetRow(nextStopPanel, 1);
        root.Children.Add(nextStopPanel);

        var separator = new Border
        {
            Height = 1d,
            Margin = new Thickness(0d, 10d, 0d, 0d),
            Background = new SolidColorBrush(Color.FromRgb(50, 54, 55))
        };
        Grid.SetRow(separator, 2);
        root.Children.Add(separator);

        return new Border
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            Padding = new Thickness(11d),
            CornerRadius = new CornerRadius(10d),
            Background = new SolidColorBrush(Color.FromRgb(7, 10, 11)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(52, 56, 57)),
            BorderThickness = new Thickness(1d),
            Child = root
        };
    }

    private void Alpha12BusDashboardTimer_Tick(object? sender, EventArgs e) =>
        RenderAlpha12BusTripDisplay();

    private void RenderAlpha12BusTripDisplay()
    {
        if (_alpha12LineText is null)
        {
            return;
        }

        var telemetry = _localTelemetry;
        _alpha12ClockText!.Text = DateTime.Now.ToString("HH:mm", LocalizationService.CurrentCulture);

        if (telemetry is null)
        {
            _alpha12LineText.Text = "—";
            _alpha12DestinationText!.Text = "SEM DESTINO";
            _alpha12NextStopText!.Text = "—";
            _alpha12StreetText!.Text = "RUA —";
            _alpha12DelayText!.Text = "HORÁRIO —";
            _alpha12DelayText.Foreground = new SolidColorBrush(Color.FromRgb(120, 136, 143));
            _alpha12StopRequestedPanel!.Visibility = Visibility.Collapsed;
            return;
        }

        _alpha12LineText.Text = CleanValue(telemetry.Line, "—", 10);
        _alpha12DestinationText!.Text = CleanValue(telemetry.DestinationName, "SEM DESTINO", 42).ToUpper(LocalizationService.CurrentCulture);
        _alpha12NextStopText!.Text = CleanValue(telemetry.NextStopName, "—", 50);
        _alpha12StreetText!.Text = string.IsNullOrWhiteSpace(telemetry.CurrentStreetName)
            ? "RUA —"
            : CleanValue(telemetry.CurrentStreetName, "RUA —", 58);

        RenderDelay(telemetry.DelaySeconds);
        _alpha12StopRequestedPanel!.Visibility = telemetry.StopRequested
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RenderDelay(int? delaySeconds)
    {
        if (_alpha12DelayText is null)
        {
            return;
        }

        if (delaySeconds is not int seconds)
        {
            _alpha12DelayText.Text = "HORÁRIO —";
            _alpha12DelayText.Foreground = new SolidColorBrush(Color.FromRgb(120, 136, 143));
            return;
        }

        var absoluteMinutes = Math.Abs(seconds) / 60d;
        if (Math.Abs(seconds) < 30)
        {
            _alpha12DelayText.Text = "NO HORÁRIO";
            _alpha12DelayText.Foreground = new SolidColorBrush(Color.FromRgb(82, 201, 122));
        }
        else if (seconds > 0)
        {
            _alpha12DelayText.Text = $"+{absoluteMinutes:F0} MIN ATRASO";
            _alpha12DelayText.Foreground = new SolidColorBrush(Color.FromRgb(255, 174, 69));
        }
        else
        {
            _alpha12DelayText.Text = $"{absoluteMinutes:F0} MIN ADIANT.";
            _alpha12DelayText.Foreground = new SolidColorBrush(Color.FromRgb(91, 183, 235));
        }
    }

    private static string CleanValue(string? value, string fallback, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text.Length <= maxLength ? text : text[..Math.Max(1, maxLength - 1)] + "…";
    }

    private static void StyleDashboardSection(Border? panel)
    {
        if (panel is null)
        {
            return;
        }

        panel.Background = new SolidColorBrush(Color.FromRgb(15, 18, 19));
        panel.BorderBrush = new SolidColorBrush(Color.FromRgb(45, 49, 50));
        panel.BorderThickness = new Thickness(1d);
        panel.CornerRadius = new CornerRadius(6d);
    }

    private static SolidColorBrush AmberBrush() =>
        new(Color.FromRgb(255, 174, 67));
}