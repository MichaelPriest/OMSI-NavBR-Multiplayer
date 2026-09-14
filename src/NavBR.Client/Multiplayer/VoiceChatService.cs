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
    private readonly WaveInEvent _capture;
    private readonly WaveOutEvent _output;
    private readonly MixingSampleProvider _mixer;
    private readonly ConcurrentDictionary<string, RemoteVoiceStream> _remoteStreams =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _started;
    private bool _pushToTalk;
    private long _sequence;

    public event Action<long, byte[]>? EncodedFrameReady;
    public event Action<string>? RemoteSpeakerActive;
    public event Action<string>? VoiceError;

    public bool IsPushToTalkActive => _pushToTalk;

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

        _capture = new WaveInEvent
        {
            WaveFormat = new WaveFormat(SampleRate, 16, Channels),
            BufferMilliseconds = FrameMilliseconds,
            NumberOfBuffers = 3
        };
        _capture.DataAvailable += Capture_DataAvailable;
        _capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null)
            {
                VoiceError?.Invoke(e.Exception.Message);
            }
        };

        _mixer = new MixingSampleProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels))
        {
            ReadFully = true
        };

        _output = new WaveOutEvent
        {
            DesiredLatency = 80,
            NumberOfBuffers = 3
        };
        _output.Init(new SampleToWaveProvider(_mixer));
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

    public void SetPushToTalk(bool active)
    {
        _pushToTalk = active && _started;
    }

    public void Receive(VoiceFrame frame)
    {
        if (!_started || frame.OpusPayload is null || frame.OpusPayload.Length == 0)
        {
            return;
        }

        try
        {
            var stream = _remoteStreams.GetOrAdd(
                frame.PlayerId,
                _ => CreateRemoteStream());

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
            RemoteSpeakerActive?.Invoke(frame.PlayerId);
        }
        catch (Exception ex)
        {
            VoiceError?.Invoke(ex.Message);
        }
    }

    public void RemoveRemotePlayer(string playerId)
    {
        if (_remoteStreams.TryRemove(playerId, out var stream))
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

    private RemoteVoiceStream CreateRemoteStream()
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
        var sampleProvider = buffer.ToSampleProvider();
        _mixer.AddMixerInput(sampleProvider);
        return new RemoteVoiceStream(decoder, buffer, sampleProvider);
    }

    private sealed class RemoteVoiceStream(
        IOpusDecoder decoder,
        BufferedWaveProvider buffer,
        ISampleProvider sampleProvider) : IDisposable
    {
        public IOpusDecoder Decoder { get; } = decoder;
        public BufferedWaveProvider Buffer { get; } = buffer;
        public ISampleProvider SampleProvider { get; } = sampleProvider;

        public void Dispose() => Decoder.Dispose();
    }
}
