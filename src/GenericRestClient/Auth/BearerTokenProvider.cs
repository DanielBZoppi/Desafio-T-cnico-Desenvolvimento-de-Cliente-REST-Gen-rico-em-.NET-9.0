using GenericRestClient.Configuration;
using Microsoft.Extensions.Options;

namespace GenericRestClient.Auth;


internal sealed class BearerTokenProvider(IOptions<ApiClientOptions> options) : TokenAuthProvider
{
    public override Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(options.Value.Authentication.BearerToken);
}
