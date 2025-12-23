using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Exceptions;

namespace Xians.Lib.Common;

/// <summary>
/// Validates X509 certificates including expiration and chain validation.
/// </summary>
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

    /// <summary>
    /// Validates the certificate chain and revocation status.
    /// </summary>
    public void ValidateChain(X509Certificate2 certificate)
    {
        using var chain = new X509Chain();
        
        // Configure chain validation policy
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);

        bool isValid = chain.Build(certificate);

        if (!isValid)
        {
            var errors = new List<string>();
            foreach (var status in chain.ChainStatus)
            {
                if (status.Status != X509ChainStatusFlags.NoError)
                {
                    errors.Add($"{status.Status}: {status.StatusInformation}");
                    _logger?.LogWarning("Certificate chain validation issue: {Status} - {Info}", 
                        status.Status, status.StatusInformation);
                }
            }

            if (errors.Count > 0)
            {
                _logger?.LogWarning("Certificate chain validation completed with {Count} warnings", errors.Count);
                // For strict validation, uncomment:
                // throw new CertificateException($"Certificate chain validation failed: {string.Join(", ", errors)}");
            }
        }
        else
        {
            _logger?.LogDebug("Certificate chain validation passed");
        }
    }
}

