using Xians.Lib.Configuration.Models;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Security.Models;
using Xians.Lib.Common.Security;
using Microsoft.Extensions.Logging;
using System.Reflection;

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
    /// Setting this property automatically initializes server logging when services are available.
    /// </summary>
    public LogLevel? ServerLogLevel { get; set; }

    /// <summary>
    /// Gets or sets whether to enable task functionality for agents.
    /// When true, agents will be initialized with task capabilities.
    /// Default is true.
    /// </summary>
    public bool EnableTasks { get; set; } = true;

    /// <summary>
    /// Enables local/mock mode for unit testing.
    /// When true, operations resolve from embedded resources instead of making server calls.
    /// This allows running tests without a real Xians server.
    /// </summary>
    /// <remarks>
    /// In LocalMode:
    /// - Knowledge methods load from embedded resources (convention: {AgentName}.Knowledge.{Name}.{ext})
    /// - No HTTP calls are made to the server
    /// - Updates/creates are stored in-memory only
    /// - Temporal workflows are not executed
    /// </remarks>
    public bool LocalMode { get; set; } = false;

    /// <summary>
    /// Optional assemblies to search for embedded resources in local mode.
    /// If not provided, searches all loaded non-system assemblies.
    /// </summary>
    /// <remarks>
    /// Specify this to improve performance and avoid searching unnecessary assemblies.
    /// Example: new[] { typeof(MyTests).Assembly, typeof(MyAgent).Assembly }
    /// </remarks>
    public Assembly[]? LocalModeAssemblies { get; set; }
    
    
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
