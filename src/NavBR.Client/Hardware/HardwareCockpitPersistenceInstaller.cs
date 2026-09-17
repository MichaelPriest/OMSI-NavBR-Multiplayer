using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NavBR.Client.Hardware;

internal sealed record HardwareCockpitConnectionSettings(
    string? PortName = null,
    int BaudRate = 115200,
    bool AutoReconnect = false);

internal static class HardwareCockpitConnectionSettingsStore
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "hardware-cockpit.json");
    private static HardwareCockpitConnectionSettings? _cached;

    public static HardwareCockpitConnectionSettings Load()
    {
        lock (Sync)
        {
            _cached ??= LoadCore();
            return _cached;
        }
    }

    public static void Save(HardwareCockpitConnectionSettings settings)
    {
        settings = Normalize(settings);
        lock (Sync)
        {
            _cached = settings;
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.WriteAllText(
                    FilePath,
                    JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Hardware preferences must never break the main application.
            }
        }
    }

    private static HardwareCockpitConnectionSettings LoadCore()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var parsed = JsonSerializer.Deserialize<HardwareCockpitConnectionSettings>(File.ReadAllText(FilePath));
                if (parsed is not null)
                {
                    return Normalize(parsed);
                }
            }
        }
        catch
        {
            // A damaged local preference file is ignored safely.
        }

        return new HardwareCockpitConnectionSettings();
    }

    private static HardwareCockpitConnectionSettings Normalize(HardwareCockpitConnectionSettings settings) => settings with
    {
        PortName = string.IsNullOrWhiteSpace(settings.PortName) ? null : settings.PortName.Trim(),
        BaudRate = settings.BaudRate > 0 ? settings.BaudRate : 115200
    };
}

/// <summary>
/// Adds persistence and safe auto-reconnect to the existing Hardware Cockpit UI
/// without changing the serial transport or the NAVBR_HW_V1 streaming path.
/// Reconnection is only attempted against the exact COM port explicitly chosen
/// by the user; NavBR never silently switches to another serial device.
/// </summary>
internal static class HardwareCockpitPersistenceInstaller
{
    private static readonly ConditionalWeakTable<HardwareCockpitView, Session> Sessions = new();

    public static void Attach(HardwareCockpitView view)
    {
        var session = Sessions.GetValue(view, static hardwareView => new Session(hardwareView));
        session.OnLoaded();
    }

    private sealed class Session
    {
        private static readonly int[] RetrySeconds = [2, 5, 10, 20, 30];

        private readonly HardwareCockpitView _view;
        private readonly DispatcherTimer _retryTimer = new() { Interval = TimeSpan.FromSeconds(2) };

        private ComboBox? _portCombo;
        private ComboBox? _baudCombo;
        private Button? _toggleButton;
        private Button? _refreshButton;
        private bool _handlersAttached;
        private bool _suppressSave;
        private bool _attemptingReconnect;
        private bool _manualIntentCaptured;
        private bool _manualDisconnectIntent;
        private int _retryIndex;
        private DateTimeOffset _nextRetryAt = DateTimeOffset.MinValue;

        public Session(HardwareCockpitView view)
        {
            _view = view;
            _retryTimer.Tick += (_, _) => RetryIfNeeded();
            _view.Unloaded += (_, _) => _retryTimer.Stop();
        }

        public void OnLoaded()
        {
            _view.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(InitializeAndStart));
        }

        private void InitializeAndStart()
        {
            ResolveControls();
            if (_portCombo is null || _baudCombo is null || _toggleButton is null)
            {
                return;
            }

            ApplySavedSelection();
            AttachHandlers();
            _retryTimer.Start();
            _nextRetryAt = DateTimeOffset.UtcNow;
            RetryIfNeeded();
        }

        private void ResolveControls()
        {
            var combos = Enumerate<ComboBox>(_view).ToArray();
            _portCombo ??= combos.FirstOrDefault(combo =>
                string.Equals(combo.ToolTip as string, "Porta COM do Arduino/ESP32", StringComparison.Ordinal));
            _baudCombo ??= combos.FirstOrDefault(combo =>
                string.Equals(combo.ToolTip as string, "Velocidade da porta serial", StringComparison.Ordinal));

            var buttons = Enumerate<Button>(_view).ToArray();
            _toggleButton ??= buttons.FirstOrDefault(button => IsConnectionButton(button.Content));
            _refreshButton ??= buttons.FirstOrDefault(button =>
                (button.Content as string)?.Contains("Portas", StringComparison.OrdinalIgnoreCase) == true);
        }

        private void AttachHandlers()
        {
            if (_handlersAttached || _portCombo is null || _baudCombo is null || _toggleButton is null)
            {
                return;
            }

            _handlersAttached = true;
            _portCombo.SelectionChanged += (_, _) => SaveSelection();
            _baudCombo.SelectionChanged += (_, _) => SaveSelection();
            _toggleButton.PreviewMouseLeftButtonDown += (_, _) => CaptureManualIntent();
            _toggleButton.PreviewKeyDown += (_, args) =>
            {
                if (args.Key is Key.Enter or Key.Space)
                {
                    CaptureManualIntent();
                }
            };
            _toggleButton.Click += (_, _) => HandleConnectionClickCompleted();
        }

