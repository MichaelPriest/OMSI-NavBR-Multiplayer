using System.Windows.Threading;
using NavBR.Client.Localization;
using NavBR.Client.Multiplayer;

namespace NavBR.Client.Overlay;

public partial class HudOverlayWindow
{
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkLeftShift = 0xA0;
    private const int VkRightShift = 0xA1;
    private const int VkLeftControl = 0xA2;
    private const int VkRightControl = 0xA3;

    private NavBRHotkeyDefinition _chatHotkey = NavBRHotkeyCatalog.Resolve(
        NavBRHotkeyCatalog.DefaultChatHotkey,
        NavBRHotkeyCatalog.DefaultChatHotkey);
    private NavBRHotkeyDefinition _voiceHotkey = NavBRHotkeyCatalog.Resolve(
        NavBRHotkeyCatalog.DefaultVoiceHotkey,
        NavBRHotkeyCatalog.DefaultVoiceHotkey);

    private bool _chatHotkeyAvailable;
    private bool _voiceHotkeyAvailable;
    private bool _omsiHotkeyConfigVerified;
    private bool _hotkeysDistinct = true;
    private bool _settingsHooked;
    private DateTimeOffset _nextOmsiHotkeyCheckUtc = DateTimeOffset.MinValue;
    private IReadOnlyList<string> _chatConflictEvents = Array.Empty<string>();
    private IReadOnlyList<string> _voiceConflictEvents = Array.Empty<string>();

    public void ConfigureHotkeys(string? chatHotkey, string? voiceHotkey)
    {
        _chatHotkey = NavBRHotkeyCatalog.Resolve(chatHotkey, NavBRHotkeyCatalog.DefaultChatHotkey);
        _voiceHotkey = NavBRHotkeyCatalog.Resolve(voiceHotkey, NavBRHotkeyCatalog.DefaultVoiceHotkey);
        _hotkeysDistinct = _chatHotkey.VirtualKey != _voiceHotkey.VirtualKey ||
                           _chatHotkey.OmsiModifierMask != _voiceHotkey.OmsiModifierMask;
        _nextOmsiHotkeyCheckUtc = DateTimeOffset.MinValue;
        RefreshOmsiHotkeyConflicts(force: true);
        RefreshHudChrome();
    }

