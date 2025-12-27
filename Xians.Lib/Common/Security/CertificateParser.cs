using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Models;
using Xians.Lib.Common.Exceptions;
using Xians.Lib.Common.Security.Models;

namespace Xians.Lib.Common.Security;

/// <summary>
/// Parses and validates X509 certificates from base64-encoded strings.
/// </summary>
internal class CertificateParser
{
    private readonly CertificateValidator _validator;
    private readonly CertificateFieldExtractor _fieldExtractor;
    private readonly ILogger? _logger;

    public CertificateParser(ILogger? logger = null)
    {
        _logger = logger;
        _validator = new CertificateValidator(logger);
        _fieldExtractor = new CertificateFieldExtractor(logger);
    }

    /// <summary>
    /// Parses a certificate from a base64-encoded string and extracts relevant information.
    /// </summary>
    public CertificateInfo Parse(string base64EncodedCertificate)
    {
        X509Certificate2? certificate = null;
        try
        {
            // Decode the base64 encoded certificate
            var certificateBytes = Convert.FromBase64String(base64EncodedCertificate);

            // Load the certificate with platform-appropriate flags
            // EphemeralKeySet is not supported on macOS, so use MachineKeySet as fallback
#pragma warning disable SYSLIB0057
            var keyStorageFlags = OperatingSystem.IsMacOS() 
                ? X509KeyStorageFlags.MachineKeySet 
                : X509KeyStorageFlags.EphemeralKeySet;
            certificate = new X509Certificate2(certificateBytes, (string?)null, keyStorageFlags);
#pragma warning restore SYSLIB0057

            // Validate certificate
            _validator.ValidateExpiration(certificate);
            _validator.ValidateChain(certificate);

            // Extract tenant ID and user ID
            var tenantId = _fieldExtractor.ExtractTenantId(certificate);
            var userId = _fieldExtractor.ExtractUserId(certificate);

            if (tenantId == null)
            {
                _logger?.LogError("Failed to extract tenant ID from certificate");
                throw new CertificateException("Certificate validation failed: missing required attributes");
            }

            if (userId == null)
            {
                _logger?.LogError("Failed to extract user ID from certificate");
                throw new CertificateException("Certificate validation failed: missing required attributes");
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

            _logger?.LogDebug("Certificate parsed successfully for user: '{UserId}', tenant: '{TenantId}'", 
                userId, tenantId);
            _logger?.LogTrace("Certificate details - Subject: {Subject}, Thumbprint: {Thumbprint}, Expires: {Expires}",
                certificate.Subject, certificate.Thumbprint, certificate.NotAfter);

            return certificateInfo;
        }
        catch (FormatException ex)
        {
            certificate?.Dispose();
            _logger?.LogError(ex, "Failed to decode base64 certificate");
            throw new CertificateException("Invalid base64 encoded certificate", ex);
        }
        catch (CertificateException)
        {
            certificate?.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            certificate?.Dispose();
            _logger?.LogError(ex, "Failed to read certificate");
            throw new CertificateException("Failed to load certificate", ex);
        }
    }
}

