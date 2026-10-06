namespace GenericRestClient.RateLimiting;

internal sealed class NoRateLimiter : IRateLimiter
{
    public static readonly NoRateLimiter Instance = new();

    public Task WaitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