        private void CaptureManualIntent()
        {
            if (_attemptingReconnect || _toggleButton is null)
            {
                return;
            }

            _manualIntentCaptured = true;
            _manualDisconnectIntent = IsDisconnectState(_toggleButton.Content);
        }

        private void HandleConnectionClickCompleted()
        {
            if (_toggleButton is null || _attemptingReconnect)
            {
                return;
            }

            if (IsDisconnectState(_toggleButton.Content))
            {
                SaveSelection(autoReconnect: true);
                ResetBackoff();
            }
            else if (_manualIntentCaptured && _manualDisconnectIntent)
            {
                SaveSelection(autoReconnect: false);
                _nextRetryAt = DateTimeOffset.MaxValue;
            }
            else
            {
                // A manual connection attempt that failed still represents a
                // user request to keep this exact device connected.
                SaveSelection(autoReconnect: true);
                ScheduleNextRetry();
            }

            _manualIntentCaptured = false;
            _manualDisconnectIntent = false;
        }

        private void ApplySavedSelection()
        {
            if (_portCombo is null || _baudCombo is null)
            {
                return;
            }

            var settings = HardwareCockpitConnectionSettingsStore.Load();
            _suppressSave = true;
            try
            {
                if (settings.PortName is not null)
                {
                    SelectPort(settings.PortName);
                }

                if (_baudCombo.Items.Cast<object>().Any(item => item is int baud && baud == settings.BaudRate))
                {
                    _baudCombo.SelectedItem = settings.BaudRate;
                }
            }
            finally
            {
                _suppressSave = false;
            }
        }

        private void SaveSelection(bool? autoReconnect = null)
        {
            if (_suppressSave || _portCombo is null || _baudCombo is null)
            {
                return;
            }

            var previous = HardwareCockpitConnectionSettingsStore.Load();
            HardwareCockpitConnectionSettingsStore.Save(previous with
            {
                PortName = _portCombo.SelectedItem as string ?? previous.PortName,
                BaudRate = _baudCombo.SelectedItem is int baud ? baud : previous.BaudRate,
                AutoReconnect = autoReconnect ?? previous.AutoReconnect
            });
        }

        private void RetryIfNeeded()
        {
            if (_toggleButton is null || _portCombo is null || _baudCombo is null)
            {
                return;
            }

            var settings = HardwareCockpitConnectionSettingsStore.Load();
            if (!settings.AutoReconnect || string.IsNullOrWhiteSpace(settings.PortName))
            {
                return;
            }

            if (IsDisconnectState(_toggleButton.Content))
            {
                ResetBackoff();
                return;
            }

            if (DateTimeOffset.UtcNow < _nextRetryAt)
            {
                return;
            }

            // Refreshing the legacy combo may auto-select the first available
            // COM. Suppress persistence during that refresh so the saved target
            // can never be silently replaced by another serial device.
            var previousSuppress = _suppressSave;
            _suppressSave = true;
            try
            {
                _refreshButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            finally
            {
                _suppressSave = previousSuppress;
            }

            if (!SelectPort(settings.PortName))
            {
                ScheduleNextRetry();
                return;
            }

            previousSuppress = _suppressSave;
            _suppressSave = true;
            try
            {
                if (_baudCombo.Items.Cast<object>().Any(item => item is int baud && baud == settings.BaudRate))
                {
                    _baudCombo.SelectedItem = settings.BaudRate;
                }
            }
            finally
            {
                _suppressSave = previousSuppress;
            }

            _attemptingReconnect = true;
            try
            {
                _toggleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            finally
            {
                _attemptingReconnect = false;
            }

            if (IsDisconnectState(_toggleButton.Content))
            {
                SaveSelection(autoReconnect: true);
                ResetBackoff();
            }
            else
            {
                ScheduleNextRetry();
            }
        }

        private bool SelectPort(string portName)
        {
            if (_portCombo is null)
            {
                return false;
            }

            var match = _portCombo.Items.Cast<object>()
                .OfType<string>()
                .FirstOrDefault(item => string.Equals(item, portName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return false;
            }

            var previousSuppress = _suppressSave;
            _suppressSave = true;
            try
            {
                _portCombo.SelectedItem = match;
            }
            finally
            {
                _suppressSave = previousSuppress;
            }
            return true;
        }

        private void ResetBackoff()
        {
            _retryIndex = 0;
            _nextRetryAt = DateTimeOffset.UtcNow.AddSeconds(RetrySeconds[0]);
        }

        private void ScheduleNextRetry()
        {
            var seconds = RetrySeconds[Math.Min(_retryIndex, RetrySeconds.Length - 1)];
            _retryIndex = Math.Min(_retryIndex + 1, RetrySeconds.Length - 1);
            _nextRetryAt = DateTimeOffset.UtcNow.AddSeconds(seconds);
        }

        private static bool IsConnectionButton(object? content)
        {
            var text = content as string;
            return string.Equals(text, "Conectar", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "Desconectar", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDisconnectState(object? content) =>
            string.Equals(content as string, "Desconectar", StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<T> Enumerate<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T match)
            {
                yield return match;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in Enumerate<T>(VisualTreeHelper.GetChild(root, index)))
                {
                    yield return child;
                }
            }
        }
    }
}
