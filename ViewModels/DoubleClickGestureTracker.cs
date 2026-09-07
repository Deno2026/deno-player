namespace DenoVideoPlayer.ViewModels;

/// <summary>
/// Pairs actual pointer presses and deduplicates gestures reported by multiple input paths.
/// Timestamps use the Win32 32-bit millisecond clock; compared events must be less than
/// half its range apart. Pointer coordinates and distance limits use screen pixels.
/// </summary>
public sealed class DoubleClickGestureTracker
{
    private bool _hasLastPress;
    private uint _lastPressTimestamp;
    private bool _hasPendingPress;
    private double _pendingX;
    private double _pendingY;
    private bool _hasAcceptedGesture;
    private uint _lastAcceptedTimestamp;

    public bool RecordPress(
        uint timestamp,
        double x,
        double y,
        uint maxIntervalMs,
        double maxDistanceX,
        double maxDistanceY)
    {
        if (_hasLastPress && !IsAfter(timestamp, _lastPressTimestamp))
            return false;

        var matchesPendingPress = _hasPendingPress
            && unchecked(timestamp - _lastPressTimestamp) <= maxIntervalMs
            && Math.Abs(x - _pendingX) <= maxDistanceX
            && Math.Abs(y - _pendingY) <= maxDistanceY;

        _hasLastPress = true;
        _lastPressTimestamp = timestamp;
        if (matchesPendingPress)
        {
            _hasPendingPress = false;
            return true;
        }

        _hasPendingPress = true;
        _pendingX = x;
        _pendingY = y;
        return false;
    }

    /// <summary>Ends an incomplete pair without accepting repeated or late input again.</summary>
    public void ResetPress() => _hasPendingPress = false;

    public bool TryAcceptGesture(uint timestamp)
    {
        if (_hasAcceptedGesture && !IsAfter(timestamp, _lastAcceptedTimestamp))
            return false;

        _hasAcceptedGesture = true;
        _lastAcceptedTimestamp = timestamp;
        return true;
    }

    private static bool IsAfter(uint timestamp, uint previousTimestamp) =>
        unchecked((int)(timestamp - previousTimestamp)) > 0;
}
