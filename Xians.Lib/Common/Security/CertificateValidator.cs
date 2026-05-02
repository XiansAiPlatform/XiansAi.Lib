using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Xians.Lib.Common.Security;

/// <summary>
/// Validates X509 certificates used as client API keys.
/// </summary>
/// <remarks>
/// The API key certificate is a self-issued identity token that the client supplies
/// to itself; the authoritative trust decision is made server-side when the
/// certificate is presented in the Authorization header. As a result, this
/// validator only checks properties the client can meaningfully verify locally
/// (e.g. expiration). Chain / revocation validation is intentionally not
/// performed here — there is no trust anchor on the client to validate against.
/// </remarks>
internal class CertificateValidator
{
    private readonly ILogger? _logger;

    public CertificateValidator(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates certificate expiration dates.
    /// </summary>
    public void ValidateExpiration(X509Certificate2 certificate)
    {
        CertificateValidationHelper.ValidateExpiration(certificate, _logger);
    }
}

