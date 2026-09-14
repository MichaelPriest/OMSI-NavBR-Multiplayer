using System.Windows.Threading;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private const int VkF9 = 0x78;
    private const int VkF10 = 0x79;

    private bool _chatHotkeyAvailable = true;
    private bool _voiceHotkeyAvailable = true;
    private DateTimeOffset _nextOmsiHotkeyCheckUtc = DateTimeOffset.MinValue;
    private IReadOnlyList<string> _f9ConflictEvents = Array.Empty<string>();
    private IReadOnlyList<string> _f10ConflictEvents = Array.Empty<string>();

    private void InstallConflictFreeHotkeys()
    {
        try
        {
            _keyboardHook?.Dispose();
            _keyboardHook = null;
            _pressedKeys.Clear();
            RefreshOmsiHotkeyConflicts(force: true);

            _keyboardHook = new GlobalKeyboardHook();
            _keyboardHook.KeyChanged += (virtualKey, isDown) =>
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    () => HandleConflictFreeHotkey(virtualKey, isDown));
        }
        catch
        {
            // O HUD continua funcional sem atalhos globais.
        }
    }

    private void RefreshOmsiHotkeyConflicts(bool force = false)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force && now < _nextOmsiHotkeyCheckUtc)
        {
            return;
        }

        _nextOmsiHotkeyCheckUtc = now.AddSeconds(3);

        if (_omsiProcessId is not int processId)
        {
            _chatHotkeyAvailable = true;
            _voiceHotkeyAvailable = true;
            _f9ConflictEvents = Array.Empty<string>();
            _f10ConflictEvents = Array.Empty<string>();
            return;
        }

        var result = OmsiKeyboardConflictDetector.AnalyzeProcessInstallation(processId);
        _f9ConflictEvents = result.F9Events;
        _f10ConflictEvents = result.F10Events;
        _chatHotkeyAvailable = !result.F9InUse;
        _voiceHotkeyAvailable = !result.F10InUse;

        if (!_voiceHotkeyAvailable && _localPushToTalk)
        {
            SetLocalPushToTalk(false);
        }
    }

    private string BuildHotkeyConflictTooltip()
    {
        var details = new List<string>();
        if (_f9ConflictEvents.Count > 0)
        {
            details.Add($"F9: {string.Join(", ", _f9ConflictEvents.Take(4))}");
        }

        if (_f10ConflictEvents.Count > 0)
        {
            details.Add($"F10: {string.Join(", ", _f10ConflictEvents.Take(4))}");
        }

        return string.Join(Environment.NewLine, details);
    }

    private void HandleConflictFreeHotkey(int virtualKey, bool isDown)
    {
        if (isDown)
        {
            if (!_pressedKeys.Add(virtualKey))
            {
                return;
            }
        }
        else
        {
            _pressedKeys.Remove(virtualKey);
        }

        if (virtualKey == VkF10)
        {
            if (!_voiceHotkeyAvailable)
            {
                return;
            }

            if (isDown)
            {
                if (!_chatInteractive && IsOmsiForeground())
                {
                    SetLocalPushToTalk(true);
                }
            }
            else if (_localPushToTalk)
            {
                SetLocalPushToTalk(false);
            }

            return;
        }

        if (virtualKey == VkF9 &&
            _chatHotkeyAvailable &&
            isDown &&
            !_chatInteractive &&
            IsOmsiForeground())
        {
            OpenChatInput();
        }
    }
}
