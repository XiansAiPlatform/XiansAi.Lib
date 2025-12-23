using Xians.Lib.Common.Exceptions;

namespace Xians.Lib.Configuration.Models;

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
    /// <exception cref="ConfigurationException">Thrown if configuration is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl))
            throw new ConfigurationException("ServerUrl is required", nameof(ServerUrl));

        if (string.IsNullOrWhiteSpace(Namespace))
            throw new ConfigurationException("Namespace is required", nameof(Namespace));

        if (MaxRetryAttempts < 0)
            throw new ConfigurationException("MaxRetryAttempts must be non-negative", nameof(MaxRetryAttempts));

        if (RetryDelaySeconds < 0)
            throw new ConfigurationException("RetryDelaySeconds must be non-negative", nameof(RetryDelaySeconds));

        // Validate TLS configuration
        var hasCert = !string.IsNullOrWhiteSpace(CertificateBase64);
        var hasKey = !string.IsNullOrWhiteSpace(PrivateKeyBase64);

        if (hasCert != hasKey)
            throw new ConfigurationException(
                "Both CertificateBase64 and PrivateKeyBase64 must be provided together for TLS, or both omitted",
                "TLS");
    }
}


