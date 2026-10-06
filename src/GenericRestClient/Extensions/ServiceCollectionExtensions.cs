using GenericRestClient.Auth;
using GenericRestClient.Configuration;
using GenericRestClient.Core;
using GenericRestClient.Handlers;
using GenericRestClient.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GenericRestClient.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRestClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = ApiClientOptions.SectionName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<ApiClientOptions>().Bind(configuration.GetSection(sectionName));
        return services.AddRestClientCore();
    }

    public static IServiceCollection AddRestClient(
        this IServiceCollection services,
        Action<ApiClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<ApiClientOptions>().Configure(configure);
        return services.AddRestClientCore();
    }

    private static IServiceCollection AddRestClientCore(this IServiceCollection services)
    {
        services.AddOptions<ApiClientOptions>().ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ApiClientOptions>, ApiClientOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAuthProvider>(CreateAuthProvider);
        services.TryAddSingleton<IRateLimiter>(CreateRateLimiter);
        services.AddHttpClient(OAuth2AuthProvider.HttpClientName);
        services.AddTransient<AuthenticationHandler>();
        services.AddTransient<RateLimitHandler>();
        services.AddTransient<RetryHandler>();
        services.AddHttpClient<IRestClient, RestClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<ApiClientOptions>>().Value;
                client.BaseAddress = new Uri(EnsureTrailingSlash(options.BaseUrl));
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddHttpMessageHandler<RetryHandler>()
            .AddHttpMessageHandler<RateLimitHandler>()
            .AddHttpMessageHandler<AuthenticationHandler>();

        return services;
    }

    private static IAuthProvider CreateAuthProvider(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<ApiClientOptions>>();

        if (!options.Value.Authentication.TryGetAuthenticationType(out var type))
        {
            throw new InvalidOperationException(
                $"Authentication:Type '{options.Value.Authentication.Type}' inválido. Use ApiKey, Bearer ou OAuth2.");
        }

        return type switch
        {
            AuthenticationType.ApiKey => new ApiKeyAuthProvider(options),
            AuthenticationType.Bearer => new BearerTokenProvider(options),
            AuthenticationType.OAuth2 => new OAuth2AuthProvider(
                sp.GetRequiredService<IHttpClientFactory>(),
                options,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<OAuth2AuthProvider>>()),
            _ => throw new InvalidOperationException($"Tipo de autenticação não suportado: {type}.")
        };
    }

    private static IRateLimiter CreateRateLimiter(IServiceProvider sp)
    {
        var rateLimit = sp.GetRequiredService<IOptions<ApiClientOptions>>().Value.RateLimit;

        return rateLimit.Enabled
            ? new SlidingWindowRateLimiter(
                rateLimit.RequestsPerMinute, TimeSpan.FromMinutes(1), sp.GetRequiredService<TimeProvider>())
            : NoRateLimiter.Instance;
    }
    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";
}
