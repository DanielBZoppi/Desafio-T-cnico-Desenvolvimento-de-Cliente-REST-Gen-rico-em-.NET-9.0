namespace GenericRestClient.RateLimiting;

public interface IRateLimiter
{

    Task WaitAsync(CancellationToken cancellationToken = default);
}
