namespace GenericRestClient.Configuration;


public sealed class ApiClientOptions
{
    public const string SectionName = "ApiClient";

    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 100;

    public AuthenticationOptions Authentication { get; set; } = new();
    public RateLimitOptions RateLimit { get; set; } = new();
    public RetryOptions Retry { get; set; } = new();
}

public enum AuthenticationType
{
    ApiKey,
    Bearer,
    OAuth2
}

public sealed class AuthenticationOptions
{
 
    public string Type { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string? ApiKeyHeader { get; set; }
    public string ApiKeyQueryParameter { get; set; } = "api_key";
    public string? BearerToken { get; set; }

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? Scope { get; set; }

    public int TokenExpirationSkewSeconds { get; set; } = 60;

    public bool TryGetAuthenticationType(out AuthenticationType type) =>
        Enum.TryParse(Type, ignoreCase: true, out type) && Enum.IsDefined(type);
}

public sealed class RateLimitOptions
{
    public bool Enabled { get; set; }
    public int RequestsPerMinute { get; set; }
}

public sealed class RetryOptions
{
    public bool Enabled { get; set; }


    public int MaxRetries { get; set; }

    public int BaseDelayMilliseconds { get; set; }
    public bool UseExponentialBackoff { get; set; }

    public bool UseJitter { get; set; } = true;

    public int MaxDelayMilliseconds { get; set; } = 30_000;
}
