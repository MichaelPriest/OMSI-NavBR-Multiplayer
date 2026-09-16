using System.Diagnostics;
using System.Text.Json;
using NavBR.Client.PluginBridge;
using NavBR.Shared.PluginBridge;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Ghost;

internal sealed class GhostReplayPlayer
{
    private CancellationTokenSource? _playbackCts;

    public bool IsPlaying => _playbackCts is not null;

    public async Task<GhostReplayDocument> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            Path.GetFullPath(path),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        var document = await JsonSerializer.DeserializeAsync<GhostReplayDocument>(
            stream,
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Invalid NavBR ghost replay file.");

        if (document.Metadata.FormatVersion != GhostReplayFormat.Version)
        {
            throw new InvalidDataException(
                $"Unsupported ghost replay format: {document.Metadata.FormatVersion}.");
        }

        if (document.Frames.Count < 2)
        {
            throw new InvalidDataException("Ghost replay has insufficient frames.");
        }

        return document;
    }

    public async Task PlayAsync(
        string path,
        double playbackSpeed = 1d,
        bool loop = false,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(playbackSpeed) || playbackSpeed is < 0.1 or > 4d)
        {
            throw new ArgumentOutOfRangeException(nameof(playbackSpeed));
        }

        Stop();
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _playbackCts = linked;

        try
        {
            var document = await LoadAsync(path, linked.Token);
            do
            {
                await PlayDocumentOnceAsync(document, playbackSpeed, linked.Token);
            }
            while (loop && !linked.IsCancellationRequested);
        }
        finally
        {
            if (ReferenceEquals(_playbackCts, linked))
            {
                _playbackCts = null;
            }

            linked.Dispose();
        }
    }

    public void Stop()
    {
        var current = Interlocked.Exchange(ref _playbackCts, null);
        if (current is null)
        {
            return;
        }

        try
        {
            current.Cancel();
        }
        catch
        {
        }
    }

    private static async Task PlayDocumentOnceAsync(
        GhostReplayDocument document,
        double playbackSpeed,
        CancellationToken cancellationToken)
    {
        var ghostId = $"ghost-{Guid.NewGuid():N}";
        var first = document.Frames[0].Telemetry with
        {
            PlayerId = ghostId,
            Timestamp = DateTimeOffset.UtcNow
        };

        var spawn = await OmsiPluginBridgeRelay.SpawnGhostVehicleAsync(
            ghostId,
            first,
            cancellationToken);

        if (spawn is null)
        {
            throw new InvalidOperationException("OMSI plugin bridge is not connected.");
        }

        if (spawn.Success != true)
        {
            throw new InvalidOperationException(
                spawn.ErrorMessage ?? "The OMSI plugin rejected ghost spawning.");
        }

        var stopwatch = Stopwatch.StartNew();
        var durationMs = document.Frames[^1].OffsetMilliseconds;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sourceMs = stopwatch.Elapsed.TotalMilliseconds * playbackSpeed;
                if (sourceMs >= durationMs)
                {
                    break;
                }

                var state = Interpolate(document.Frames, sourceMs, ghostId);
                var result = await OmsiPluginBridgeRelay.UpdateGhostVehicleAsync(
                    ghostId,
                    state,
                    cancellationToken);

                if (result is { Success: false })
                {
                    throw new InvalidOperationException(
                        result.ErrorMessage ?? "The OMSI plugin rejected a ghost update.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }
        }
        finally
        {
            await OmsiPluginBridgeRelay.DespawnGhostVehicleAsync(
                ghostId,
                CancellationToken.None);
        }
    }

    private static VehicleTelemetry Interpolate(
        IReadOnlyList<GhostReplayFrame> frames,
        double offsetMilliseconds,
        string ghostId)
    {
        var upperIndex = FindUpperFrameIndex(frames, offsetMilliseconds);
        var lowerIndex = Math.Max(0, upperIndex - 1);
        var lower = frames[lowerIndex];
        var upper = frames[Math.Min(upperIndex, frames.Count - 1)];

        if (lower.OffsetMilliseconds == upper.OffsetMilliseconds)
        {
            return lower.Telemetry with
            {
                PlayerId = ghostId,
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        var t = Math.Clamp(
            (offsetMilliseconds - lower.OffsetMilliseconds) /
            (upper.OffsetMilliseconds - lower.OffsetMilliseconds),
            0d,
            1d);

        var a = lower.Telemetry;
        var b = upper.Telemetry;
        return a with
        {
            PlayerId = ghostId,
            Timestamp = DateTimeOffset.UtcNow,
            X = Lerp(a.X, b.X, t),
            Y = Lerp(a.Y, b.Y, t),
            Z = Lerp(a.Z, b.Z, t),
            HeadingDegrees = LerpHeading(a.HeadingDegrees, b.HeadingDegrees, t),
            SpeedKph = Lerp(a.SpeedKph, b.SpeedKph, t),
            AccelerationMps2 = LerpNullable(a.AccelerationMps2, b.AccelerationMps2, t),
            FuelPercent = LerpNullable(a.FuelPercent, b.FuelPercent, t),
            ThrottlePercent = LerpNullable(a.ThrottlePercent, b.ThrottlePercent, t),
            BrakePercent = LerpNullable(a.BrakePercent, b.BrakePercent, t),
            SteeringDegrees = LerpNullable(a.SteeringDegrees, b.SteeringDegrees, t),
            GridX = t < 0.5 ? a.GridX : b.GridX,
            GridY = t < 0.5 ? a.GridY : b.GridY,
            TileX = LerpNullable(a.TileX, b.TileX, t),
            TileY = LerpNullable(a.TileY, b.TileY, t),
            Line = t < 0.5 ? a.Line : b.Line,
            Route = t < 0.5 ? a.Route : b.Route,
            NextStopName = t < 0.5 ? a.NextStopName : b.NextStopName,
            DestinationName = t < 0.5 ? a.DestinationName : b.DestinationName,
            DelaySeconds = t < 0.5 ? a.DelaySeconds : b.DelaySeconds,
            CurrentStopIndex = t < 0.5 ? a.CurrentStopIndex : b.CurrentStopIndex,
            Doors = t < 0.5 ? a.Doors : b.Doors,
            Lights = t < 0.5 ? a.Lights : b.Lights,
            TurnSignal = t < 0.5 ? a.TurnSignal : b.TurnSignal,
            HornActive = t < 0.5 ? a.HornActive : b.HornActive,
            WipersActive = t < 0.5 ? a.WipersActive : b.WipersActive,
            ParkingBrakeActive = t < 0.5 ? a.ParkingBrakeActive : b.ParkingBrakeActive,
            ReverseGear = t < 0.5 ? a.ReverseGear : b.ReverseGear
        };
    }

    private static int FindUpperFrameIndex(
        IReadOnlyList<GhostReplayFrame> frames,
        double offsetMilliseconds)
    {
        var low = 0;
        var high = frames.Count - 1;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (frames[mid].OffsetMilliseconds < offsetMilliseconds)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static double? LerpNullable(double? a, double? b, double t)
    {
        if (a is not double av)
        {
            return b;
        }

        if (b is not double bv)
        {
            return a;
        }

        return Lerp(av, bv, t);
    }

    private static double LerpHeading(double a, double b, double t)
    {
        var delta = ((b - a + 540d) % 360d) - 180d;
        var value = (a + (delta * t)) % 360d;
        return value < 0d ? value + 360d : value;
    }
}
