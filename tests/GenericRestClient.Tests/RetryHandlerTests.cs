using System.Net;
using GenericRestClient.Configuration;
using GenericRestClient.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GenericRestClient.Tests;

public class RetryHandlerTests
{
    private static RetryOptions FastRetry(int maxRetries = 3) => new()
    {
        Enabled = true,
        MaxRetries = maxRetries,
        BaseDelayMilliseconds = 1,
        UseExponentialBackoff = true,
        UseJitter = false
    };

    private static HttpMessageInvoker Build(RetryOptions retry, StubHandler inner)
    {
        var options = Options.Create(new ApiClientOptions { Retry = retry });
        var handler = new RetryHandler(options, NullLogger<RetryHandler>.Instance, TimeProvider.System)
        {
            InnerHandler = inner
        };
        return new HttpMessageInvoker(handler);
    }

    private static HttpRequestMessage NewRequest() => new(HttpMethod.Get, "http://localhost/x");

    [Fact]
    public async Task Retries_transient_status_until_success()
    {
        var inner = new StubHandler((_, n) =>
            new HttpResponseMessage(n < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));

        using var response = await Build(FastRetry(), inner).SendAsync(NewRequest(), default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, inner.CallCount);
    }

    [Fact]
    public async Task Retries_429_too_many_requests()
    {
        var inner = new StubHandler((_, n) =>
            new HttpResponseMessage(n == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK));

        using var response = await Build(FastRetry(), inner).SendAsync(NewRequest(), default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task Stops_after_max_retries_and_returns_last_response()
    {
        var inner = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        using var response = await Build(FastRetry(maxRetries: 2), inner).SendAsync(NewRequest(), default);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(3, inner.CallCount);
    }

    [Fact]
    public async Task Does_not_retry_permanent_errors()
    {
        var inner = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));

        using var response = await Build(FastRetry(), inner).SendAsync(NewRequest(), default);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task Does_nothing_when_disabled()
    {
        var inner = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var retry = FastRetry();
        retry.Enabled = false;

        using var response = await Build(retry, inner).SendAsync(NewRequest(), default);

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task Retries_network_exceptions()
    {
        var inner = new StubHandler((_, n) =>
            n < 2 ? throw new HttpRequestException("falha de rede") : new HttpResponseMessage(HttpStatusCode.OK));

        using var response = await Build(FastRetry(), inner).SendAsync(NewRequest(), default);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public void Exponential_backoff_doubles_each_attempt_without_jitter()
    {
        var retry = new RetryOptions
        {
            Enabled = true, MaxRetries = 5, BaseDelayMilliseconds = 500,
            UseExponentialBackoff = true, UseJitter = false, MaxDelayMilliseconds = 30_000
        };

        Assert.Equal(TimeSpan.FromMilliseconds(500), RetryHandler.CalculateDelay(retry, 1, null, DateTimeOffset.UtcNow));
        Assert.Equal(TimeSpan.FromMilliseconds(1000), RetryHandler.CalculateDelay(retry, 2, null, DateTimeOffset.UtcNow));
        Assert.Equal(TimeSpan.FromMilliseconds(2000), RetryHandler.CalculateDelay(retry, 3, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Retry_After_header_wins_when_longer()
    {
        var retry = new RetryOptions
        {
            Enabled = true, MaxRetries = 3, BaseDelayMilliseconds = 100,
            UseExponentialBackoff = false, UseJitter = false
        };
        var retryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(5));

        var delay = RetryHandler.CalculateDelay(retry, 1, retryAfter, DateTimeOffset.UtcNow);

        Assert.Equal(TimeSpan.FromSeconds(5), delay);
    }
}
