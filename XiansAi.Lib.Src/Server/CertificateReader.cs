using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;
using XiansAi.Models;
using Microsoft.Extensions.Logging;

namespace XiansAi.Server;

public class CertificateReader
{
    private readonly ILogger<CertificateReader> _logger;
    private static readonly ConcurrentDictionary<string, Lazy<CertificateInfo>> _certificateCache = new();

    public CertificateReader()
    {
        _logger = Globals.LogFactory.CreateLogger<CertificateReader>();
    }

    public CertificateInfo? ReadCertificate(string? base64EncodedCertificate = null)
    {
        if (string.IsNullOrEmpty(base64EncodedCertificate))
        {
            base64EncodedCertificate = PlatformConfig.APP_SERVER_API_KEY ?? throw new InvalidOperationException("App server API key is not set");
        }

        // Normalize the base64 string to eliminate whitespace issues
        var normalizedKey = base64EncodedCertificate.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");
        

        // Use Lazy<T> with GetOrAdd to ensure single execution even under concurrency
        var lazyCertificate = _certificateCache.GetOrAdd(normalizedKey, key => new Lazy<CertificateInfo>(() => {
            return ParseCertificate(key);
        }));
        
        return lazyCertificate.Value;
    }

    private CertificateInfo ParseCertificate(string base64EncodedCertificate)
    {
        try
        {            
            // Decode the base64 encoded certificate
            var certificateBytes = Convert.FromBase64String(base64EncodedCertificate);

            // Load the certificate
#pragma warning disable SYSLIB0057 // Type or member is obsolete
            var certificate = new X509Certificate2(certificateBytes);
#pragma warning restore SYSLIB0057 // Type or member is obsolete

            // Extract tenant ID from the certificate subject
            var tenantId = ExtractTenantIdFromCertificate(certificate);
            var userId = ExtractUserIdFromCertificate(certificate);
            
            // Security: Use generic error messages to avoid information disclosure
            if (tenantId == null)
            {
                _logger.LogError("Failed to extract tenant ID from certificate");
                throw new InvalidOperationException("Certificate validation failed: missing required attributes");
            }
            
            if (userId == null)
            {
                _logger.LogError("Failed to extract user ID from certificate");
                throw new InvalidOperationException("Certificate validation failed: missing required attributes");
            }
            
            var certificateInfo = new CertificateInfo
            {
                Certificate = certificate,
                TenantId = tenantId,
                UserId = userId,
                Subject = certificate.Subject,
                Thumbprint = certificate.Thumbprint,
                ExpiresAt = certificate.NotAfter
            };

            _logger.LogInformation($"Certificate parsed successfully for user: `{userId}`, tenant: `{tenantId}`");

            _logger.LogTrace($"Certificate parsed and cached - Subject: {certificate.Subject}, Thumbprint: {certificate.Thumbprint}, Expires: {certificate.NotAfter}");
            
            return certificateInfo;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decode base64 certificate");
            throw new InvalidOperationException("Invalid base64 encoded certificate", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read certificate");
            throw new InvalidOperationException("Failed to load certificate", ex);
        }
    }

    /// <summary>
    /// Clears the certificate cache. Useful for testing or when certificates need to be refreshed.
    /// </summary>
    public static void ClearCache()
    {
        _certificateCache.Clear();
    }
    
    private string? ExtractTenantIdFromCertificate(X509Certificate2 certificate)
    {
        try
        {
            // Parse the subject name to extract tenant ID
            var subject = certificate.Subject;
            
            // Split the subject into parts
            var subjectParts = subject.Split(',');
            
            foreach (var part in subjectParts)
            {
                var trimmedPart = part.Trim();
                
                // Look for Organization (O=) field first - this is where tenant ID is typically stored
                if (trimmedPart.StartsWith("O="))
                {
                    var organizationValue = trimmedPart.Substring(2);
                    if (!string.IsNullOrEmpty(organizationValue))
                    {
                        return organizationValue;
                    }
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract tenant ID from certificate");
            return null;
        }
    }

    private string? ExtractUserIdFromCertificate(X509Certificate2 certificate)
    {
        try
        {
            // Parse the subject name to extract user ID
            var subject = certificate.Subject;
            
            // Split the subject into parts
            var subjectParts = subject.Split(',');
            
            foreach (var part in subjectParts)
            {
                var trimmedPart = part.Trim();
                
                // Look for Organizational Unit (OU=) field - this is where user ID is stored
                if (trimmedPart.StartsWith("OU="))
                {
                    var organizationalUnitValue = trimmedPart.Substring(3);
                    if (!string.IsNullOrEmpty(organizationalUnitValue))
                    {
                        return organizationalUnitValue;
                    }
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract user ID from certificate");
            return null;
        }
    }
}
