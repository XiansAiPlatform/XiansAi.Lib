using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Models;

namespace Xians.Lib.Common;

/// <summary>
/// Service for reading and parsing X509 certificates from base64-encoded strings.
/// Extracts tenant ID and user ID from certificate subject fields.
/// </summary>
public class CertificateReader
{
    private readonly ILogger<CertificateReader> _logger;
    private readonly CertificateCache _cache;
    private readonly CertificateParser _parser;

    public CertificateReader(ILogger<CertificateReader>? logger = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<CertificateReader>();
        _cache = new CertificateCache(_logger);
        _parser = new CertificateParser(_logger);
    }

    /// <summary>
    /// Reads and parses a certificate from a base64-encoded string.
    /// </summary>
    /// <param name="base64EncodedCertificate">The base64-encoded certificate (API key).</param>
    /// <returns>Parsed certificate information including tenant ID and user ID.</returns>
    /// <exception cref="CertificateException">Thrown if certificate is invalid or missing required fields.</exception>
    public CertificateInfo ReadCertificate(string base64EncodedCertificate)
    {
        if (string.IsNullOrEmpty(base64EncodedCertificate))
        {
            throw new ArgumentException("Certificate cannot be null or empty", nameof(base64EncodedCertificate));
        }

        // Normalize the base64 string to eliminate whitespace issues
        var normalizedKey = base64EncodedCertificate
            .Replace(" ", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Replace("\t", "");

        // Check cache first
        if (_cache.TryGet(normalizedKey, out var certificateInfo) && certificateInfo != null)
        {
            return certificateInfo;
        }

        // Parse certificate and add to cache
        var certInfo = _parser.Parse(normalizedKey);
        _cache.Add(normalizedKey, certInfo);
        
        return certInfo;
    }

    /// <summary>
    /// Clears the certificate cache. Useful for testing or when certificates need to be refreshed.
    /// </summary>
    public static void ClearCache()
    {
        CertificateCache.Clear();
    }
}
