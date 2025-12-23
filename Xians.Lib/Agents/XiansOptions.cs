using Xians.Lib.Common;
using Xians.Lib.Common.Models;
using Xians.Lib.Configuration.Models;

namespace Xians.Lib.Agents;

/// <summary>
/// Configuration options for initializing the Xians platform.
/// Inherits server configuration settings from ServerConfiguration.
/// </summary>
public class XiansOptions : ServerConfiguration
{
    private string? _tenantId;
    private CertificateInfo? _certificateInfo;

    /// <summary>
    /// Optional Temporal configuration. If not provided, will be fetched from the server.
    /// </summary>
    public TemporalConfiguration? TemporalConfiguration { get; set; }
    
    /// <summary>
    /// The tenant ID for this agent. 
    /// If not explicitly set, will be automatically extracted from the API key certificate.
    /// </summary>
    public string TenantId 
    { 
        get
        {
            if (_tenantId != null)
            {
                return _tenantId;
            }

            // Extract from certificate if not set
            if (_certificateInfo == null)
            {
                var reader = new CertificateReader();
                _certificateInfo = reader.ReadCertificate(ApiKey);
            }

            return _certificateInfo.TenantId;
        }
        set
        {
            _tenantId = value;
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

