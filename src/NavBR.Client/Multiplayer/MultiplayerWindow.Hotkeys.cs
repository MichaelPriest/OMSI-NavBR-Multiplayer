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

        _settings = _settings with
        {
            ChatHotkey = chatHotkey,
            VoiceHotkey = voiceHotkey
        };
        MultiplayerSettingsStore.Save(_settings);
        RefreshHotkeyUiText();

        if (string.Equals(chatHotkey, voiceHotkey, StringComparison.OrdinalIgnoreCase))
        {
            StatusDetailText.Text = LocalizationService.CurrentCulture.TwoLetterISOLanguageName switch
            {
                "pt" => "Escolha teclas diferentes para chat e voz.",
                "es" => "Elige teclas diferentes para chat y voz.",
                "de" => "Für Chat und Sprache unterschiedliche Tasten wählen.",
                "fr" => "Choisissez des touches différentes pour le chat et la voix.",
                _ => "Choose different keys for chat and voice."
            };
        }
    }

    private void RefreshHotkeyUiText()
    {
        ChatHotkeyLabelText.Text = LocalizationService.Get("MultiplayerChat");
        VoiceHotkeyLabelText.Text = "PTT";

        var chat = _settings.ChatHotkey;
        var voice = _settings.VoiceHotkey;
        VoiceEnabledCheckBox.Content = LocalizationService.Get("MultiplayerVoiceEnabled")
            .Replace("F10", voice, StringComparison.OrdinalIgnoreCase);
        FooterText.Text = LocalizationService.Get("MultiplayerPeerFooter")
            .Replace("F9", chat, StringComparison.OrdinalIgnoreCase)
            .Replace("F10", voice, StringComparison.OrdinalIgnoreCase);
    }
}
