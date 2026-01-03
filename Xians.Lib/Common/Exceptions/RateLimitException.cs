namespace Xians.Lib.Common.Exceptions;

/// <summary>
/// Exception thrown when a rate limit is exceeded.
/// Contains information about how long to wait before retrying.
/// </summary>
public class RateLimitException : Exception
{
    /// <summary>
    /// Gets the number of seconds to wait before retrying, as specified by the server.
    /// </summary>
    public int RetryAfterSeconds { get; }

    /// <summary>
    /// Gets the HTTP status code (typically 429).
    /// </summary>
    public int StatusCode { get; }

    public RateLimitException(string message, int retryAfterSeconds, int statusCode = 429)
        : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
        StatusCode = statusCode;
    }

    public RateLimitException(string message, int retryAfterSeconds, int statusCode, Exception innerException)
        : base(message, innerException)
    {
        RetryAfterSeconds = retryAfterSeconds;
        StatusCode = statusCode;
    }
}


