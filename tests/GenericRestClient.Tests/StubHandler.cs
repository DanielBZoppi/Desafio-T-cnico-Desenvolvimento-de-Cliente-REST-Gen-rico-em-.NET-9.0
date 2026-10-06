namespace GenericRestClient.Tests;

internal sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int CallCount;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var n = Interlocked.Increment(ref CallCount);
        return Task.FromResult(respond(request, n));
    }
}
