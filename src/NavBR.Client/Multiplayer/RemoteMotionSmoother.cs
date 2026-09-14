namespace NavBR.Client.Multiplayer;

public sealed class RemoteMotionSmoother
{
    private const double PositionSnapDistance = 600d;

    private bool _initialized;
    private double _x;
    private double _y;
    private double _heading;
    private double _targetX;
    private double _targetY;
    private double _targetHeading;

    public DateTimeOffset LastTargetUtc { get; private set; } = DateTimeOffset.MinValue;

    public void SetTarget(double x, double y, double headingDegrees, DateTimeOffset? timestampUtc = null)
    {
        var normalizedHeading = NormalizeHeading(headingDegrees);
        LastTargetUtc = timestampUtc ?? DateTimeOffset.UtcNow;

        if (!_initialized)
        {
            Snap(x, y, normalizedHeading);
            return;
        }

        var dx = x - _x;
        var dy = y - _y;
        if (Math.Sqrt(dx * dx + dy * dy) > PositionSnapDistance)
        {
            Snap(x, y, normalizedHeading);
            return;
        }

        _targetX = x;
        _targetY = y;
        _targetHeading = normalizedHeading;
    }

    public SmoothedRemotePose Step(double positionAlpha = 0.30d, double headingAlpha = 0.25d)
    {
        if (!_initialized)
        {
            return new SmoothedRemotePose(0d, 0d, 0d, false);
        }

        positionAlpha = Math.Clamp(positionAlpha, 0d, 1d);
        headingAlpha = Math.Clamp(headingAlpha, 0d, 1d);

        _x += (_targetX - _x) * positionAlpha;
        _y += (_targetY - _y) * positionAlpha;
        _heading = NormalizeHeading(_heading + ShortestAngleDelta(_heading, _targetHeading) * headingAlpha);

        if (Math.Abs(_targetX - _x) < 0.05d)
        {
            _x = _targetX;
        }

        if (Math.Abs(_targetY - _y) < 0.05d)
        {
            _y = _targetY;
        }

        return new SmoothedRemotePose(_x, _y, _heading, true);
    }

    public void Reset()
    {
        _initialized = false;
        LastTargetUtc = DateTimeOffset.MinValue;
    }

    private void Snap(double x, double y, double heading)
    {
        _initialized = true;
        _x = _targetX = x;
        _y = _targetY = y;
        _heading = _targetHeading = heading;
    }

    private static double ShortestAngleDelta(double from, double to)
    {
        var delta = NormalizeHeading(to) - NormalizeHeading(from);
        if (delta > 180d)
        {
            delta -= 360d;
        }
        else if (delta < -180d)
        {
            delta += 360d;
        }

        return delta;
    }

    private static double NormalizeHeading(double value)
    {
        value %= 360d;
        return value < 0d ? value + 360d : value;
    }
}

public readonly record struct SmoothedRemotePose(
    double X,
    double Y,
    double HeadingDegrees,
    bool IsValid);
