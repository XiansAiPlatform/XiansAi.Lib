using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Xians.Lib.Common.Security;

/// <summary>
/// Generates test certificates for local mode and unit testing.
/// These certificates are NOT suitable for production use.
/// </summary>
internal static class CertificateGenerator
{
    /// <summary>
    /// Generates a self-signed test certificate with tenant and user information.
    /// Returns the certificate as a base64-encoded string (PFX format).
    /// </summary>
    /// <param name="tenantId">The tenant ID to embed in the certificate.</param>
    /// <param name="userId">The user ID to embed in the certificate.</param>
    /// <returns>Base64-encoded certificate string.</returns>
    public static string GenerateTestCertificate(string tenantId, string userId)
    {
        // Create a self-signed certificate with required fields
        using var rsa = RSA.Create(2048);
        
        var request = new CertificateRequest(
            $"CN={userId}, OU={userId}, O={tenantId}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Add basic constraints
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        // Add key usage
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));

        // Create certificate valid for 1 year
        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        // Export as PFX (with private key) and convert to base64
        var pfxBytes = certificate.Export(X509ContentType.Pfx, string.Empty);
        return Convert.ToBase64String(pfxBytes);
    }
}
