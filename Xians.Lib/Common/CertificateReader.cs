using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Xians.Lib.Common;

/// <summary>
/// Service for reading and parsing X509 certificates from base64-encoded strings.
/// Extracts tenant ID and user ID from certificate subject fields.
/// </summary>
public class CertificateReader
{
    private readonly ILogger<CertificateReader> _logger;
    private static readonly ConcurrentDictionary<string, Lazy<CertificateInfo>> _certificateCache = new();

    public CertificateReader(ILogger<CertificateReader>? logger = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<CertificateReader>();
    }

    /// <summary>
    /// Reads and parses a certificate from a base64-encoded string.
    /// </summary>
    /// <param name="base64EncodedCertificate">The base64-encoded certificate (API key).</param>
    /// <returns>Parsed certificate information including tenant ID and user ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown if certificate is invalid or missing required fields.</exception>
    public CertificateInfo ReadCertificate(string base64EncodedCertificate)
    {
        if (string.IsNullOrEmpty(base64EncodedCertificate))
        {
            throw new ArgumentException("Certificate cannot be null or empty", nameof(base64EncodedCertificate));
        }

        // Normalize the base64 string to eliminate whitespace issues
        var normalizedKey = base64EncodedCertificate
            .Replace(" ", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace("\t", "");

        // Use Lazy<T> with GetOrAdd to ensure single execution even under concurrency
        var lazyCertificate = _certificateCache.GetOrAdd(normalizedKey, key => 
            new Lazy<CertificateInfo>(() => ParseCertificate(key)));

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

            // Extract tenant ID and user ID from the certificate subject
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

            _logger.LogInformation("Certificate parsed successfully for user: '{UserId}', tenant: '{TenantId}'", 
                userId, tenantId);
            _logger.LogTrace("Certificate details - Subject: {Subject}, Thumbprint: {Thumbprint}, Expires: {Expires}",
                certificate.Subject, certificate.Thumbprint, certificate.NotAfter);

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
    /// Extracts the tenant ID from the certificate's Organization (O=) field.
    /// </summary>
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

                // Look for Organization (O=) field - this is where tenant ID is stored
                if (trimmedPart.StartsWith("O=", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Extracts the user ID from the certificate's Organizational Unit (OU=) field.
    /// </summary>
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
                if (trimmedPart.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Clears the certificate cache. Useful for testing or when certificates need to be refreshed.
    /// </summary>
    public static void ClearCache()
    {
        _certificateCache.Clear();
    }
}

