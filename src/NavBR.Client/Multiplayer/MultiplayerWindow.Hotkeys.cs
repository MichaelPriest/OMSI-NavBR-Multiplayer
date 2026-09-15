using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Localization;
using NavBR.Client.Overlay;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private bool _hotkeyUiReady;

    public string CurrentChatHotkey => _settings.ChatHotkey;
    public string CurrentVoiceHotkey => _settings.VoiceHotkey;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Loaded += MultiplayerWindow_HotkeysLoaded;
    }

    private void MultiplayerWindow_HotkeysLoaded(object sender, RoutedEventArgs e)
    {
        if (_hotkeyUiReady)
        {
            return;
        }

        var options = NavBRHotkeyCatalog.Options.Select(option => option.Name).ToArray();
        ChatHotkeyComboBox.ItemsSource = options;
        VoiceHotkeyComboBox.ItemsSource = options;
        ChatHotkeyComboBox.SelectedItem = _settings.ChatHotkey;
        VoiceHotkeyComboBox.SelectedItem = _settings.VoiceHotkey;
        _hotkeyUiReady = true;
        RefreshHotkeyUiText();
    }

    private void HotkeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_hotkeyUiReady ||
            ChatHotkeyComboBox.SelectedItem is not string chatHotkey ||
            VoiceHotkeyComboBox.SelectedItem is not string voiceHotkey)
        {
            return;
        }

        if (string.Equals(chatHotkey, voiceHotkey, StringComparison.OrdinalIgnoreCase))
        {
            if (ReferenceEquals(sender, ChatHotkeyComboBox))
            {
                voiceHotkey = PickDistinctHotkey(
                    chatHotkey,
                    NavBRHotkeyCatalog.DefaultVoiceHotkey,
                    NavBRHotkeyCatalog.DefaultChatHotkey);
            }
            else
            {
                chatHotkey = PickDistinctHotkey(
                    voiceHotkey,
                    NavBRHotkeyCatalog.DefaultChatHotkey,
                    NavBRHotkeyCatalog.DefaultVoiceHotkey);
            }

            _hotkeyUiReady = false;
            ChatHotkeyComboBox.SelectedItem = chatHotkey;
            VoiceHotkeyComboBox.SelectedItem = voiceHotkey;
            _hotkeyUiReady = true;
        }

        _settings = _settings with
        {
            ChatHotkey = chatHotkey,
            VoiceHotkey = voiceHotkey
        };
        MultiplayerSettingsStore.Save(_settings);
        RefreshHotkeyUiText();
    }

    private static string PickDistinctHotkey(string reserved, params string[] preferred)
    {
        foreach (var candidate in preferred)
        {
            if (!string.Equals(candidate, reserved, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return NavBRHotkeyCatalog.Options
            .Select(option => option.Name)
            .First(option => !string.Equals(option, reserved, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshHotkeyUiText()
    {
        ChatHotkeyLabelText.Text = LocalizationService.Get("MultiplayerChat");
        VoiceHotkeyLabelText.Text = "PTT";

        var chat = _settings.ChatHotkey;
        var voice = _settings.VoiceHotkey;
        VoiceEnabledCheckBox.Content = LocalizationService.Get("MultiplayerVoiceEnabled")
            .Replace("F10", "{VOICE}", StringComparison.OrdinalIgnoreCase)
            .Replace("{VOICE}", voice, StringComparison.Ordinal);

        FooterText.Text = LocalizationService.Get("MultiplayerPeerFooter")
            .Replace("F9", "{CHAT}", StringComparison.OrdinalIgnoreCase)
            .Replace("F10", "{VOICE}", StringComparison.OrdinalIgnoreCase)
            .Replace("{CHAT}", chat, StringComparison.Ordinal)
            .Replace("{VOICE}", voice, StringComparison.Ordinal);
    }
}
