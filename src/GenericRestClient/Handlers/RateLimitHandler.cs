using GenericRestClient.RateLimiting;

namespace GenericRestClient.Handlers;

internal sealed class RateLimitHandler(IRateLimiter rateLimiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await rateLimiter.WaitAsync(cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
