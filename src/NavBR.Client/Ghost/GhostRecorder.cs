using System.Text.Json;
using NavBR.Shared.Telemetry;

namespace NavBR.Client.Ghost;

internal sealed class GhostRecorder
{
    private readonly object _sync = new();
    private readonly List<GhostReplayFrame> _frames = [];
    private DateTimeOffset? _startedAtUtc;
    private string? _name;

    public bool IsRecording
    {
        get
        {
            lock (_sync)
            {
                return _startedAtUtc is not null;
            }
        }
    }

    public int FrameCount
    {
        get
        {
            lock (_sync)
            {
                return _frames.Count;
            }
        }
    }

    public void Start(string? name = null)
    {
        lock (_sync)
        {
            _frames.Clear();
            _startedAtUtc = DateTimeOffset.UtcNow;
            _name = string.IsNullOrWhiteSpace(name)
                ? $"Ghost {DateTime.Now:yyyy-MM-dd HH-mm-ss}"
                : name.Trim();
        }
    }

    public void Record(VehicleTelemetry? telemetry)
    {
        if (telemetry is null || !telemetry.IsInGame)
        {
            return;
        }

        lock (_sync)
        {
            if (_startedAtUtc is not DateTimeOffset started)
            {
                return;
            }

            var offset = Math.Max(0L, (long)(telemetry.Timestamp - started).TotalMilliseconds);
            if (_frames.Count > 0 && offset <= _frames[^1].OffsetMilliseconds)
            {
                offset = _frames[^1].OffsetMilliseconds + 1;
            }

            _frames.Add(new GhostReplayFrame(offset, telemetry));
        }
    }

    public async Task<string> StopAndSaveAsync(
        string? outputPath = null,
        CancellationToken cancellationToken = default)
    {
        GhostReplayFrame[] frames;
        DateTimeOffset started;
        string name;

        lock (_sync)
        {
            if (_startedAtUtc is not DateTimeOffset currentStart)
            {
                throw new InvalidOperationException("Ghost recording is not active.");
            }

            if (_frames.Count < 2)
            {
                throw new InvalidOperationException("Ghost recording needs at least two valid telemetry frames.");
            }

            started = currentStart;
            name = _name ?? "Ghost";
            frames = _frames.ToArray();
            _frames.Clear();
            _startedAtUtc = null;
            _name = null;
        }

        var first = frames[0].Telemetry;
        var duration = frames[^1].OffsetMilliseconds / 1000d;
        var metadata = new GhostReplayMetadata(
            GhostReplayFormat.Version,
            name,
            started,
            first.MapName,
            first.MapCompatibilityId,
            first.VehicleName,
            first.VehiclePath,
            first.VehicleCompatibilityId,
            first.HofName,
            first.HofCompatibilityId,
            duration,
            frames.Length);

        var document = new GhostReplayDocument(metadata, frames);
        var path = string.IsNullOrWhiteSpace(outputPath)
            ? CreateDefaultPath(name)
            : Path.GetFullPath(outputPath);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            document,
            new JsonSerializerOptions
            {
                WriteIndented = false
            },
            cancellationToken);

        return path;
    }

    public void Cancel()
    {
        lock (_sync)
        {
            _frames.Clear();
            _startedAtUtc = null;
            _name = null;
        }
    }

    public static string GetGhostDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OMSI NavBR Multiplayer",
            "Ghosts");
    }

    private static string CreateDefaultPath(string name)
    {
        var safe = string.Concat(name.Select(ch =>
            Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "Ghost";
        }

        return Path.Combine(
            GetGhostDirectory(),
            $"{DateTime.Now:yyyyMMdd-HHmmss}-{safe}{GhostReplayFormat.Extension}");
    }
}
