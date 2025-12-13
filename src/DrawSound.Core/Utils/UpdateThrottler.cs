namespace DrawSound.Core.Utils;

/// <summary>
/// Throttles update calls to prevent excessive processing during rapid input
/// </summary>
public class UpdateThrottler
{
    private readonly int _throttleIntervalMs;
    private DateTime _lastUpdateTime = DateTime.MinValue;

    public int ThrottleIntervalMs => _throttleIntervalMs;
    public bool NeedsDeferredUpdate { get; private set; }

    public UpdateThrottler(int throttleIntervalMs)
    {
        _throttleIntervalMs = throttleIntervalMs;
    }

    /// <summary>
    /// Check if enough time has passed to allow an update
    /// </summary>
    /// <returns>True if update should proceed, false if throttled</returns>
    public bool ShouldUpdate()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastUpdateTime).TotalMilliseconds;

        if (elapsed >= _throttleIntervalMs)
        {
            _lastUpdateTime = now;
            NeedsDeferredUpdate = false;
            return true;
        }

        NeedsDeferredUpdate = true;
        return false;
    }

    /// <summary>
    /// Reset the throttler to allow immediate update
    /// </summary>
    public void Reset()
    {
        _lastUpdateTime = DateTime.MinValue;
        NeedsDeferredUpdate = false;
    }

    /// <summary>
    /// Get the remaining time until next allowed update
    /// </summary>
    public int GetDeferredDelayMs()
    {
        var elapsed = (DateTime.UtcNow - _lastUpdateTime).TotalMilliseconds;
        return Math.Max(1, _throttleIntervalMs - (int)elapsed);
    }
}

