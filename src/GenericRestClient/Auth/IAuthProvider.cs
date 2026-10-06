namespace GenericRestClient.Auth;

public interface IAuthProvider
{
    Task AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
