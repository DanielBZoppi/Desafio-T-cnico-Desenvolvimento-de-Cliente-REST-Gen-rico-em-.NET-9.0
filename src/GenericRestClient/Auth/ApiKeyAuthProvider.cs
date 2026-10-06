using System.Web;
using GenericRestClient.Configuration;
using Microsoft.Extensions.Options;

namespace GenericRestClient.Auth;

internal sealed class ApiKeyAuthProvider(IOptions<ApiClientOptions> options) : IAuthProvider
{
    public Task AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var auth = options.Value.Authentication;
        var key = auth.ApiKey ?? throw new InvalidOperationException("ApiKey não configurada.");

        if (!string.IsNullOrWhiteSpace(auth.ApiKeyHeader))
        {
            
            request.Headers.Remove(auth.ApiKeyHeader);
            request.Headers.TryAddWithoutValidation(auth.ApiKeyHeader, key);
        }
        else
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("Requisição sem URI.");
            if (!uri.IsAbsoluteUri)
                throw new InvalidOperationException("A URI da requisição deve ser absoluta neste ponto do pipeline.");

            var query = HttpUtility.ParseQueryString(uri.Query);
            query[auth.ApiKeyQueryParameter] = key; 
            request.RequestUri = new UriBuilder(uri) { Query = query.ToString() }.Uri;
        }

        return Task.CompletedTask;
    }
}
