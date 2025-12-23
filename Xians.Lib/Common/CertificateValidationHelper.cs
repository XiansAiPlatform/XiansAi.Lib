using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Exceptions;

namespace Xians.Lib.Common;

/// <summary>
/// Helper for certificate expiration validation.
/// </summary>
public static class CertificateValidationHelper
{
    /// <summary>
    /// Validates that a certificate is within its valid time period.
    /// </summary>
    public static void ValidateExpiration(X509Certificate2 certificate, ILogger? logger = null)
    {
        var now = DateTime.UtcNow;

        if (certificate.NotAfter < now)
        {
            logger?.LogError("Certificate expired on {ExpirationDate}", certificate.NotAfter);
            throw new CertificateException(
                $"Certificate has expired on {certificate.NotAfter:yyyy-MM-dd}");
        }

        if (certificate.NotBefore > now)
        {
            logger?.LogError("Certificate not valid until {ValidFrom}", certificate.NotBefore);
            throw new CertificateException(
                $"Certificate is not yet valid until {certificate.NotBefore:yyyy-MM-dd}");
        }
    }
}

