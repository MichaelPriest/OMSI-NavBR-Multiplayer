using System.IO.Ports;
using System.Text;

namespace NavBR.Client.Hardware;

internal sealed class HardwareSerialTransport : IDisposable
{
    private readonly object _sync = new();
    private SerialPort? _port;

    public bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _port?.IsOpen == true;
            }
        }
    }

    public string? PortName
    {
        get
        {
            lock (_sync)
            {
                return _port?.PortName;
            }
        }
    }

    public int? BaudRate
    {
        get
        {
            lock (_sync)
            {
                return _port?.BaudRate;
            }
        }
    }

    public static IReadOnlyList<string> GetAvailablePorts() =>
        SerialPort.GetPortNames()
            .OrderBy(ParsePortNumber)
            .ThenBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public void Connect(string portName, int baudRate)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("Selecione uma porta COM.", nameof(portName));
        }

        if (baudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baudRate));
        }

        lock (_sync)
        {
            DisconnectUnsafe();

            var port = new SerialPort(portName.Trim(), baudRate, Parity.None, 8, StopBits.One)
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                NewLine = "\n",
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = false,
                ReadTimeout = 750,
                WriteTimeout = 750
            };

            try
            {
                port.Open();
                _port = port;
            }
            catch
            {
                port.Dispose();
                throw;
            }
        }
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            DisconnectUnsafe();
        }
    }

    public void SendFrame(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        lock (_sync)
        {
            if (_port?.IsOpen != true)
            {
                throw new InvalidOperationException("A porta serial não está conectada.");
            }

            _port.WriteLine(payload);
        }
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }

    private void DisconnectUnsafe()
    {
        var port = _port;
        _port = null;
        if (port is null)
        {
            return;
        }

        try
        {
            if (port.IsOpen)
            {
                port.Close();
            }
        }
        finally
        {
            port.Dispose();
        }
    }

    private static int ParsePortNumber(string portName)
    {
        if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(portName.AsSpan(3), out var number))
        {
            return number;
        }

        return int.MaxValue;
    }
}
