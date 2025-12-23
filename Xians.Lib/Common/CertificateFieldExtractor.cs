using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Xians.Lib.Common;

/// <summary>
/// Extracts specific fields (tenant ID, user ID) from X509 certificates.
/// </summary>
internal class CertificateFieldExtractor
{
    private readonly ILogger? _logger;

    public CertificateFieldExtractor(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts the tenant ID from the certificate's Organization (O=) field.
    /// </summary>
    public string? ExtractTenantId(X509Certificate2 certificate)
    {
        return ExtractField(certificate, "O=", "tenant ID");
    }

    /// <summary>
    /// Extracts the user ID from the certificate's Organizational Unit (OU=) field.
    /// </summary>
    public string? ExtractUserId(X509Certificate2 certificate)
    {
        return ExtractField(certificate, "OU=", "user ID");
    }

    /// <summary>
    /// Extracts a specific field from the certificate subject.
    /// </summary>
    private string? ExtractField(X509Certificate2 certificate, string fieldPrefix, string fieldName)
    {
        try
        {
            var distinguishedName = new X500DistinguishedName(certificate.SubjectName.RawData);
            var subject = distinguishedName.Name;
            var subjectParts = subject.Split(',');

            foreach (var part in subjectParts)
            {
                var trimmedPart = part.Trim();

                if (trimmedPart.StartsWith(fieldPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var value = trimmedPart.Substring(fieldPrefix.Length).Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to extract {FieldName} from certificate", fieldName);
            return null;
        }
    }
}

