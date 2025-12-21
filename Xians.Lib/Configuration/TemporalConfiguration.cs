namespace Xians.Lib.Configuration;

/// <summary>
/// Configuration for Temporal server connection.
/// </summary>
public class TemporalConfiguration
{
    /// <summary>
    /// The Temporal server URL (e.g., localhost:7233 or temporal.example.com:7233).
    /// </summary>
    public required string ServerUrl { get; set; }

    /// <summary>
    /// The Temporal namespace to use.
    /// </summary>
    public required string Namespace { get; set; }

    /// <summary>
    /// Optional TLS certificate in Base64 format for mTLS authentication.
    /// </summary>
    public string? CertificateBase64 { get; set; }

    /// <summary>
    /// Optional TLS private key in Base64 format for mTLS authentication.
    /// </summary>
    public string? PrivateKeyBase64 { get; set; }

    /// <summary>
    /// Maximum number of retry attempts for connection failures. Default is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts in seconds. Default is 5 seconds.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Whether TLS is enabled. Automatically determined based on certificate presence.
    /// </summary>
    public bool IsTlsEnabled => !string.IsNullOrWhiteSpace(CertificateBase64) && 
                                !string.IsNullOrWhiteSpace(PrivateKeyBase64);

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
            throw new InvalidOperationException("ServerUrl is required");

        if (string.IsNullOrWhiteSpace(Namespace))
            throw new InvalidOperationException("Namespace is required");

        if (MaxRetryAttempts < 0)
            throw new InvalidOperationException("MaxRetryAttempts must be non-negative");

        if (RetryDelaySeconds < 0)
            throw new InvalidOperationException("RetryDelaySeconds must be non-negative");

        // Validate TLS configuration
        var hasCert = !string.IsNullOrWhiteSpace(CertificateBase64);
        var hasKey = !string.IsNullOrWhiteSpace(PrivateKeyBase64);

        if (hasCert != hasKey)
            throw new InvalidOperationException(
                "Both CertificateBase64 and PrivateKeyBase64 must be provided together for TLS, or both omitted");
    }
}


