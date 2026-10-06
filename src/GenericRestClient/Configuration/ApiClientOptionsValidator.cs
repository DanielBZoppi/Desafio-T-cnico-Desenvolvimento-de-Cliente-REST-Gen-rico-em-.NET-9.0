using Microsoft.Extensions.Options;

namespace GenericRestClient.Configuration;


internal sealed class ApiClientOptionsValidator : IValidateOptions<ApiClientOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiClientOptions options)
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("ApiClient:BaseUrl é obrigatório e deve ser uma URL http(s) absoluta.");
        }

        if (options.TimeoutSeconds <= 0)
            errors.Add("ApiClient:TimeoutSeconds deve ser maior que zero.");

        ValidateAuthentication(options.Authentication, errors);

        if (options.RateLimit.Enabled && options.RateLimit.RequestsPerMinute <= 0)
            errors.Add("ApiClient:RateLimit:RequestsPerMinute deve ser maior que zero quando RateLimit.Enabled = true.");

        if (options.Retry.Enabled)
        {
            if (options.Retry.MaxRetries <= 0)
                errors.Add("ApiClient:Retry:MaxRetries deve ser maior que zero quando Retry.Enabled = true.");
            if (options.Retry.BaseDelayMilliseconds <= 0)
                errors.Add("ApiClient:Retry:BaseDelayMilliseconds deve ser maior que zero quando Retry.Enabled = true.");
            if (options.Retry.MaxDelayMilliseconds < options.Retry.BaseDelayMilliseconds)
                errors.Add("ApiClient:Retry:MaxDelayMilliseconds não pode ser menor que BaseDelayMilliseconds.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateAuthentication(AuthenticationOptions auth, List<string> errors)
    {
        if (!auth.TryGetAuthenticationType(out var type))
        {
            errors.Add($"ApiClient:Authentication:Type '{auth.Type}' é inválido. Valores suportados: ApiKey, Bearer, OAuth2.");
            return;
        }

        switch (type)
        {
            case AuthenticationType.ApiKey:
                if (string.IsNullOrWhiteSpace(auth.ApiKey))
                    errors.Add("ApiClient:Authentication:ApiKey é obrigatório quando Type = ApiKey.");
                if (string.IsNullOrWhiteSpace(auth.ApiKeyHeader) && string.IsNullOrWhiteSpace(auth.ApiKeyQueryParameter))
                    errors.Add("Informe ApiKeyHeader ou ApiKeyQueryParameter para definir onde a chave será enviada.");
                break;

            case AuthenticationType.Bearer:
                if (string.IsNullOrWhiteSpace(auth.BearerToken))
                    errors.Add("ApiClient:Authentication:BearerToken é obrigatório quando Type = Bearer.");
                break;

            case AuthenticationType.OAuth2:
                if (string.IsNullOrWhiteSpace(auth.ClientId))
                    errors.Add("ApiClient:Authentication:ClientId é obrigatório quando Type = OAuth2.");
                if (string.IsNullOrWhiteSpace(auth.ClientSecret))
                    errors.Add("ApiClient:Authentication:ClientSecret é obrigatório quando Type = OAuth2.");
                if (!Uri.TryCreate(auth.TokenEndpoint, UriKind.Absolute, out _))
                    errors.Add("ApiClient:Authentication:TokenEndpoint é obrigatório (URL absoluta) quando Type = OAuth2.");
                break;
        }
    }
}
