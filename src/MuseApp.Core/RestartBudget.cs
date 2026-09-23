namespace MuseApp.Core;

/// <summary>
/// Sliding-window retry limiter for automatic WebView2 recovery. A crash loop (e.g. a
/// broken runtime) must end in an error panel rather than an endless reload cycle.
/// </summary>
public sealed class RestartBudget(int maxAttempts, TimeSpan window, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Queue<DateTimeOffset> _attempts = new();

    /// <summary>Records an attempt and returns true if it is within budget.</summary>
    public bool TryConsume()
    {
        var now = _time.GetUtcNow();
        while (_attempts.Count > 0 && now - _attempts.Peek() >= window)
            _attempts.Dequeue();

        if (_attempts.Count >= maxAttempts)
            return false;

        _attempts.Enqueue(now);
        return true;
    }

    /// <summary>Forgets past attempts (after the user manually retries).</summary>
    public void Reset() => _attempts.Clear();
}
