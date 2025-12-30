namespace Xians.Lib.Http;

/// <summary>
/// Interface for HTTP client service with resilient connection management.
/// </summary>
public interface IHttpClientService : IDisposable
{
    /// <summary>
    /// Gets the configured HTTP client.
    /// </summary>
    HttpClient Client { get; }

    /// <summary>
    /// Gets a healthy HTTP client, automatically reconnecting if necessary.
    /// </summary>
    /// <returns>A healthy HTTP client instance.</returns>
    Task<HttpClient> GetHealthyClientAsync();

    /// <summary>
    /// Tests the connection to the server with retry logic.
    /// </summary>
    Task TestConnectionAsync();

    /// <summary>
    /// Performs an asynchronous health check on the connection.
    /// </summary>
    /// <returns>True if the connection is healthy; otherwise, false.</returns>
    Task<bool> IsHealthyAsync();

    /// <summary>
    /// Executes an HTTP operation with automatic retry logic for transient failures.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation);

    /// <summary>
    /// Executes an HTTP operation with automatic retry logic for transient failures.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    Task ExecuteWithRetryAsync(Func<Task> operation);

    /// <summary>
    /// Forces a reconnection on the next operation.
    /// </summary>
    Task ForceReconnectAsync();
}



