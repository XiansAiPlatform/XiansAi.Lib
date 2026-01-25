using Xians.Lib.Configuration.Models;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Security.Models;
using Xians.Lib.Common.Security;
using Microsoft.Extensions.Logging;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Configuration options for initializing the Xians platform.
/// Inherits server configuration settings from ServerConfiguration.
/// </summary>
public class XiansOptions : ServerConfiguration
{
    private CertificateInfo? _certificateInfo;

    /// <summary>
    /// Optional Temporal configuration. If not provided, will be fetched from the server.
    /// </summary>
    public TemporalConfiguration? TemporalConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the cache configuration.
    /// If not provided, uses default caching settings (5-minute TTL, enabled).
    /// </summary>
    public CacheOptions? Cache { get; set; }

    /// <summary>
    /// Gets or sets the console log level.
    /// If not provided, falls back to CONSOLE_LOG_LEVEL environment variable or Debug.
    /// </summary>
    public LogLevel? ConsoleLogLevel { get; set; }

    /// <summary>
    /// Gets or sets the server log level (threshold for uploading logs to server).
    /// If not provided, falls back to SERVER_LOG_LEVEL (or legacy API_LOG_LEVEL) environment variable or Error.
    /// </summary>
    public LogLevel? ServerLogLevel { get; set; }
    
    /// <summary>
    /// The tenant ID extracted from the API key certificate.
    /// This represents the tenant that owns this API key.
    /// </summary>
    public string CertificateTenantId 
    { 
        get
        {
            // Extract from certificate if not set
            if (_certificateInfo == null)
            {
                var reader = new CertificateReader();
                _certificateInfo = reader.ReadCertificate(ApiKey);
            }

            return _certificateInfo.TenantId;
        }
    }

    /// <summary>
    /// Gets the certificate information extracted from the API key.
    /// </summary>
    public CertificateInfo CertificateInfo
    {
        get
        {
            if (_certificateInfo == null)
            {
                var reader = new CertificateReader();
                _certificateInfo = reader.ReadCertificate(ApiKey);
            }
            return _certificateInfo;
        }
    }
}
