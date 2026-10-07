namespace NavBR.OpenOmsiPlugin;

internal sealed record OpenOmsiNavigationRuntimeState(
    double SuggestedMapRadiusMeters,
    string CongestionLevel,
    double? AverageNearbyTrafficSpeedKph,
    int NearbyMovingAiCount,
    int NearbySlowAiCount,
    int NearbyStoppedAiCount);

internal static class OpenOmsiNavigationRuntime
{
    private const double CongestionSampleRadiusMeters = 350d;

    public static OpenOmsiNavigationRuntimeState Build(OpenOmsiLuaSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new(
                SuggestedMapRadiusMeters: 900d,
                CongestionLevel: "unknown",
                AverageNearbyTrafficSpeedKph: null,
                NearbyMovingAiCount: 0,
                NearbySlowAiCount: 0,
                NearbyStoppedAiCount: 0);
        }

        var suggestedRadius = SuggestedRadius(
            snapshot.ReportedSpeedKph,
            snapshot.OnFoot);

        if (!snapshot.HasPosition ||
            snapshot.X is null ||
            snapshot.Y is null)
        {
            return new(
                SuggestedMapRadiusMeters: suggestedRadius,
                CongestionLevel: "unknown",
                AverageNearbyTrafficSpeedKph: null,
                NearbyMovingAiCount: 0,
                NearbySlowAiCount: 0,
                NearbyStoppedAiCount: 0);
        }

        var nearbyAi = snapshot.NearbyVehicles
            .Where(vehicle =>
                string.Equals(vehicle.Kind, "ai", StringComparison.Ordinal) &&
                Distance2D(snapshot.X.Value, snapshot.Y.Value, vehicle.X, vehicle.Y) <=
                    CongestionSampleRadiusMeters)
            .ToArray();

        var speeds = nearbyAi
            .Where(vehicle => vehicle.SpeedKph is >= 0d and <= 220d)
            .Select(vehicle => vehicle.SpeedKph!.Value)
            .ToArray();

        if (speeds.Length == 0)
        {
            return new(
                SuggestedMapRadiusMeters: suggestedRadius,
                CongestionLevel: nearbyAi.Length == 0 ? "clear" : "unknown",
                AverageNearbyTrafficSpeedKph: null,
                NearbyMovingAiCount: 0,
                NearbySlowAiCount: 0,
                NearbyStoppedAiCount: 0);
        }

        var stopped = speeds.Count(speed => speed < 3d);
        var slow = speeds.Count(speed => speed is >= 3d and < 15d);
        var moving = speeds.Count(speed => speed >= 15d);
        var average = speeds.Average();

        var constrainedRatio = (stopped + slow) / (double)speeds.Length;
        var congestion = (average, constrainedRatio, speeds.Length) switch
        {
            (_, _, < 3) => "light",
            (< 8d, >= 0.70d, _) => "heavy",
            (< 15d, >= 0.50d, _) => "moderate",
            (< 25d, >= 0.35d, _) => "light",
            _ => "clear"
        };

        return new(
            SuggestedMapRadiusMeters: suggestedRadius,
            CongestionLevel: congestion,
            AverageNearbyTrafficSpeedKph: Math.Round(average, 1),
            NearbyMovingAiCount: moving,
            NearbySlowAiCount: slow,
            NearbyStoppedAiCount: stopped);
    }

    internal static double SuggestedRadius(double? speedKph, bool onFoot)
    {
        if (onFoot)
        {
            return 250d;
        }

        var speed = Math.Abs(speedKph ?? 0d);
        return speed switch
        {
            < 10d => 450d,
            < 25d => 650d,
            < 45d => 900d,
            < 65d => 1250d,
            < 90d => 1750d,
            _ => 2400d
        };
    }

    private static double Distance2D(
        double x1,
        double y1,
        double x2,
        double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
