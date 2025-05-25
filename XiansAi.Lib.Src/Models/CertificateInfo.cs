using System.Security.Cryptography.X509Certificates;

namespace XiansAi.Models;

public class CertificateInfo
{
    public X509Certificate2 Certificate { get; set; } = null!;
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}