    private void InstallConflictFreeHotkeys()
    {
        try
        {
            _keyboardHook?.Dispose();
            _keyboardHook = null;
            _pressedKeys.Clear();

            var settings = MultiplayerSettingsStore.Load();
            ConfigureHotkeys(settings.ChatHotkey, settings.VoiceHotkey);
            if (!_settingsHooked)
            {
                _settingsHooked = true;
                MultiplayerSettingsStore.SettingsSaved += OnMultiplayerSettingsSaved;
            }

            _keyboardHook = new GlobalKeyboardHook();
            _keyboardHook.KeyChanged += (virtualKey, isDown) =>
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    () => HandleConflictFreeHotkey(virtualKey, isDown));
        }
        catch
        {
            _chatHotkeyAvailable = false;
            _voiceHotkeyAvailable = false;
            _omsiHotkeyConfigVerified = false;
        }
    }

    private void OnMultiplayerSettingsSaved(MultiplayerSettings settings)
    {
        _ = Dispatcher.BeginInvoke(() =>
            ConfigureHotkeys(settings.ChatHotkey, settings.VoiceHotkey));
    }

    private void UnsubscribeHotkeySettings()
    {
        if (!_settingsHooked)
        {
            return;
        }

        MultiplayerSettingsStore.SettingsSaved -= OnMultiplayerSettingsSaved;
        _settingsHooked = false;
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
            DisableUnverifiedHotkeys();
            return;
        }

        var result = OmsiKeyboardConflictDetector.AnalyzeProcessInstallation(processId);
        _omsiHotkeyConfigVerified = result.ConfigFound;
        _chatConflictEvents = result.GetEvents(
            _chatHotkey.OmsiScanCode,
            _chatHotkey.OmsiModifierMask);
        _voiceConflictEvents = result.GetEvents(
            _voiceHotkey.OmsiScanCode,
            _voiceHotkey.OmsiModifierMask);
        _chatHotkeyAvailable = result.ConfigFound && _hotkeysDistinct && _chatConflictEvents.Count == 0;
        _voiceHotkeyAvailable = result.ConfigFound && _hotkeysDistinct && _voiceConflictEvents.Count == 0;

        if (!_voiceHotkeyAvailable && _localPushToTalk)
        {
            SetLocalPushToTalk(false);
        }
    }

    private void DisableUnverifiedHotkeys()
    {
        _chatHotkeyAvailable = false;
        _voiceHotkeyAvailable = false;
        _omsiHotkeyConfigVerified = false;
        _chatConflictEvents = Array.Empty<string>();
        _voiceConflictEvents = Array.Empty<string>();
    }

    private string BuildHotkeyConflictTooltip()
    {
        if (!_omsiHotkeyConfigVerified)
        {
            return "Inputs\\keyboard.cfg";
        }

        if (!_hotkeysDistinct)
        {
            return $"{_chatHotkey.Name}: chat + PTT";
        }

        var details = new List<string>();
        if (_chatConflictEvents.Count > 0)
        {
            details.Add($"{_chatHotkey.Name}: {string.Join(", ", _chatConflictEvents.Take(4))}");
        }

        if (_voiceConflictEvents.Count > 0)
        {
            details.Add($"{_voiceHotkey.Name}: {string.Join(", ", _voiceConflictEvents.Take(4))}");
        }

        return string.Join(Environment.NewLine, details);
    }

    private string BuildHotkeyConflictMessage()
    {
        var language = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
        if (!_omsiHotkeyConfigVerified)
        {
            return language switch
            {
                "pt" => "Atalhos desativados: não foi possível verificar o keyboard.cfg do OMSI.",
                "es" => "Atajos desactivados: no se pudo verificar el keyboard.cfg de OMSI.",
                "de" => "Hotkeys deaktiviert: OMSIs keyboard.cfg konnte nicht geprüft werden.",
                "fr" => "Raccourcis désactivés : impossible de vérifier le keyboard.cfg d’OMSI.",
                _ => "Hotkeys disabled: OMSI keyboard.cfg could not be verified."
            };
        }

        if (!_hotkeysDistinct)
        {
            return language switch
            {
                "pt" => "Escolha teclas diferentes para chat e voz.",
                "es" => "Elige teclas diferentes para chat y voz.",
                "de" => "Für Chat und Sprache unterschiedliche Tasten wählen.",
                "fr" => "Choisissez des touches différentes pour le chat et la voix.",
                _ => "Choose different keys for chat and voice."
            };
        }

        var both = !_chatHotkeyAvailable && !_voiceHotkeyAvailable;
        var chatOnly = !_chatHotkeyAvailable && _voiceHotkeyAvailable;

        return language switch
        {
            "pt" when both => $"{_chatHotkey.Name} e {_voiceHotkey.Name} desativadas: o OMSI já usa essas combinações.",
            "pt" when chatOnly => $"{_chatHotkey.Name} desativada: o OMSI já usa essa combinação.",
            "pt" => $"{_voiceHotkey.Name} desativada: o OMSI já usa essa combinação.",
            "es" when both => $"{_chatHotkey.Name} y {_voiceHotkey.Name} desactivadas: OMSI ya usa estas combinaciones.",
            "es" when chatOnly => $"{_chatHotkey.Name} desactivada: OMSI ya usa esta combinación.",
            "es" => $"{_voiceHotkey.Name} desactivada: OMSI ya usa esta combinación.",
            "de" when both => $"{_chatHotkey.Name} und {_voiceHotkey.Name} deaktiviert: OMSI verwendet diese Kombinationen bereits.",
            "de" when chatOnly => $"{_chatHotkey.Name} deaktiviert: OMSI verwendet diese Kombination bereits.",
            "de" => $"{_voiceHotkey.Name} deaktiviert: OMSI verwendet diese Kombination bereits.",
            "fr" when both => $"{_chatHotkey.Name} et {_voiceHotkey.Name} désactivées : OMSI utilise déjà ces combinaisons.",
            "fr" when chatOnly => $"{_chatHotkey.Name} désactivée : OMSI utilise déjà cette combinaison.",
            "fr" => $"{_voiceHotkey.Name} désactivée : OMSI utilise déjà cette combinaison.",
            _ when both => $"{_chatHotkey.Name} and {_voiceHotkey.Name} disabled: OMSI already uses these combinations.",
            _ when chatOnly => $"{_chatHotkey.Name} disabled: OMSI already uses this combination.",
            _ => $"{_voiceHotkey.Name} disabled: OMSI already uses this combination."
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

        var modifierMask = GetCurrentOmsiModifierMask();

        if (virtualKey == _voiceHotkey.VirtualKey)
        {
            if (!isDown && _localPushToTalk)
            {
                SetLocalPushToTalk(false);
                return;
            }

            if (isDown &&
                _voiceHotkeyAvailable &&
                modifierMask == _voiceHotkey.OmsiModifierMask &&
                !_chatInteractive &&
                IsOmsiForeground())
            {
                SetLocalPushToTalk(true);
                return;
            }
        }

        if (virtualKey == _chatHotkey.VirtualKey &&
            _chatHotkeyAvailable &&
            isDown &&
            modifierMask == _chatHotkey.OmsiModifierMask &&
            !_chatInteractive &&
            IsOmsiForeground())
        {
            OpenChatInput();
        }
    }

    private int GetCurrentOmsiModifierMask()
    {
        var mask = 0;

        if (_pressedKeys.Contains(VkShift) ||
            _pressedKeys.Contains(VkLeftShift) ||
            _pressedKeys.Contains(VkRightShift))
        {
            mask |= NavBRHotkeyCatalog.OmsiShiftModifier;
        }

        if (_pressedKeys.Contains(VkControl) ||
            _pressedKeys.Contains(VkLeftControl) ||
            _pressedKeys.Contains(VkRightControl))
        {
            mask |= NavBRHotkeyCatalog.OmsiCtrlModifier;
        }

        return mask;
    }
}
