namespace GenericRestClient.RateLimiting;


internal sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly Queue<long> _timestamps = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SlidingWindowRateLimiter(int limit, TimeSpan window, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limit, 0);
        _limit = limit;
        _window = window;
        _timeProvider = timeProvider;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                var now = _timeProvider.GetTimestamp();

                while (_timestamps.Count > 0 &&
                       _timeProvider.GetElapsedTime(_timestamps.Peek(), now) >= _window)
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count < _limit)
                {
                    _timestamps.Enqueue(now);
                    return;
                }
                var wait = _window - _timeProvider.GetElapsedTime(_timestamps.Peek(), now);
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, _timeProvider, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
