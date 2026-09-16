using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

internal static class VoiceChannelSession
{
    private static readonly object Sync = new();
    private static string _channel = "general";
    private static double _proximityMeters = 120d;
    private static Func<VoiceFrame, bool>? _proximityFilter;

    public static string CurrentChannel
    {
        get
        {
            lock (Sync)
            {
                return _channel;
            }
        }
    }

    public static double ProximityMeters
    {
        get
        {
            lock (Sync)
            {
                return _proximityMeters;
            }
        }
    }

    public static void Configure(
        string? channel,
        double proximityMeters,
        Func<VoiceFrame, bool>? proximityFilter = null)
    {
        lock (Sync)
        {
            _channel = NormalizeChannel(channel);
            _proximityMeters = Math.Clamp(
                double.IsFinite(proximityMeters) ? proximityMeters : 120d,
                20d,
                1000d);
            _proximityFilter = proximityFilter;
        }
    }

    public static void SetProximityFilter(Func<VoiceFrame, bool>? proximityFilter)
    {
        lock (Sync)
        {
            _proximityFilter = proximityFilter;
        }
    }

    public static bool ShouldReceive(VoiceFrame frame)
    {
        string channel;
        Func<VoiceFrame, bool>? proximityFilter;
        lock (Sync)
        {
            channel = _channel;
            proximityFilter = _proximityFilter;
        }

        var incoming = NormalizeChannel(frame.Channel);
        if (!string.Equals(channel, incoming, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.Equals(channel, "proximity", StringComparison.OrdinalIgnoreCase) ||
               (proximityFilter?.Invoke(frame) ?? false);
    }

    public static string NormalizeChannel(string? channel) =>
        (channel ?? "general").Trim().ToLowerInvariant() switch
        {
            "company" or "team" => "company",
            "dispatch" or "cco" => "dispatch",
            "proximity" => "proximity",
            _ => "general"
        };
}
