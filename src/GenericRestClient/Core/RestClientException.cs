using System.Net;

namespace GenericRestClient.Core;


public class RestClientException : Exception
{
    public RestClientException(
        string message,
        HttpMethod? method = null,
        Uri? requestUri = null,
        HttpStatusCode? statusCode = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Method = method;
        RequestUri = requestUri;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpMethod? Method { get; }
    public Uri? RequestUri { get; }
    public HttpStatusCode? StatusCode { get; }
    public string? ResponseBody { get; }

    public bool IsTransient => StatusCode is { } code && code.IsTransient();
}
