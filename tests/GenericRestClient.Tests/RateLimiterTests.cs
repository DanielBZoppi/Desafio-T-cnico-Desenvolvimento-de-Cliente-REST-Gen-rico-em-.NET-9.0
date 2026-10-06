using GenericRestClient.RateLimiting;
using Microsoft.Extensions.Time.Testing;

namespace GenericRestClient.Tests;

public class RateLimiterTests
{
    [Fact]
    public async Task Blocks_when_limit_is_reached_and_releases_after_window()
    {
        var time = new FakeTimeProvider();
        var limiter = new SlidingWindowRateLimiter(2, TimeSpan.FromMinutes(1), time);

        await limiter.WaitAsync();
        await limiter.WaitAsync();

        var third = limiter.WaitAsync();
        Assert.False(third.IsCompleted); 

        time.Advance(TimeSpan.FromMinutes(1));
        await third.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Concurrent_calls_never_exceed_the_limit()
    {
        var time = new FakeTimeProvider();
        var limiter = new SlidingWindowRateLimiter(3, TimeSpan.FromMinutes(1), time);

        var tasks = Enumerable.Range(0, 5).Select(_ => limiter.WaitAsync()).ToList();

        Assert.Equal(3, tasks.Count(t => t.IsCompleted));

        time.Advance(TimeSpan.FromMinutes(1));
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Cancellation_is_respected_while_waiting()
    {
        var time = new FakeTimeProvider();
        var limiter = new SlidingWindowRateLimiter(1, TimeSpan.FromMinutes(1), time);
        await limiter.WaitAsync();

        using var cts = new CancellationTokenSource();
        var waiting = limiter.WaitAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }
}
