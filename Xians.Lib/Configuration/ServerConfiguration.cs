namespace Xians.Lib.Configuration;

/// <summary>
/// Configuration for the application server connection.
/// </summary>
public class ServerConfiguration
{
    /// <summary>
    /// The base URL of the application server (e.g., https://api.example.com).
    /// </summary>
    public required string ServerUrl { get; set; }

    /// <summary>
    /// The API key or certificate in Base64 format for authentication.
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// Maximum number of retry attempts for failed requests. Default is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retry attempts in seconds. Uses exponential backoff. Default is 2 seconds.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 2;

    /// <summary>
    /// HTTP request timeout in seconds. Default is 300 seconds (5 minutes).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum connections per server. Default is 10.
    /// </summary>
    public int MaxConnectionsPerServer { get; set; } = 10;

    /// <summary>
    /// Connection pool lifetime in minutes. Default is 15 minutes.
    /// </summary>
    public int PooledConnectionLifetimeMinutes { get; set; } = 15;

    /// <summary>
    /// Connection idle timeout in minutes. Default is 2 minutes.
    /// </summary>
    public int PooledConnectionIdleTimeoutMinutes { get; set; } = 2;

    /// <summary>
    /// Health check interval in minutes. Default is 1 minute.
    /// </summary>
    public int HealthCheckIntervalMinutes { get; set; } = 1;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
            throw new InvalidOperationException("ServerUrl is required");

        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("ApiKey is required");

        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException($"ServerUrl '{ServerUrl}' is not a valid URL");

        if (MaxRetryAttempts < 0)
            throw new InvalidOperationException("MaxRetryAttempts must be non-negative");

        if (RetryDelaySeconds < 0)
            throw new InvalidOperationException("RetryDelaySeconds must be non-negative");

        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException("TimeoutSeconds must be positive");
    }
}

