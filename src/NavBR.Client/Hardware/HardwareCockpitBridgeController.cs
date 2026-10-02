using NavBR.Shared.Telemetry;

namespace NavBR.Client.Hardware;

internal sealed record HardwareCockpitBridgeState(
    string Protocol,
    bool Connected,
    string? PortName,
    int BaudRate,
    bool AutoReconnect,
    IReadOnlyList<string> AvailablePorts,
    string? LastError,
    DateTimeOffset? LastFrameSentAtUtc,
    string? PayloadPreview,
    bool SelectedPortAvailable,
    bool ReconnectPending,
    int RetryAttempt,
    DateTimeOffset? NextReconnectAtUtc);

internal sealed class HardwareCockpitBridgeController : IDisposable
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30)
    ];

    private readonly object _sync = new();
    private readonly HardwareSerialTransport _transport = new();

    private string? _lastError;
    private DateTimeOffset? _lastFrameSentAtUtc;
    private DateTimeOffset _nextReconnectAtUtc = DateTimeOffset.MinValue;
    private int _retryIndex;

    public static HardwareCockpitBridgeController Shared { get; } = new();

    public bool IsConnected => _transport.IsConnected;
    public bool WantsTelemetry
    {
        get
        {
            if (_transport.IsConnected)
            {
                return true;
            }

            var settings =
                HardwareCockpitConnectionSettingsStore.Load();
            return settings.AutoReconnect &&
                   !string.IsNullOrWhiteSpace(settings.PortName);
        }
    }
    public string? PortName => _transport.PortName;
    public int? BaudRate => _transport.BaudRate;

    public HardwareCockpitBridgeState Snapshot(VehicleTelemetry? telemetry)
    {
        var settings = HardwareCockpitConnectionSettingsStore.Load();
        IReadOnlyList<string> ports;
        try
        {
            ports = HardwareSerialTransport.GetAvailablePorts();
        }
        catch
        {
            ports = Array.Empty<string>();
        }

        lock (_sync)
        {
            var selectedPort =
                _transport.PortName ?? settings.PortName;
            var selectedPortAvailable =
                !string.IsNullOrWhiteSpace(selectedPort) &&
                ports.Any(port =>
                    string.Equals(
                        port,
                        selectedPort,
                        StringComparison.OrdinalIgnoreCase));
            var reconnectPending =
                !_transport.IsConnected &&
                settings.AutoReconnect &&
                !string.IsNullOrWhiteSpace(settings.PortName);

            return new HardwareCockpitBridgeState(
                HardwareCockpitProtocol.Version,
                _transport.IsConnected,
                selectedPort,
                _transport.BaudRate ?? settings.BaudRate,
                settings.AutoReconnect,
                ports,
                _lastError,
                _lastFrameSentAtUtc,
                telemetry is null ? null : HardwareCockpitProtocol.Serialize(telemetry),
                selectedPortAvailable,
                reconnectPending,
                reconnectPending ? _retryIndex + 1 : 0,
                reconnectPending &&
                _nextReconnectAtUtc > DateTimeOffset.MinValue &&
                _nextReconnectAtUtc < DateTimeOffset.MaxValue
                    ? _nextReconnectAtUtc
                    : null);
        }
    }

    public void Connect(string portName, int baudRate, bool autoReconnect)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new InvalidOperationException("Selecione uma porta COM válida.");
        }

        lock (_sync)
        {
            _transport.Connect(portName.Trim(), baudRate);
            HardwareCockpitConnectionSettingsStore.Save(
                new HardwareCockpitConnectionSettings(
                    portName.Trim(),
                    baudRate,
                    autoReconnect));
            _lastError = null;
            ResetBackoffUnsafe();
        }
    }

    public void Disconnect(bool disableAutoReconnect)
    {
        lock (_sync)
        {
            _transport.Disconnect();
            var settings = HardwareCockpitConnectionSettingsStore.Load();
            HardwareCockpitConnectionSettingsStore.Save(settings with
            {
                AutoReconnect = disableAutoReconnect ? false : settings.AutoReconnect
            });
            _lastError = null;
            _nextReconnectAtUtc = disableAutoReconnect
                ? DateTimeOffset.MaxValue
                : DateTimeOffset.MinValue;
            _retryIndex = 0;
        }
    }

    public void SaveSelection(string? portName, int baudRate, bool autoReconnect)
    {
        var previous = HardwareCockpitConnectionSettingsStore.Load();
        HardwareCockpitConnectionSettingsStore.Save(previous with
        {
            PortName = string.IsNullOrWhiteSpace(portName) ? previous.PortName : portName.Trim(),
            BaudRate = baudRate > 0 ? baudRate : previous.BaudRate,
            AutoReconnect = autoReconnect
        });

        lock (_sync)
        {
            if (autoReconnect)
            {
                _nextReconnectAtUtc = DateTimeOffset.MinValue;
                _retryIndex = 0;
            }
        }
    }

    public void PublishTelemetry(VehicleTelemetry? telemetry)
    {
        if (telemetry is null)
        {
            return;
        }

        // In the default/no-hardware case, avoid taking the controller lock
        // and running reconnect logic on every telemetry poll. Settings are
        // immutable cached records, so this fast path stays coherent with
        // Connect/Disconnect/SaveSelection while preserving exact-port
        // auto-reconnect behavior when it is enabled.
        if (!_transport.IsConnected)
        {
            var settings = HardwareCockpitConnectionSettingsStore.Load();
            if (!settings.AutoReconnect ||
                string.IsNullOrWhiteSpace(settings.PortName))
            {
                return;
            }
        }

        lock (_sync)
        {
            TryReconnectUnsafe();

            if (!_transport.IsConnected)
            {
                return;
            }

            try
            {
                _transport.SendFrame(HardwareCockpitProtocol.SerializeCompact(telemetry));
                _lastFrameSentAtUtc = DateTimeOffset.UtcNow;
                _lastError = null;
                ResetBackoffUnsafe();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _transport.Disconnect();
                ScheduleRetryUnsafe();
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _transport.Dispose();
        }
    }

    private void TryReconnectUnsafe()
    {
        if (_transport.IsConnected)
        {
            return;
        }

        var settings = HardwareCockpitConnectionSettingsStore.Load();
        if (!settings.AutoReconnect ||
            string.IsNullOrWhiteSpace(settings.PortName) ||
            DateTimeOffset.UtcNow < _nextReconnectAtUtc)
        {
            return;
        }

        IReadOnlyList<string> ports;
        try
        {
            ports = HardwareSerialTransport.GetAvailablePorts();
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            ScheduleRetryUnsafe();
            return;
        }

        var exactPort = ports.FirstOrDefault(port =>
            string.Equals(port, settings.PortName, StringComparison.OrdinalIgnoreCase));
        if (exactPort is null)
        {
            _lastError = $"A porta {settings.PortName} não está disponível.";
            ScheduleRetryUnsafe();
            return;
        }

        try
        {
            _transport.Connect(exactPort, settings.BaudRate);
            _lastError = null;
            ResetBackoffUnsafe();
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            ScheduleRetryUnsafe();
        }
    }

    private void ResetBackoffUnsafe()
    {
        _retryIndex = 0;
        _nextReconnectAtUtc = DateTimeOffset.MinValue;
    }

    private void ScheduleRetryUnsafe()
    {
        var delay = RetryDelays[Math.Min(_retryIndex, RetryDelays.Length - 1)];
        _retryIndex = Math.Min(_retryIndex + 1, RetryDelays.Length - 1);
        _nextReconnectAtUtc = DateTimeOffset.UtcNow + delay;
    }
}
