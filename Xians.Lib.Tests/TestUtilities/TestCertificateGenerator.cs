using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Xians.Lib.Tests.TestUtilities;

/// <summary>
/// Generates test certificates for unit and integration testing.
/// </summary>
public static class TestCertificateGenerator
{
    /// <summary>
    /// Generates a self-signed certificate for testing and returns it as Base64.
    /// The certificate will be valid from 1 year ago to 100 years in the future.
    /// </summary>
    /// <param name="tenantId">Optional tenant ID to include in Organization field.</param>
    /// <param name="userId">Optional user ID to include in Common Name field.</param>
    /// <returns>Base64-encoded certificate bytes.</returns>
    public static string GenerateTestCertificateBase64(string? tenantId = null, string? userId = null)
    {
        using var rsa = RSA.Create(2048);
        
        // Certificate must have:
        // - O= (Organization) for tenant ID
        // - OU= (Organizational Unit) for user ID
        var request = new CertificateRequest(
            $"CN=Test Certificate, OU={userId ?? "test-user"}, O={tenantId ?? "test-tenant"}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Valid from 1 year ago to 100 years in the future
        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddYears(-1),
            DateTimeOffset.UtcNow.AddYears(100));

        var certBytes = certificate.Export(X509ContentType.Pfx);
        return Convert.ToBase64String(certBytes);
    }

    /// <summary>
    /// Gets a reusable test certificate (Base64 encoded).
    /// This certificate is cached for performance.
    /// </summary>
    private static readonly Lazy<string> _testCertificate = 
        new(() => GenerateTestCertificateBase64());

    public static string GetTestCertificate() => _testCertificate.Value;
}



