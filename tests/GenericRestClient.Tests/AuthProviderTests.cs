using System.Text;
using System.Web;
using GenericRestClient.Auth;
using GenericRestClient.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GenericRestClient.Tests;

public class AuthProviderTests
{
    private static IOptions<ApiClientOptions> Opts(AuthenticationOptions auth) =>
        Options.Create(new ApiClientOptions { BaseUrl = "https://api.test/", Authentication = auth });

    [Fact]
    public async Task ApiKey_goes_to_header_when_header_name_is_set()
    {
        var provider = new ApiKeyAuthProvider(Opts(new AuthenticationOptions
        { Type = "ApiKey", ApiKey = "segredo", ApiKeyHeader = "x-api-key" }));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/items");

        await provider.AuthenticateAsync(request);
        await provider.AuthenticateAsync(request); 

        Assert.Equal("segredo", request.Headers.GetValues("x-api-key").Single());
    }

    [Fact]
    public async Task ApiKey_goes_to_query_string_when_no_header_name()
    {
        var provider = new ApiKeyAuthProvider(Opts(new AuthenticationOptions
        { Type = "ApiKey", ApiKey = "segredo", ApiKeyQueryParameter = "key" }));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/items?page=1");

        await provider.AuthenticateAsync(request);
        await provider.AuthenticateAsync(request); 

        var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
        Assert.Equal("segredo", query["key"]);
        Assert.Equal("1", query["page"]);
        Assert.Single(query.GetValues("key")!);
    }

    [Fact]
    public async Task Bearer_sets_authorization_header()
    {
        var provider = new BearerTokenProvider(Opts(new AuthenticationOptions { Type = "Bearer", BearerToken = "abc" }));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test/items");

        await provider.AuthenticateAsync(request);

        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("abc", request.Headers.Authorization.Parameter);
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static (OAuth2AuthProvider Provider, StubHandler Server, FakeTimeProvider Time) BuildOAuth()
    {
        var server = new StubHandler((_, n) => new HttpResponseMessage
        {
            Content = new StringContent($"{{\"access_token\":\"token-{n}\",\"expires_in\":3600}}", Encoding.UTF8, "application/json")
        });
        var time = new FakeTimeProvider();
        var options = Opts(new AuthenticationOptions
        {
            Type = "OAuth2", ClientId = "id", ClientSecret = "secret", TokenEndpoint = "https://auth.test/token"
        });
        var provider = new OAuth2AuthProvider(new StubFactory(server), options, time, NullLogger<OAuth2AuthProvider>.Instance);
        return (provider, server, time);
    }

    [Fact]
    public async Task OAuth2_caches_token_between_calls()
    {
        var (provider, server, _) = BuildOAuth();

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal("token-1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, server.CallCount);
    }

    [Fact]
    public async Task OAuth2_renews_token_before_it_expires()
    {
        var (provider, server, time) = BuildOAuth();
        await provider.GetAccessTokenAsync();

        time.Advance(TimeSpan.FromSeconds(3600 - 60)); 
        var renewed = await provider.GetAccessTokenAsync();

        Assert.Equal("token-2", renewed);
        Assert.Equal(2, server.CallCount);
    }

    [Fact]
    public async Task OAuth2_concurrent_calls_share_a_single_token_request()
    {
        var (provider, server, _) = BuildOAuth();

        var tokens = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => provider.GetAccessTokenAsync()));

        Assert.All(tokens, t => Assert.Equal("token-1", t));
        Assert.Equal(1, server.CallCount);
    }
}
