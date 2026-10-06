using System.Net.Http.Json;
using System.Text.Json;

namespace GenericRestClient.Core;


internal sealed class RestClient(HttpClient httpClient) : IRestClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(endpoint));
        return await SendAsync<T>(request, cancellationToken);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint, TRequest body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(endpoint))
        {
            Content = JsonContent.Create(body, mediaType: null, options: JsonOptions)
        };
        return await SendAsync<TResponse>(request, cancellationToken);
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(
        string endpoint, TRequest body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, BuildUri(endpoint))
        {
            Content = JsonContent.Create(body, mediaType: null, options: JsonOptions)
        };
        return await SendAsync<TResponse>(request, cancellationToken);
    }

    public async Task DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildUri(endpoint));
        using var response = await SendCoreAsync(request, cancellationToken);
    }


    private static Uri BuildUri(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        if (!Uri.TryCreate(endpoint.TrimStart('/'), UriKind.Relative, out var uri))
            throw new ArgumentException($"O endpoint '{endpoint}' deve ser um caminho relativo à BaseUrl.", nameof(endpoint));

        return uri;
    }

    private async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(request, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
            return default; 

        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new RestClientException(
                $"Não foi possível interpretar a resposta de {request.Method} {request.RequestUri} como {typeof(T).Name}.",
                request.Method, request.RequestUri, response.StatusCode, content, ex);
        }
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return response;

        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new RestClientException(
                $"{request.Method} {request.RequestUri} falhou com HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                request.Method, request.RequestUri, response.StatusCode, body);
        }
        finally
        {
            response.Dispose();
        }
    }
}
