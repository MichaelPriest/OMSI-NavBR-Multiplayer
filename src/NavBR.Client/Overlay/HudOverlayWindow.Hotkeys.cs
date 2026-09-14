using System.Windows.Threading;
using NavBR.Client.Localization;

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

    private string BuildHotkeyConflictMessage()
    {
        var both = !_chatHotkeyAvailable && !_voiceHotkeyAvailable;
        var chatOnly = !_chatHotkeyAvailable && _voiceHotkeyAvailable;

        return LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
        {
            "pt" when both => "F9 e F10 desativadas: o OMSI já usa essas teclas.",
            "pt" when chatOnly => "F9 desativada: o OMSI já usa essa tecla.",
            "pt" => "F10 desativada: o OMSI já usa essa tecla.",
            "es" when both => "F9 y F10 desactivadas: OMSI ya usa estas teclas.",
            "es" when chatOnly => "F9 desactivada: OMSI ya usa esta tecla.",
            "es" => "F10 desactivada: OMSI ya usa esta tecla.",
            "de" when both => "F9 und F10 deaktiviert: OMSI verwendet diese Tasten bereits.",
            "de" when chatOnly => "F9 deaktiviert: OMSI verwendet diese Taste bereits.",
            "de" => "F10 deaktiviert: OMSI verwendet diese Taste bereits.",
            "fr" when both => "F9 et F10 désactivées : OMSI utilise déjà ces touches.",
            "fr" when chatOnly => "F9 désactivée : OMSI utilise déjà cette touche.",
            "fr" => "F10 désactivée : OMSI utilise déjà cette touche.",
            _ when both => "F9 and F10 disabled: OMSI already uses these keys.",
            _ when chatOnly => "F9 disabled: OMSI already uses this key.",
            _ => "F10 disabled: OMSI already uses this key."
        };
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
