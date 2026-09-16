using System.Collections.Concurrent;
using Concentus;
using Concentus.Enums;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public sealed class VoiceChatService : IDisposable
{
    public const int SampleRate = 48000;
    public const int Channels = 1;
    public const int FrameMilliseconds = 20;
    public const int FrameSamples = SampleRate * FrameMilliseconds / 1000;
    private const int MaxOpusPacketBytes = 1275;
    private const int RemoteBufferMilliseconds = 300;

    private readonly IOpusEncoder _encoder;
    private readonly MixingSampleProvider _mixer;
    private readonly ConcurrentDictionary<string, RemoteVoiceStream> _remoteStreams =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RemoteVoicePreference> _remotePreferences =
        new(StringComparer.OrdinalIgnoreCase);

    private WaveInEvent _capture;
    private WaveOutEvent _output;
    private bool _started;
    private bool _pushToTalk;
    private bool _deafened;
    private long _sequence;

    public event Action<long, byte[]>? EncodedFrameReady;
    public event Action<string>? RemoteSpeakerActive;
    public event Action<string>? VoiceError;

    public bool IsPushToTalkActive => _pushToTalk;
    public bool IsDeafened => _deafened;
    public int InputDeviceNumber { get; private set; }
    public int OutputDeviceNumber { get; private set; }

    public VoiceChatService()
    {
        OpusCodecFactory.AttemptToUseNativeLibrary = false;

        _encoder = OpusCodecFactory.CreateEncoder(
            SampleRate,
            Channels,
            OpusApplication.OPUS_APPLICATION_VOIP);
        _encoder.Bitrate = 24000;
        _encoder.UseVBR = true;
        _encoder.UseInbandFEC = true;
        _encoder.PacketLossPercent = 5;
        _encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
        _encoder.Complexity = 5;

        _mixer = new MixingSampleProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels))
        {
            ReadFully = true
        };

        InputDeviceNumber = VoiceAudioDeviceCatalog.NormalizeInputDevice(0);
        OutputDeviceNumber = VoiceAudioDeviceCatalog.NormalizeOutputDevice(-1);
        _capture = CreateCapture(InputDeviceNumber);
        _output = CreateOutput(OutputDeviceNumber);
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        try
        {
            _output.Play();
            _capture.StartRecording();
            _started = true;
        }
        catch (Exception ex)
        {
            VoiceError?.Invoke(ex.Message);
        }
    }

    public void ConfigureDevices(int inputDeviceNumber, int outputDeviceNumber)
    {
        var safeInput = VoiceAudioDeviceCatalog.NormalizeInputDevice(inputDeviceNumber);
        var safeOutput = VoiceAudioDeviceCatalog.NormalizeOutputDevice(outputDeviceNumber);
        if (safeInput == InputDeviceNumber && safeOutput == OutputDeviceNumber)
        {
            return;
        }

        var restart = _started;
        if (restart)
        {
            Stop();
        }

        try
        {
            _capture.Dispose();
            _output.Dispose();
            InputDeviceNumber = safeInput;
            OutputDeviceNumber = safeOutput;
            _capture = CreateCapture(InputDeviceNumber);
            _output = CreateOutput(OutputDeviceNumber);

            if (restart)
            {
                Start();
            }
        }
        catch (Exception ex)
        {
            VoiceError?.Invoke(ex.Message);
        }
    }

    public void SetPushToTalk(bool active)
    {
        _pushToTalk = active && _started;
    }

    public void SetDeafened(bool deafened)
    {
        _deafened = deafened;
        foreach (var stream in _remoteStreams.Values)
        {
            ApplyPreference(stream.PlayerId, stream);
        }
    }

    public void SetRemoteMuted(string playerId, bool muted)
    {
        var normalized = NormalizePlayerId(playerId);
        if (normalized is null)
        {
            return;
        }

        _remotePreferences.AddOrUpdate(
            normalized,
            _ => new RemoteVoicePreference(muted, 1f),
            (_, current) => current with { Muted = muted });

        if (_remoteStreams.TryGetValue(normalized, out var stream))
        {
            ApplyPreference(normalized, stream);
        }
    }

    public bool IsRemoteMuted(string playerId)
    {
        var normalized = NormalizePlayerId(playerId);
        return normalized is not null &&
               _remotePreferences.TryGetValue(normalized, out var preference) &&
               preference.Muted;
    }

    public void SetRemoteGain(string playerId, double gain)
    {
        var normalized = NormalizePlayerId(playerId);
        if (normalized is null)
        {
            return;
        }

        var safeGain = (float)Math.Clamp(double.IsFinite(gain) ? gain : 1d, 0d, 2d);
        _remotePreferences.AddOrUpdate(
            normalized,
            _ => new RemoteVoicePreference(false, safeGain),
            (_, current) => current with { Gain = safeGain });

        if (_remoteStreams.TryGetValue(normalized, out var stream))
        {
            ApplyPreference(normalized, stream);
        }
    }

    public double GetRemoteGain(string playerId)
    {
        var normalized = NormalizePlayerId(playerId);
        return normalized is not null &&
               _remotePreferences.TryGetValue(normalized, out var preference)
            ? preference.Gain
            : 1d;
    }

    public void Receive(VoiceFrame frame)
    {
        if (!_started ||
            _deafened ||
            frame.OpusPayload is null ||
            frame.OpusPayload.Length == 0 ||
            !VoiceChannelSession.ShouldReceive(frame))
        {
            return;
        }

        var playerId = NormalizePlayerId(frame.PlayerId);
        if (playerId is null || IsRemoteMuted(playerId))
        {
            return;
        }

        try
        {
            var stream = _remoteStreams.GetOrAdd(
                playerId,
                id => CreateRemoteStream(id));

            var pcm = new short[5760 * Channels];
            var decodedSamples = stream.Decoder.Decode(
                frame.OpusPayload,
                pcm,
                5760,
                decode_fec: false);

            if (decodedSamples <= 0)
            {
                return;
            }

            var byteCount = decodedSamples * Channels * sizeof(short);
            var bytes = new byte[byteCount];
            Buffer.BlockCopy(pcm, 0, bytes, 0, byteCount);
            stream.Buffer.AddSamples(bytes, 0, bytes.Length);
            RemoteSpeakerActive?.Invoke(playerId);
        }
        catch (Exception ex)
        {
            VoiceError?.Invoke(ex.Message);
        }
    }

    public void RemoveRemotePlayer(string playerId)
    {
        var normalized = NormalizePlayerId(playerId);
        if (normalized is null)
        {
            return;
        }

        if (_remoteStreams.TryRemove(normalized, out var stream))
        {
            _mixer.RemoveMixerInput(stream.SampleProvider);
            stream.Dispose();
        }
    }

    public void Stop()
    {
        _pushToTalk = false;
        if (!_started)
        {
            return;
        }

        _started = false;

        try
        {
            _capture.StopRecording();
        }
        catch
        {
        }

        try
        {
            _output.Stop();
        }
        catch
        {
        }

        foreach (var playerId in _remoteStreams.Keys.ToArray())
        {
            RemoveRemotePlayer(playerId);
        }
    }

    public void Dispose()
    {
        Stop();
        _capture.Dispose();
        _output.Dispose();
        _encoder.Dispose();
    }

    private WaveInEvent CreateCapture(int deviceNumber)
    {
        var capture = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(SampleRate, 16, Channels),
            BufferMilliseconds = FrameMilliseconds,
            NumberOfBuffers = 3
        };
        capture.DataAvailable += Capture_DataAvailable;
        capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null)
            {
                VoiceError?.Invoke(e.Exception.Message);
            }
        };
        return capture;
    }

    private WaveOutEvent CreateOutput(int deviceNumber)
    {
        var output = new WaveOutEvent
        {
            DeviceNumber = deviceNumber,
            DesiredLatency = 80,
            NumberOfBuffers = 3
        };
        output.Init(new SampleToWaveProvider(_mixer));
        return output;
    }

    private void Capture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_pushToTalk || e.BytesRecorded < FrameSamples * sizeof(short))
        {
            return;
        }

        try
        {
            var sampleCount = e.BytesRecorded / sizeof(short);
            var samples = new short[sampleCount];
            Buffer.BlockCopy(e.Buffer, 0, samples, 0, sampleCount * sizeof(short));
            var encoded = new byte[MaxOpusPacketBytes];

            for (var offset = 0; offset + FrameSamples <= sampleCount; offset += FrameSamples)
            {
                var length = _encoder.Encode(
                    samples.AsSpan(offset, FrameSamples),
                    FrameSamples,
                    encoded,
                    MaxOpusPacketBytes);

                if (length <= 0)
                {
                    continue;
                }

                var payload = encoded.AsSpan(0, length).ToArray();
                EncodedFrameReady?.Invoke(Interlocked.Increment(ref _sequence), payload);
            }
        }
        catch (Exception ex)
        {
            VoiceError?.Invoke(ex.Message);
        }
    }

    private RemoteVoiceStream CreateRemoteStream(string playerId)
    {
        var decoder = OpusCodecFactory.CreateDecoder(SampleRate, Channels);
        var waveFormat = new WaveFormat(SampleRate, 16, Channels);
        var buffer = new BufferedWaveProvider(
            waveFormat,
            TimeSpan.FromMilliseconds(RemoteBufferMilliseconds))
        {
            DiscardOnBufferOverflow = true,
            ReadFully = false
        };
        var volume = new VolumeSampleProvider(buffer.ToSampleProvider());
        var stream = new RemoteVoiceStream(playerId, decoder, buffer, volume);
        ApplyPreference(playerId, stream);
        _mixer.AddMixerInput(stream.SampleProvider);
        return stream;
    }

    private void ApplyPreference(string playerId, RemoteVoiceStream stream)
    {
        var preference = _remotePreferences.TryGetValue(playerId, out var stored)
            ? stored
            : RemoteVoicePreference.Default;
        stream.Volume.Volume = _deafened || preference.Muted ? 0f : preference.Gain;
    }

    private static string? NormalizePlayerId(string? playerId)
    {
        var normalized = playerId?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record RemoteVoicePreference(bool Muted, float Gain)
    {
        public static RemoteVoicePreference Default { get; } = new(false, 1f);
    }

    private sealed class RemoteVoiceStream(
        string playerId,
        IOpusDecoder decoder,
        BufferedWaveProvider buffer,
        VolumeSampleProvider volume) : IDisposable
    {
        public string PlayerId { get; } = playerId;
        public IOpusDecoder Decoder { get; } = decoder;
        public BufferedWaveProvider Buffer { get; } = buffer;
        public VolumeSampleProvider Volume { get; } = volume;
        public ISampleProvider SampleProvider => Volume;

        public void Dispose() => Decoder.Dispose();
    }
}
