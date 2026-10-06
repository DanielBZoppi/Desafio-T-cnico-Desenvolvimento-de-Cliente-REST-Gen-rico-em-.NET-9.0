using System.Net.Http.Headers;

namespace GenericRestClient.Auth;

internal abstract class TokenAuthProvider : IAuthProvider
{
    public abstract Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    public async Task AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}
