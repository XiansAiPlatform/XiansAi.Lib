using System.Security.Cryptography.X509Certificates;

namespace Xians.Lib.Common.Models;

/// <summary>
/// Contains information extracted from a certificate.
/// </summary>
public class CertificateInfo
{
    public required X509Certificate2 Certificate { get; set; }
    public required string TenantId { get; set; }
    public required string UserId { get; set; }
    public required string Subject { get; set; }
    public required string Thumbprint { get; set; }
    public DateTime ExpiresAt { get; set; }
}

