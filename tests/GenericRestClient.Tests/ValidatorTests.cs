using GenericRestClient.Configuration;

namespace GenericRestClient.Tests;

public class ValidatorTests
{
    private static ApiClientOptions Valid() => new()
    {
        BaseUrl = "https://api.test/",
        Authentication = new AuthenticationOptions { Type = "ApiKey", ApiKey = "k", ApiKeyHeader = "x-api-key" },
        RateLimit = new RateLimitOptions { Enabled = true, RequestsPerMinute = 60 },
        Retry = new RetryOptions { Enabled = true, MaxRetries = 3, BaseDelayMilliseconds = 500 }
    };

    private static Microsoft.Extensions.Options.ValidateOptionsResult Run(ApiClientOptions o) =>
        new ApiClientOptionsValidator().Validate(null, o);

    [Fact]
    public void Valid_configuration_passes() => Assert.True(Run(Valid()).Succeeded);

    [Fact]
    public void Unknown_auth_type_fails_with_clear_message()
    {
        var o = Valid();
        o.Authentication.Type = "Basic";
        var result = Run(o);
        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Type") && f.Contains("Basic"));
    }

    [Fact]
    public void OAuth2_requires_client_id_secret_and_endpoint()
    {
        var o = Valid();
        o.Authentication = new AuthenticationOptions { Type = "OAuth2" };
        Assert.Equal(3, Run(o).Failures!.Count());
    }

    [Fact]
    public void Bearer_requires_token()
    {
        var o = Valid();
        o.Authentication = new AuthenticationOptions { Type = "Bearer" };
        Assert.True(Run(o).Failed);
    }

    [Fact]
    public void Enabled_rate_limit_requires_positive_value()
    {
        var o = Valid();
        o.RateLimit.RequestsPerMinute = 0;
        Assert.True(Run(o).Failed);
    }

    [Fact]
    public void Disabled_rate_limit_and_retry_skip_their_validation()
    {
        var o = Valid();
        o.RateLimit = new RateLimitOptions { Enabled = false };
        o.Retry = new RetryOptions { Enabled = false };
        Assert.True(Run(o).Succeeded);
    }

    [Fact]
    public void Invalid_base_url_fails()
    {
        var o = Valid();
        o.BaseUrl = "nao-e-uma-url";
        Assert.True(Run(o).Failed);
    }
}
