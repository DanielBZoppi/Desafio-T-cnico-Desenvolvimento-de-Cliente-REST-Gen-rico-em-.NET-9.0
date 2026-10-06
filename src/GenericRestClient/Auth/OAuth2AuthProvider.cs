using System.Text.Json;
using GenericRestClient.Configuration;
using GenericRestClient.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GenericRestClient.Auth;


internal sealed class OAuth2AuthProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ApiClientOptions> options,
    TimeProvider timeProvider,
    ILogger<OAuth2AuthProvider> logger) : TokenAuthProvider
{
    public const string HttpClientName = "GenericRestClient.OAuth2";

    private sealed record TokenState(string AccessToken, DateTimeOffset RenewAt);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private volatile TokenState? _state;

    public override async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (TryGetValidToken(out var token))
            return token;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetValidToken(out token)) 
                return token;

            var state = await RequestTokenAsync(cancellationToken);
            _state = state;
            return state.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool TryGetValidToken(out string? token)
    {
        var state = _state;
        if (state is not null && timeProvider.GetUtcNow() < state.RenewAt)
        {
            token = state.AccessToken;
            return true;
        }

        token = null;
        return false;
    }

    private async Task<TokenState> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var auth = options.Value.Authentication;
        var endpoint = new Uri(auth.TokenEndpoint!);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = auth.ClientId!,
            ["client_secret"] = auth.ClientSecret!
        };
        if (!string.IsNullOrWhiteSpace(auth.Scope))
            form["scope"] = auth.Scope;

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new RestClientException(
                $"Falha ao obter token OAuth2 em {endpoint}: HTTP {(int)response.StatusCode}.",
                request.Method, endpoint, response.StatusCode, json);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var tokenElement) ||
                tokenElement.GetString() is not { Length: > 0 } accessToken)
            {
                throw new RestClientException("A resposta do servidor OAuth2 não contém 'access_token'.",
                    request.Method, endpoint, response.StatusCode, json);
            }

            var expiresInSeconds = 3600; // padrão caso o servidor não informe
            if (root.TryGetProperty("expires_in", out var expiresElement))
            {
                if (expiresElement.ValueKind == JsonValueKind.Number && expiresElement.TryGetInt32(out var n))
                    expiresInSeconds = n;
                else if (expiresElement.ValueKind == JsonValueKind.String &&
                         int.TryParse(expiresElement.GetString(), out var s))
                    expiresInSeconds = s;
            }

            var lifetime = TimeSpan.FromSeconds(Math.Max(expiresInSeconds - auth.TokenExpirationSkewSeconds, 0));
            var renewAt = timeProvider.GetUtcNow().Add(lifetime);

            logger.LogInformation("Novo token OAuth2 obtido (expira em {ExpiresIn}s).", expiresInSeconds);
            return new TokenState(accessToken, renewAt);
        }
        catch (JsonException ex)
        {
            throw new RestClientException("Resposta inválida do servidor OAuth2 (JSON malformado).",
                request.Method, endpoint, response.StatusCode, json, ex);
        }
    }
}
