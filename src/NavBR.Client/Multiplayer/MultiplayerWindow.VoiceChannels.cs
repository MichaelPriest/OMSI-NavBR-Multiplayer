using System.Windows;
using System.Windows.Controls;
using NavBR.Client.Localization;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public partial class MultiplayerWindow
{
    private Button? _voiceChannelButton;
    private bool _voiceChannelLifecycleHooked;

    private void InitializeVoiceChannels()
    {
        var channel = VoiceChannelSession.NormalizeChannel(_settings.VoiceChannel);
        var radius = Math.Clamp(
            double.IsFinite(_settings.VoiceProximityMeters) ? _settings.VoiceProximityMeters : 120d,
            20d,
            1000d);

        _settings = _settings with
        {
            VoiceChannel = channel,
            VoiceProximityMeters = radius
        };
        VoiceChannelSession.Configure(channel, radius, ShouldReceiveProximityVoice);
        _voiceChat.SetDeafened(_settings.VoiceDeafened);

        if (_voiceChannelButton is null && VoiceEnabledCheckBox.Parent is Grid settingsGrid)
        {
            Grid.SetColumnSpan(VoiceEnabledCheckBox, 1);
            _voiceChannelButton = new Button
            {
                MinWidth = 150,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 11)
            };
            _voiceChannelButton.Click += VoiceChannelButton_Click;
            Grid.SetRow(_voiceChannelButton, 2);
            Grid.SetColumn(_voiceChannelButton, 3);
            settingsGrid.Children.Add(_voiceChannelButton);
        }

        RenderVoiceChannelButton();

        if (!_voiceChannelLifecycleHooked)
        {
            _voiceChannelLifecycleHooked = true;
            Closed += VoiceChannels_WindowClosed;
        }
    }

    private void VoiceChannelButton_Click(object sender, RoutedEventArgs e)
    {
        var remotePlayers = _players.Values
            .Where(player => !string.Equals(player.PlayerId, _settings.PlayerId, StringComparison.OrdinalIgnoreCase))
            .Select(player => (player.PlayerId, player.DisplayName))
            .ToArray();

        var dialog = new VoiceOptionsWindow(
            _settings.VoiceChannel,
            _settings.VoiceProximityMeters,
            _settings.VoiceDeafened,
            remotePlayers,
            _voiceChat)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _settings = _settings with
        {
            VoiceChannel = dialog.SelectedChannel,
            VoiceProximityMeters = dialog.ProximityMeters,
            VoiceDeafened = dialog.Deafened
        };
        MultiplayerSettingsStore.Save(_settings);
        VoiceChannelSession.Configure(
            _settings.VoiceChannel,
            _settings.VoiceProximityMeters,
            ShouldReceiveProximityVoice);
        _voiceChat.SetDeafened(_settings.VoiceDeafened);
        RenderVoiceChannelButton();
    }

    private bool ShouldReceiveProximityVoice(VoiceFrame frame)
    {
        var local = _telemetrySource();
        if (local is null || !_remoteTelemetry.TryGetValue(frame.PlayerId, out var remote))
        {
            return false;
        }

        var localCompatibility = _activeMapSource()?.CompatibilityId ?? local.MapCompatibilityId;
        var remoteCompatibility = remote.MapCompatibilityId;
        if (!string.IsNullOrWhiteSpace(localCompatibility) &&
            !string.IsNullOrWhiteSpace(remoteCompatibility) &&
            !string.Equals(localCompatibility, remoteCompatibility, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if ((string.IsNullOrWhiteSpace(localCompatibility) || string.IsNullOrWhiteSpace(remoteCompatibility)) &&
            (!string.IsNullOrWhiteSpace(local.MapName) || !string.IsNullOrWhiteSpace(remote.MapName)) &&
            !string.Equals(local.MapName, remote.MapName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!double.IsFinite(local.X) || !double.IsFinite(local.Y) ||
            !double.IsFinite(remote.X) || !double.IsFinite(remote.Y))
        {
            return false;
        }

        var dx = remote.X - local.X;
        var dy = remote.Y - local.Y;
        var distanceMeters = Math.Sqrt(dx * dx + dy * dy);
        return distanceMeters <= VoiceChannelSession.ProximityMeters;
    }

    private void RenderVoiceChannelButton()
    {
        if (_voiceChannelButton is null)
        {
            return;
        }

        var label = VoiceChannelSession.NormalizeChannel(_settings.VoiceChannel) switch
        {
            "company" => T("Empresa/equipe", "Company/team", "Empresa/equipo", "Unternehmen/Team", "Entreprise/équipe"),
            "dispatch" => T("CCO", "Dispatcher", "CCO", "Leitstelle", "CCO"),
            "proximity" => T(
                $"Proximidade {_settings.VoiceProximityMeters:0} m",
                $"Proximity {_settings.VoiceProximityMeters:0} m",
                $"Proximidad {_settings.VoiceProximityMeters:0} m",
                $"Nähe {_settings.VoiceProximityMeters:0} m",
                $"Proximité {_settings.VoiceProximityMeters:0} m"),
            _ => T("Geral", "General", "General", "Allgemein", "Général")
        };

        var deafen = _settings.VoiceDeafened
            ? $" • {T("silenciado", "deafened", "silenciado", "stumm", "sourdine")}"
            : string.Empty;
        _voiceChannelButton.Content = $"{T("Voz", "Voice", "Voz", "Sprache", "Voix")}: {label}{deafen}";
    }

    private void VoiceChannels_WindowClosed(object? sender, EventArgs e)
    {
        VoiceChannelSession.SetProximityFilter(null);
    }

    private static string T(string pt, string en, string es, string de, string fr) =>
        LocalizationService.CurrentCulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "pt" => pt,
            "es" => es,
            "de" => de,
            "fr" => fr,
            _ => en
        };
}
