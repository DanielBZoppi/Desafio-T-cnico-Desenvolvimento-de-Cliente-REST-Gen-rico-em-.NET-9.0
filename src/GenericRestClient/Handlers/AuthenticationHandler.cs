using GenericRestClient.Auth;

namespace GenericRestClient.Handlers;


internal sealed class AuthenticationHandler(IAuthProvider authProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await authProvider.AuthenticateAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
