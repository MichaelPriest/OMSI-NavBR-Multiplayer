using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Hardware;

internal sealed class HardwareCockpitView : Grid
{
    private readonly Func<VehicleTelemetry?> _telemetryProvider;
    private readonly DispatcherTimer _refreshTimer;
    private readonly TextBlock _statusText;
    private readonly TextBlock _lineValue;
    private readonly TextBlock _destinationValue;
    private readonly TextBlock _nextStopValue;
    private readonly TextBlock _streetValue;
    private readonly TextBlock _stopRequestedValue;
    private readonly TextBox _payloadPreview;

    public HardwareCockpitView(Func<VehicleTelemetry?> telemetryProvider)
    {
        _telemetryProvider = telemetryProvider;

        var stack = new StackPanel();
        Children.Add(stack);

        stack.Children.Add(new TextBlock
        {
            Text = "Hardware Cockpit Bridge",
            FontSize = 25d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Telemetria do NavBR pronta para letreiros físicos, computador de bordo, LEDs e painéis Arduino/ESP32.",
            Margin = new Thickness(0d, 6d, 0d, 20d),
            Foreground = Brush(145, 164, 180),
            FontSize = 13d,
            TextWrapping = TextWrapping.Wrap
        });

        var protocolCard = NewCard();
        var protocolStack = new StackPanel();
        protocolCard.Child = protocolStack;
        protocolStack.Children.Add(NewLabel("PROTOCOLO"));
        protocolStack.Children.Add(new TextBlock
        {
            Text = HardwareCockpitProtocol.Version,
            Margin = new Thickness(0d, 5d, 0d, 3d),
            FontSize = 18d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(244, 122, 24)
        });
        _statusText = new TextBlock
        {
            Text = "Aguardando telemetria do OMSI...",
            Foreground = Brush(183, 199, 213),
            TextWrapping = TextWrapping.Wrap
        };
        protocolStack.Children.Add(_statusText);
        stack.Children.Add(protocolCard);

        var transports = new Grid { Margin = new Thickness(0d, 14d, 0d, 14d) };
        transports.ColumnDefinitions.Add(new ColumnDefinition());
        transports.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14d) });
        transports.ColumnDefinitions.Add(new ColumnDefinition());
        var serial = BuildTransportCard("USB / Serial", "Arduino Uno, Mega, Nano e ESP32", "Próxima etapa");
        var wifi = BuildTransportCard("Wi-Fi / ESP32", "UDP/WebSocket para cockpit sem cabo", "Próxima etapa");
        Grid.SetColumn(serial, 0);
        Grid.SetColumn(wifi, 2);
        transports.Children.Add(serial);
        transports.Children.Add(wifi);
        stack.Children.Add(transports);

        var liveCard = NewCard();
        var liveStack = new StackPanel();
        liveCard.Child = liveStack;
        liveStack.Children.Add(NewLabel("TELEMETRIA AO VIVO"));

        var metrics = new Grid { Margin = new Thickness(0d, 12d, 0d, 0d) };
        for (var i = 0; i < 2; i++)
        {
            metrics.ColumnDefinitions.Add(new ColumnDefinition());
        }
        for (var i = 0; i < 3; i++)
        {
            metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        _lineValue = AddMetric(metrics, 0, 0, "Linha");
        _destinationValue = AddMetric(metrics, 1, 0, "Destino");
        _streetValue = AddMetric(metrics, 0, 1, "Rua atual");
        _nextStopValue = AddMetric(metrics, 1, 1, "Próxima parada");
        _stopRequestedValue = AddMetric(metrics, 0, 2, "Parada solicitada");
        liveStack.Children.Add(metrics);
        stack.Children.Add(liveCard);

        var payloadCard = NewCard();
        payloadCard.Margin = new Thickness(0d, 14d, 0d, 0d);
        var payloadStack = new StackPanel();
        payloadCard.Child = payloadStack;
        payloadStack.Children.Add(NewLabel("PREVIEW DO PACOTE PARA O HARDWARE"));
        _payloadPreview = new TextBox
        {
            Margin = new Thickness(0d, 10d, 0d, 0d),
            Height = 235d,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11.5d,
            Background = Brush(5, 12, 20),
            Foreground = Brush(188, 209, 229),
            BorderBrush = Brush(29, 42, 52),
            BorderThickness = new Thickness(1d),
            Padding = new Thickness(10d)
        };
        payloadStack.Children.Add(_payloadPreview);
        stack.Children.Add(payloadCard);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _refreshTimer.Tick += (_, _) => RefreshTelemetry();
        Loaded += (_, _) =>
        {
            RefreshTelemetry();
            _refreshTimer.Start();
        };
        Unloaded += (_, _) => _refreshTimer.Stop();
    }

    private void RefreshTelemetry()
    {
        var telemetry = _telemetryProvider();
        if (telemetry is null)
        {
            _statusText.Text = "Aguardando telemetria do OMSI...";
            SetValue(_lineValue, null);
            SetValue(_destinationValue, null);
            SetValue(_streetValue, null);
            SetValue(_nextStopValue, null);
            SetValue(_stopRequestedValue, null);
            _payloadPreview.Text = "{\n  \"protocol\": \"NAVBR_HW_V1\",\n  \"state\": \"waiting-for-telemetry\"\n}";
            return;
        }

        _statusText.Text = "Telemetria disponível. O pacote abaixo será a base dos transportes Serial e Wi-Fi.";
        SetValue(_lineValue, telemetry.Line);
        SetValue(_destinationValue, telemetry.DestinationName);
        SetValue(_streetValue, telemetry.CurrentStreetName);
        SetValue(_nextStopValue, telemetry.NextStopName);
        _stopRequestedValue.Text = telemetry.StopRequested ? "SIM" : "Não";
        _stopRequestedValue.Foreground = telemetry.StopRequested
            ? Brush(244, 122, 24)
            : Brushes.White;
        _payloadPreview.Text = HardwareCockpitProtocol.Serialize(telemetry);
    }

    private static Border BuildTransportCard(string title, string description, string state)
    {
        var border = NewCard();
        var stack = new StackPanel();
        border.Child = stack;
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        stack.Children.Add(new TextBlock
        {
            Text = description,
            Margin = new Thickness(0d, 5d, 0d, 10d),
            Foreground = Brush(145, 164, 180),
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Text = state,
            Foreground = Brush(244, 122, 24),
            FontSize = 11d,
            FontWeight = FontWeights.SemiBold
        });
        return border;
    }

    private static TextBlock AddMetric(Grid grid, int column, int row, string caption)
    {
        var stack = new StackPanel
        {
            Margin = new Thickness(0d, 0d, 14d, 14d)
        };
        stack.Children.Add(NewLabel(caption.ToUpperInvariant()));
        var value = new TextBlock
        {
            Text = "—",
            Margin = new Thickness(0d, 4d, 0d, 0d),
            FontSize = 14d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(value);
        Grid.SetColumn(stack, column);
        Grid.SetRow(stack, row);
        grid.Children.Add(stack);
        return value;
    }

    private static TextBlock NewLabel(string text) => new()
    {
        Text = text,
        FontSize = 9d,
        FontWeight = FontWeights.Bold,
        Foreground = Brush(111, 131, 146)
    };

    private static Border NewCard() => new()
    {
        Padding = new Thickness(18d),
        Background = Brush(8, 18, 27),
        BorderBrush = Brush(29, 42, 52),
        BorderThickness = new Thickness(1d),
        CornerRadius = new CornerRadius(12d)
    };

    private static SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    private static void SetValue(TextBlock target, string? value)
    {
        target.Text = string.IsNullOrWhiteSpace(value) ? "—" : value;
        target.Foreground = Brushes.White;
    }
}
