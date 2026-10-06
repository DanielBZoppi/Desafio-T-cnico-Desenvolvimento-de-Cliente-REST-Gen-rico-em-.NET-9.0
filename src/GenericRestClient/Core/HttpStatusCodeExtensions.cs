using System.Net;

namespace GenericRestClient.Core;

internal static class HttpStatusCodeExtensions
{
    public static bool IsTransient(this HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is 408 or 429 or >= 500;
    }
}
