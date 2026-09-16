using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

public sealed record VoiceQualitySnapshot(
    int ActiveStreams,
    long ReceivedPackets,
    long PlayedPackets,
    long FecRecoveredPackets,
    long EstimatedLostPackets,
    long LatePackets,
    long DuplicatePackets,
    double AverageJitterMilliseconds,
    int TargetBufferMilliseconds)
{
    public static VoiceQualitySnapshot Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0d,
        40);

    public double EstimatedLossPercent
    {
        get
        {
            var total = ReceivedPackets + EstimatedLostPackets;
            return total <= 0 ? 0d : EstimatedLostPackets * 100d / total;
        }
    }

    public bool IsDegraded =>
        AverageJitterMilliseconds >= 45d ||
        EstimatedLossPercent >= 5d;
}

internal enum VoiceDecodeMode
{
    Normal,
    FecRecovery
}

internal readonly record struct BufferedVoicePacket(
    VoiceFrame Frame,
    VoiceDecodeMode Mode);

internal sealed class AdaptiveVoiceJitterBuffer
{
    private const int FrameMilliseconds = VoiceChatService.FrameMilliseconds;
    private const int MinimumTargetFrames = 2;
    private const int MaximumTargetFrames = 6;
    private const int MaximumPendingPackets = 32;
    private const int MaximumRecoverableGapFrames = 4;
    private static readonly TimeSpan TalkspurtResetAfter = TimeSpan.FromMilliseconds(750);

    private readonly object _sync = new();
    private readonly SortedDictionary<long, QueuedPacket> _pending = new();

    private long? _nextSequence;
    private DateTimeOffset? _lastArrivalUtc;
    private DateTimeOffset? _lastSourceTimestampUtc;
    private bool _started;
    private double _jitterMilliseconds;
    private int _targetFrames = MinimumTargetFrames;

    private long _receivedPackets;
    private long _playedPackets;
    private long _fecRecoveredPackets;
    private long _estimatedLostPackets;
    private long _latePackets;
    private long _duplicatePackets;

    public void Enqueue(VoiceFrame frame, DateTimeOffset arrivalUtc)
    {
        lock (_sync)
        {
            if (_lastArrivalUtc is { } lastArrival &&
                arrivalUtc - lastArrival >= TalkspurtResetAfter &&
                _pending.Count == 0)
            {
                ResetSequenceState();
            }

            UpdateJitter(frame.TimestampUtc, arrivalUtc);
            _lastArrivalUtc = arrivalUtc;
            _lastSourceTimestampUtc = frame.TimestampUtc;

            if (_nextSequence is null)
            {
                _nextSequence = frame.Sequence;
            }

            if (frame.Sequence < _nextSequence.Value)
            {
                _latePackets++;
                return;
            }

            if (_pending.ContainsKey(frame.Sequence))
            {
                _duplicatePackets++;
                return;
            }

            if (frame.Sequence - _nextSequence.Value > MaximumPendingPackets)
            {
                var skipped = frame.Sequence - _nextSequence.Value;
                _estimatedLostPackets += skipped;
                ResetSequenceState(frame.Sequence);
            }

            _pending[frame.Sequence] = new QueuedPacket(frame, arrivalUtc);
            _receivedPackets++;

            while (_pending.Count > MaximumPendingPackets)
            {
                var first = _pending.First();
                _pending.Remove(first.Key);
                if (_nextSequence is { } next && first.Key >= next)
                {
                    _estimatedLostPackets += first.Key - next + 1;
                    _nextSequence = first.Key + 1;
                }
            }
        }
    }

    public BufferedVoicePacket? TryTake(DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            if (_nextSequence is null || _pending.Count == 0)
            {
                return null;
            }

            if (!_started)
            {
                var oldest = _pending.First().Value.ArrivalUtc;
                var targetDelay = TimeSpan.FromMilliseconds(_targetFrames * FrameMilliseconds);
                if (_pending.Count < _targetFrames && nowUtc - oldest < targetDelay)
                {
                    return null;
                }
                _started = true;
            }

            var nextSequence = _nextSequence.Value;
            if (_pending.TryGetValue(nextSequence, out var expected))
            {
                _pending.Remove(nextSequence);
                _nextSequence = nextSequence + 1;
                _playedPackets++;
                return new BufferedVoicePacket(expected.Frame, VoiceDecodeMode.Normal);
            }

            var first = _pending.First();
            var gap = first.Key - nextSequence;
            if (gap <= 0)
            {
                _pending.Remove(first.Key);
                _latePackets++;
                return null;
            }

            var targetWait = TimeSpan.FromMilliseconds(_targetFrames * FrameMilliseconds);
            if (nowUtc - first.Value.ArrivalUtc < targetWait)
            {
                return null;
            }

            if (gap == 1)
            {
                _estimatedLostPackets++;
                _nextSequence = nextSequence + 1;
                return new BufferedVoicePacket(first.Value.Frame, VoiceDecodeMode.FecRecovery);
            }

            if (gap > MaximumRecoverableGapFrames)
            {
                _estimatedLostPackets += gap;
                _nextSequence = first.Key;
                _started = false;
                return null;
            }

            _estimatedLostPackets += gap;
            _nextSequence = first.Key;
            return null;
        }
    }

    public void ReportFecRecovered()
    {
        lock (_sync)
        {
            _fecRecoveredPackets++;
            _playedPackets++;
        }
    }

    public VoiceQualitySnapshot Snapshot()
    {
        lock (_sync)
        {
            return new VoiceQualitySnapshot(
                ActiveStreams: 1,
                ReceivedPackets: _receivedPackets,
                PlayedPackets: _playedPackets,
                FecRecoveredPackets: _fecRecoveredPackets,
                EstimatedLostPackets: _estimatedLostPackets,
                LatePackets: _latePackets,
                DuplicatePackets: _duplicatePackets,
                AverageJitterMilliseconds: _jitterMilliseconds,
                TargetBufferMilliseconds: _targetFrames * FrameMilliseconds);
        }
    }

    private void UpdateJitter(DateTimeOffset sourceTimestampUtc, DateTimeOffset arrivalUtc)
    {
        if (_lastArrivalUtc is not { } lastArrival ||
            _lastSourceTimestampUtc is not { } lastSource)
        {
            return;
        }

        var arrivalDelta = (arrivalUtc - lastArrival).TotalMilliseconds;
        var sourceDelta = (sourceTimestampUtc - lastSource).TotalMilliseconds;
        if (!double.IsFinite(arrivalDelta) ||
            !double.IsFinite(sourceDelta) ||
            arrivalDelta < 0d ||
            sourceDelta < 0d ||
            arrivalDelta > 2000d ||
            sourceDelta > 2000d)
        {
            return;
        }

        var variation = Math.Abs(arrivalDelta - sourceDelta);
        _jitterMilliseconds += (variation - _jitterMilliseconds) / 16d;
        _targetFrames = _jitterMilliseconds switch
        {
            < 8d => 2,
            < 18d => 3,
            < 35d => 4,
            < 60d => 5,
            _ => 6
        };
        _targetFrames = Math.Clamp(_targetFrames, MinimumTargetFrames, MaximumTargetFrames);
    }

    private void ResetSequenceState(long? nextSequence = null)
    {
        _pending.Clear();
        _nextSequence = nextSequence;
        _started = false;
    }

    private sealed record QueuedPacket(
        VoiceFrame Frame,
        DateTimeOffset ArrivalUtc);
}
