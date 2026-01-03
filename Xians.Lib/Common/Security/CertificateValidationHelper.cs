using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Exceptions;

namespace Xians.Lib.Common.Security;

/// <summary>
/// Helper for certificate expiration validation.
/// </summary>
public static class CertificateValidationHelper
{
    /// <summary>
    /// Validates that a certificate is within its valid time period.
    /// Allows a 15-minute clock skew tolerance to handle time sync issues.
    /// </summary>
    public static void ValidateExpiration(X509Certificate2 certificate, ILogger? logger = null)
    {
        var now = DateTime.UtcNow;
        var clockSkewTolerance = TimeSpan.FromMinutes(15);

        // Normalize certificate dates to UTC for comparison
        var notBefore = certificate.NotBefore.ToUniversalTime();
        var notAfter = certificate.NotAfter.ToUniversalTime();

        logger?.LogDebug(
            "Certificate validation - Now: {Now:O} ({NowKind}), NotBefore: {NotBefore:O} ({NotBeforeKind}), NotAfter: {NotAfter:O} ({NotAfterKind})", 
            now, now.Kind, notBefore, notBefore.Kind, notAfter, notAfter.Kind);

        if (notAfter < now.Subtract(clockSkewTolerance))
        {
            logger?.LogError("Certificate expired on {ExpirationDate}", notAfter);
            throw new CertificateException(
                $"Certificate has expired on {notAfter:yyyy-MM-dd}");
        }

        if (notBefore > now.Add(clockSkewTolerance))
        {
            logger?.LogError("Certificate not valid until {ValidFrom} (Current UTC: {Now}, Tolerance: {Tolerance} min)", 
                notBefore, now, clockSkewTolerance.TotalMinutes);
            throw new CertificateException(
                $"Certificate is not yet valid until {notBefore:yyyy-MM-dd}");
        }
    }
}

