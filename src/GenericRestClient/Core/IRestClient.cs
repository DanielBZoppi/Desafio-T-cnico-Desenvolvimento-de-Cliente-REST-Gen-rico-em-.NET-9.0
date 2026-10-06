namespace GenericRestClient.Core;


public interface IRestClient
{
    Task<T?> GetAsync<T>(
        string endpoint,
        CancellationToken cancellationToken = default);

    Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest body,
        CancellationToken cancellationToken = default);

    Task<TResponse?> PutAsync<TRequest, TResponse>(
        string endpoint,
        TRequest body,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string endpoint,
        CancellationToken cancellationToken = default);
}
