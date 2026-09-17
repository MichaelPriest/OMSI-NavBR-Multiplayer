using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Hardware;

internal sealed class HardwareCockpitView : Grid
{
    private static readonly int[] SupportedBaudRates = [9600, 19200, 38400, 57600, 115200, 230400];

    private readonly Func<VehicleTelemetry?> _telemetryProvider;
    private readonly DispatcherTimer _refreshTimer;
    private readonly HardwareSerialTransport _serialTransport = new();
    private readonly TextBlock _statusText;
    private readonly TextBlock _lineValue;
    private readonly TextBlock _destinationValue;
    private readonly TextBlock _nextStopValue;
    private readonly TextBlock _streetValue;
    private readonly TextBlock _stopRequestedValue;
    private readonly TextBlock _speedValue;
    private readonly TextBox _payloadPreview;
    private readonly ComboBox _portComboBox;
    private readonly ComboBox _baudComboBox;
    private readonly TextBlock _serialStatusText;
    private readonly Button _serialToggleButton;

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
            Text = "Telemetria do NavBR para letreiros físicos, computador de bordo, LEDs e painéis Arduino/ESP32.",
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

        var serialCard = NewCard();
        var serialStack = new StackPanel();
        serialCard.Child = serialStack;
        serialStack.Children.Add(new TextBlock
        {
            Text = "USB / Serial",
            FontSize = 16d,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        serialStack.Children.Add(new TextBlock
        {
            Text = "Arduino Uno, Mega, Nano e ESP32 • JSON Lines • 5 Hz",
            Margin = new Thickness(0d, 5d, 0d, 12d),
            Foreground = Brush(145, 164, 180),
            TextWrapping = TextWrapping.Wrap
        });

        var serialControls = new WrapPanel
        {
            Margin = new Thickness(0d, 0d, 0d, 10d),
            VerticalAlignment = VerticalAlignment.Center
        };

        _portComboBox = new ComboBox
        {
            MinWidth = 105d,
            Height = 34d,
            Margin = new Thickness(0d, 0d, 8d, 8d),
            ToolTip = "Porta COM do Arduino/ESP32"
        };
        serialControls.Children.Add(_portComboBox);

        _baudComboBox = new ComboBox
        {
            MinWidth = 100d,
            Height = 34d,
            Margin = new Thickness(0d, 0d, 8d, 8d),
            ItemsSource = SupportedBaudRates,
            SelectedItem = 115200,
            ToolTip = "Velocidade da porta serial"
        };
        serialControls.Children.Add(_baudComboBox);

        var refreshPortsButton = NewActionButton("↻ Portas");
        refreshPortsButton.Click += (_, _) => RefreshPorts();
        serialControls.Children.Add(refreshPortsButton);

        _serialToggleButton = NewActionButton("Conectar");
        _serialToggleButton.Margin = new Thickness(0d, 0d, 0d, 8d);
        _serialToggleButton.Click += (_, _) => ToggleSerialConnection();
        serialControls.Children.Add(_serialToggleButton);

        serialStack.Children.Add(serialControls);

        _serialStatusText = new TextBlock
        {
            Text = "Procurando portas COM...",
            Foreground = Brush(183, 199, 213),
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap
        };
        serialStack.Children.Add(_serialStatusText);

        var wifi = BuildTransportCard(
            "Wi-Fi / ESP32",
            "UDP/WebSocket para cockpit sem cabo",
            "Planejado após validação do protocolo Serial");
        Grid.SetColumn(serialCard, 0);
        Grid.SetColumn(wifi, 2);
        transports.Children.Add(serialCard);
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
        _speedValue = AddMetric(metrics, 1, 2, "Velocidade");
        liveStack.Children.Add(metrics);
        stack.Children.Add(liveCard);

        var payloadCard = NewCard();
        var payloadStack = new StackPanel();
        payloadCard.Child = payloadStack;
        payloadStack.Children.Add(new TextBlock
        {
            Text = "Na porta serial o mesmo pacote é enviado sem indentação, uma linha por atualização.",
            Foreground = Brush(145, 164, 180),
            FontSize = 11d,
            TextWrapping = TextWrapping.Wrap
        });
        _payloadPreview = new TextBox
        {
            Margin = new Thickness(0d, 10d, 0d, 0d),
            Height = 210d,
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

        var payloadExpander = new Expander
        {
            Header = new TextBlock
            {
                Text = "PREVIEW TÉCNICO DO PACOTE",
                Foreground = Brush(151, 171, 185),
                FontSize = 10d,
                FontWeight = FontWeights.Bold
            },
            IsExpanded = false,
            Margin = new Thickness(0d, 14d, 0d, 0d),
            Content = payloadCard
        };
        stack.Children.Add(payloadExpander);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _refreshTimer.Tick += (_, _) => RefreshTelemetry();
        Loaded += (_, _) =>
        {
            RefreshPorts();
            RefreshTelemetry();
            _refreshTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            _refreshTimer.Stop();
            _serialTransport.Dispose();
        };
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
            SetValue(_speedValue, null);
            _payloadPreview.Text = "{\n  \"protocol\": \"NAVBR_HW_V1\",\n  \"state\": \"waiting-for-telemetry\"\n}";
            return;
        }

        _statusText.Text = "Telemetria disponível. O pacote abaixo alimenta o cockpit físico e usa a mesma velocidade corrigida do HUD.";
        SetValue(_lineValue, telemetry.Line);
        SetValue(_destinationValue, telemetry.DestinationName);
        SetValue(_streetValue, telemetry.CurrentStreetName);
        SetValue(_nextStopValue, telemetry.NextStopName);
        _stopRequestedValue.Text = telemetry.StopRequested ? "SIM" : "Não";
        _stopRequestedValue.Foreground = telemetry.StopRequested
            ? Brush(244, 122, 24)
            : Brushes.White;
        _speedValue.Text = $"{telemetry.SpeedKph:F1} km/h";
        _speedValue.Foreground = Brushes.White;
        _payloadPreview.Text = HardwareCockpitProtocol.Serialize(telemetry);

        if (!_serialTransport.IsConnected)
        {
            return;
        }

        try
        {
            _serialTransport.SendFrame(HardwareCockpitProtocol.SerializeCompact(telemetry));
            _serialStatusText.Text = $"Conectado em {_serialTransport.PortName} @ {_serialTransport.BaudRate} baud • enviando 5 Hz";
            _serialStatusText.Foreground = Brush(78, 201, 137);
        }
        catch (Exception ex)
        {
            _serialTransport.Disconnect();
            _serialToggleButton.Content = "Conectar";
            _serialStatusText.Text = $"Conexão serial interrompida: {ex.Message}";
            _serialStatusText.Foreground = Brush(237, 111, 111);
        }
    }

    private void RefreshPorts()
    {
        try
        {
            var previous = _portComboBox.SelectedItem as string ?? _serialTransport.PortName;
            var ports = HardwareSerialTransport.GetAvailablePorts();
            _portComboBox.ItemsSource = ports;

            if (previous is not null && ports.Contains(previous, StringComparer.OrdinalIgnoreCase))
            {
                _portComboBox.SelectedItem = ports.First(port =>
                    string.Equals(port, previous, StringComparison.OrdinalIgnoreCase));
            }
            else if (ports.Count > 0)
            {
                _portComboBox.SelectedIndex = 0;
            }

            if (_serialTransport.IsConnected)
            {
                return;
            }

            _serialStatusText.Text = ports.Count == 0
                ? "Nenhuma porta COM encontrada. Conecte o Arduino/ESP32 e clique em Portas."
                : $"{ports.Count} porta(s) encontrada(s). Selecione a COM e conecte.";
            _serialStatusText.Foreground = Brush(183, 199, 213);
        }
        catch (Exception ex)
        {
            _serialStatusText.Text = $"Não foi possível enumerar as portas: {ex.Message}";
            _serialStatusText.Foreground = Brush(237, 111, 111);
        }
    }

    private void ToggleSerialConnection()
    {
        if (_serialTransport.IsConnected)
        {
            _serialTransport.Disconnect();
            _serialToggleButton.Content = "Conectar";
            _serialStatusText.Text = "Porta serial desconectada.";
            _serialStatusText.Foreground = Brush(183, 199, 213);
            return;
        }

        if (_portComboBox.SelectedItem is not string portName)
        {
            RefreshPorts();
            if (_portComboBox.SelectedItem is not string refreshedPort)
            {
                _serialStatusText.Text = "Selecione uma porta COM válida.";
                _serialStatusText.Foreground = Brush(237, 111, 111);
                return;
            }

            portName = refreshedPort;
        }

        var baudRate = _baudComboBox.SelectedItem is int selectedBaud
            ? selectedBaud
            : 115200;

        try
        {
            _serialTransport.Connect(portName, baudRate);
            _serialToggleButton.Content = "Desconectar";
            _serialStatusText.Text = $"Conectado em {portName} @ {baudRate} baud. Aguardando o próximo quadro de telemetria.";
            _serialStatusText.Foreground = Brush(78, 201, 137);
        }
        catch (Exception ex)
        {
            _serialToggleButton.Content = "Conectar";
            _serialStatusText.Text = $"Falha ao abrir {portName}: {ex.Message}";
            _serialStatusText.Foreground = Brush(237, 111, 111);
        }
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
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        return border;
    }

    private static Button NewActionButton(string text) => new()
    {
        Content = text,
        Height = 34d,
        MinWidth = 86d,
        Margin = new Thickness(0d, 0d, 8d, 8d),
        Padding = new Thickness(12d, 5d, 12d, 5d),
        Background = Brush(15, 30, 41),
        Foreground = Brushes.White,
        BorderBrush = Brush(46, 65, 79),
        BorderThickness = new Thickness(1d),
        Cursor = System.Windows.Input.Cursors.Hand
    };

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
