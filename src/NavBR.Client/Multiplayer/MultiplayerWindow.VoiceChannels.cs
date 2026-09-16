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
        var inputDevice = VoiceAudioDeviceCatalog.NormalizeInputDevice(_settings.VoiceInputDeviceNumber);
        var outputDevice = VoiceAudioDeviceCatalog.NormalizeOutputDevice(_settings.VoiceOutputDeviceNumber);

        _settings = _settings with
        {
            VoiceChannel = channel,
            VoiceProximityMeters = radius,
            VoiceInputDeviceNumber = inputDevice,
            VoiceOutputDeviceNumber = outputDevice
        };
        VoiceChannelSession.Configure(channel, radius, ShouldReceiveProximityVoice);
        _voiceChat.ConfigureDevices(inputDevice, outputDevice);
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
            _voiceChat.VoiceQualityChanged += VoiceChat_QualityChanged;
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
            _settings.VoiceInputDeviceNumber,
            _settings.VoiceOutputDeviceNumber,
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
            VoiceDeafened = dialog.Deafened,
            VoiceInputDeviceNumber = dialog.InputDeviceNumber,
            VoiceOutputDeviceNumber = dialog.OutputDeviceNumber
        };
        MultiplayerSettingsStore.Save(_settings);
        VoiceChannelSession.Configure(
            _settings.VoiceChannel,
            _settings.VoiceProximityMeters,
            ShouldReceiveProximityVoice);
        _voiceChat.ConfigureDevices(
            _settings.VoiceInputDeviceNumber,
            _settings.VoiceOutputDeviceNumber);
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

        var quality = _voiceChat.GetQualitySnapshot();
        var qualitySuffix = quality.ActiveStreams > 0
            ? $" • {(quality.IsDegraded ? "⚠" : "✓")} {quality.AverageJitterMilliseconds:0} ms / {quality.EstimatedLossPercent:0.#}%"
            : string.Empty;

        _voiceChannelButton.Content = $"{T("Voz", "Voice", "Voz", "Sprache", "Voix")}: {label}{deafen}{qualitySuffix}";
        _voiceChannelButton.ToolTip = quality.ActiveStreams > 0
            ? T(
                $"Qualidade da voz: jitter {quality.AverageJitterMilliseconds:0} ms • perda estimada {quality.EstimatedLossPercent:0.#}% • FEC recuperados {quality.FecRecoveredPackets} • buffer {quality.TargetBufferMilliseconds} ms",
                $"Voice quality: jitter {quality.AverageJitterMilliseconds:0} ms • estimated loss {quality.EstimatedLossPercent:0.#}% • FEC recovered {quality.FecRecoveredPackets} • buffer {quality.TargetBufferMilliseconds} ms",
                $"Calidad de voz: jitter {quality.AverageJitterMilliseconds:0} ms • pérdida estimada {quality.EstimatedLossPercent:0.#}% • FEC recuperados {quality.FecRecoveredPackets} • búfer {quality.TargetBufferMilliseconds} ms",
                $"Sprachqualität: Jitter {quality.AverageJitterMilliseconds:0} ms • geschätzter Verlust {quality.EstimatedLossPercent:0.#}% • FEC wiederhergestellt {quality.FecRecoveredPackets} • Puffer {quality.TargetBufferMilliseconds} ms",
                $"Qualité vocale : jitter {quality.AverageJitterMilliseconds:0} ms • perte estimée {quality.EstimatedLossPercent:0.#}% • FEC récupérés {quality.FecRecoveredPackets} • tampon {quality.TargetBufferMilliseconds} ms")
            : T(
                "A qualidade aparecerá quando chegar áudio de outro jogador.",
                "Voice quality appears after audio arrives from another player.",
                "La calidad aparecerá cuando llegue audio de otro jugador.",
                "Die Sprachqualität erscheint nach dem Empfang von Audio eines anderen Spielers.",
                "La qualité apparaîtra après réception de l’audio d’un autre joueur.");
    }

    private void VoiceChat_QualityChanged(VoiceQualitySnapshot snapshot)
    {
        if (Dispatcher.CheckAccess())
        {
            RenderVoiceChannelButton();
            return;
        }

        _ = Dispatcher.BeginInvoke(RenderVoiceChannelButton);
    }

    private void VoiceChannels_WindowClosed(object? sender, EventArgs e)
    {
        VoiceChannelSession.SetProximityFilter(null);
        _voiceChat.VoiceQualityChanged -= VoiceChat_QualityChanged;
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
