using System.Net;

namespace Xians.Lib.Common.Exceptions;

/// <summary>
/// Exception thrown when HTTP service operations fail.
/// Wraps HTTP-specific errors with additional context.
/// </summary>
public class HttpServiceException : XiansException
{
    /// <summary>
    /// Gets the HTTP status code if available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Gets the endpoint that was being accessed.
    /// </summary>
    public string? Endpoint { get; }

    /// <summary>
    /// Gets whether this is a transient error that should be retried.
    /// </summary>
    public bool IsTransient { get; }

    public HttpServiceException(string message) : base(message) { }

    public HttpServiceException(string message, Exception innerException) 
        : base(message, innerException) 
    {
        // Auto-detect transient errors from HttpRequestException
        if (innerException is HttpRequestException httpEx)
        {
            StatusCode = httpEx.StatusCode;
            IsTransient = IsTransientStatusCode(httpEx.StatusCode);
        }
    }

    public HttpServiceException(
        string message,
        HttpStatusCode? statusCode = null,
        string? endpoint = null,
        bool isTransient = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Endpoint = endpoint;
        IsTransient = isTransient;
    }

    private static bool IsTransientStatusCode(HttpStatusCode? statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.RequestTimeout => true,
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            _ => false
        };
    }
}
