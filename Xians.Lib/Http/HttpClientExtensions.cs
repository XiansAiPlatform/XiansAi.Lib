namespace Xians.Lib.Http;

/// <summary>
/// Extension methods for resilient HTTP operations.
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Sends an HTTP request with automatic retry logic for transient failures.
    /// </summary>
    /// <param name="service">The HTTP client service.</param>
    /// <param name="request">The HTTP request message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        this IHttpClientService service,
        HttpRequestMessage request, 
        CancellationToken cancellationToken = default)
    {
        return await service.ExecuteWithRetryAsync(async () =>
        {
            var client = await service.GetHealthyClientAsync();
            var clonedRequest = await CloneHttpRequestMessageAsync(request);
            return await client.SendAsync(clonedRequest, cancellationToken);
        });
    }

    /// <summary>
    /// Performs a GET request with automatic retry logic.
    /// </summary>
    /// <param name="service">The HTTP client service.</param>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public static async Task<HttpResponseMessage> GetWithRetryAsync(
        this IHttpClientService service,
        string requestUri, 
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        return await service.SendWithRetryAsync(request, cancellationToken);
    }

    /// <summary>
    /// Performs a POST request with automatic retry logic.
    /// </summary>
    /// <param name="service">The HTTP client service.</param>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="content">The HTTP content to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public static async Task<HttpResponseMessage> PostWithRetryAsync(
        this IHttpClientService service,
        string requestUri, 
        HttpContent content, 
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = content };
        return await service.SendWithRetryAsync(request, cancellationToken);
    }

    /// <summary>
    /// Performs a PUT request with automatic retry logic.
    /// </summary>
    /// <param name="service">The HTTP client service.</param>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="content">The HTTP content to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public static async Task<HttpResponseMessage> PutWithRetryAsync(
        this IHttpClientService service,
        string requestUri, 
        HttpContent content, 
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, requestUri) { Content = content };
        return await service.SendWithRetryAsync(request, cancellationToken);
    }

    /// <summary>
    /// Performs a DELETE request with automatic retry logic.
    /// </summary>
    /// <param name="service">The HTTP client service.</param>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public static async Task<HttpResponseMessage> DeleteWithRetryAsync(
        this IHttpClientService service,
        string requestUri, 
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        return await service.SendWithRetryAsync(request, cancellationToken);
    }

    /// <summary>
    /// Performs a PATCH request with automatic retry logic.
    /// </summary>
    /// <param name="service">The HTTP client service.</param>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="content">The HTTP content to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public static async Task<HttpResponseMessage> PatchWithRetryAsync(
        this IHttpClientService service,
        string requestUri, 
        HttpContent content, 
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, requestUri) { Content = content };
        return await service.SendWithRetryAsync(request, cancellationToken);
    }

    private static Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Content = original.Content,
            Version = original.Version
        };

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in original.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        return Task.FromResult(clone);
    }
}


