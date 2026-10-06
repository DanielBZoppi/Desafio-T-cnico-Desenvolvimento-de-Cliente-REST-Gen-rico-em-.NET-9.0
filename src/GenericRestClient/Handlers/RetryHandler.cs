using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using GenericRestClient.Configuration;
using GenericRestClient.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GenericRestClient.Handlers;


internal sealed class RetryHandler(
    IOptions<ApiClientOptions> options,
    ILogger<RetryHandler> logger,
    TimeProvider timeProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var retry = options.Value.Retry;
        if (!retry.Enabled)
            return await base.SendAsync(request, cancellationToken);

        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync(cancellationToken);

        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage? response = null;
            Exception? failure = null;

            try
            {
                response = await base.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (IsTransientException(ex, cancellationToken))
            {
                failure = ex;
            }

            var isLastAttempt = attempt >= retry.MaxRetries;

            if (response is not null && (!response.StatusCode.IsTransient() || isLastAttempt))
                return response; 

            if (failure is not null && isLastAttempt)
                ExceptionDispatchInfo.Capture(failure).Throw(); 

            var delay = CalculateDelay(retry, attempt + 1, response?.Headers.RetryAfter, timeProvider.GetUtcNow());
            var reason = failure?.GetType().Name ?? $"HTTP {(int)response!.StatusCode}";

            logger.LogWarning(
                "Tentativa {Attempt}/{Total} falhou ({Reason}). Nova tentativa em {DelayMs} ms.",
                attempt + 1, retry.MaxRetries + 1, reason, (int)delay.TotalMilliseconds);

            response?.Dispose();
            await Task.Delay(delay, timeProvider, cancellationToken);
        }
    }

    private static bool IsTransientException(Exception ex, CancellationToken cancellationToken) => ex switch
    {
        HttpRequestException => true,                                      
        TimeoutException => true,
        TaskCanceledException when !cancellationToken.IsCancellationRequested => true, // timeout (não foi o usuário que cancelou)
        _ => false
    };

    internal static TimeSpan CalculateDelay(
        RetryOptions retry, int retryNumber, RetryConditionHeaderValue? retryAfter, DateTimeOffset now)
    {
        var ms = retry.UseExponentialBackoff
            ? retry.BaseDelayMilliseconds * Math.Pow(2, retryNumber - 1)
            : retry.BaseDelayMilliseconds;

        ms = Math.Min(ms, retry.MaxDelayMilliseconds);

        if (retry.UseJitter)
            ms = ms / 2 + Random.Shared.NextDouble() * ms / 2;

        var delay = TimeSpan.FromMilliseconds(ms);

        var serverDelay = retryAfter?.Delta ?? (retryAfter?.Date - now);
        if (serverDelay is { } requested && requested > delay)
            delay = requested;

        return delay;
    }
}
