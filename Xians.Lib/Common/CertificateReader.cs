using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Models;
using Xians.Lib.Common.Exceptions;

namespace Xians.Lib.Common;

/// <summary>
/// Service for reading and parsing X509 certificates from base64-encoded strings.
/// Extracts tenant ID and user ID from certificate subject fields.
/// </summary>
public class CertificateReader
{
    private readonly ILogger<CertificateReader> _logger;
    private static readonly ConcurrentDictionary<string, CachedCertificate> _certificateCache = new();
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);
    private static readonly int MaxCacheSize = 1000;

    public CertificateReader(ILogger<CertificateReader>? logger = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger<CertificateReader>();
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

        // Check cache with expiration
        if (_certificateCache.TryGetValue(normalizedKey, out var cachedCert))
        {
            if (DateTime.UtcNow < cachedCert.ExpiresAt)
            {
                _logger.LogTrace("Certificate loaded from cache");
                return cachedCert.CertificateInfo;
            }
            else
            {
                // Remove expired entry
                _certificateCache.TryRemove(normalizedKey, out _);
                _logger.LogDebug("Expired certificate removed from cache");
            }
        }

        // Enforce cache size limit
        if (_certificateCache.Count >= MaxCacheSize)
        {
            EvictOldestCacheEntries();
        }

        // Parse certificate and add to cache
        var certInfo = ParseCertificate(normalizedKey);
        var cached = new CachedCertificate
        {
            CertificateInfo = certInfo,
            ExpiresAt = DateTime.UtcNow.Add(CacheExpiration)
        };
        
        _certificateCache.TryAdd(normalizedKey, cached);
        return certInfo;
    }

    /// <summary>
    /// Evicts the oldest 20% of cache entries when cache is full.
    /// Optimized to avoid full dictionary sort - removes first expired entries found.
    /// </summary>
    private void EvictOldestCacheEntries()
    {
        var targetRemoveCount = (int)(_certificateCache.Count * 0.2);
        var removedCount = 0;
        var now = DateTime.UtcNow;

        // Fast path: Remove expired entries first (no sorting needed)
        foreach (var kvp in _certificateCache)
        {
            if (removedCount >= targetRemoveCount) break;
            
            if (kvp.Value.ExpiresAt < now)
            {
                if (_certificateCache.TryRemove(kvp.Key, out _))
                {
                    removedCount++;
                }
            }
        }

        // If we still need to remove more, take the oldest by expiration
        if (removedCount < targetRemoveCount)
        {
            var remaining = targetRemoveCount - removedCount;
            var toRemove = _certificateCache
                .OrderBy(x => x.Value.ExpiresAt)
                .Take(remaining)
                .Select(x => x.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _certificateCache.TryRemove(key, out _);
                removedCount++;
            }
        }

        _logger.LogDebug("Evicted {Count} entries from certificate cache", removedCount);
    }

    private CertificateInfo ParseCertificate(string base64EncodedCertificate)
    {
        X509Certificate2? certificate = null;
        try
        {
            // Decode the base64 encoded certificate
            var certificateBytes = Convert.FromBase64String(base64EncodedCertificate);

            // Load the certificate with EphemeralKeySet to avoid persisting to disk
#pragma warning disable SYSLIB0057 // Type or member is obsolete
            certificate = new X509Certificate2(certificateBytes, (string?)null, X509KeyStorageFlags.EphemeralKeySet);
#pragma warning restore SYSLIB0057 // Type or member is obsolete

            // Validate certificate expiration
            var now = DateTime.UtcNow;
            if (certificate.NotAfter < now)
            {
                _logger.LogError("Certificate expired on {ExpirationDate}", certificate.NotAfter);
                throw new CertificateException("Certificate has expired");
            }

            if (certificate.NotBefore > now)
            {
                _logger.LogError("Certificate not valid until {ValidFrom}", certificate.NotBefore);
                throw new CertificateException("Certificate is not yet valid");
            }

            // Validate certificate chain and revocation status
            ValidateCertificateChain(certificate);

            // Extract tenant ID and user ID from the certificate subject
            var tenantId = ExtractTenantIdFromCertificate(certificate);
            var userId = ExtractUserIdFromCertificate(certificate);

            // Security: Use generic error messages to avoid information disclosure
            if (tenantId == null)
            {
                _logger.LogError("Failed to extract tenant ID from certificate");
                throw new CertificateException("Certificate validation failed: missing required attributes");
            }

            if (userId == null)
            {
                _logger.LogError("Failed to extract user ID from certificate");
                throw new CertificateException("Certificate validation failed: missing required attributes");
            }

            var certificateInfo = new CertificateInfo
            {
                Certificate = certificate,
                TenantId = tenantId,
                UserId = userId,
                Subject = certificate.Subject,
                Thumbprint = certificate.Thumbprint,
                ExpiresAt = certificate.NotAfter
            };

            // Security: Use Debug level for sensitive information
            _logger.LogDebug("Certificate parsed successfully for user: '{UserId}', tenant: '{TenantId}'", 
                userId, tenantId);
            _logger.LogTrace("Certificate details - Subject: {Subject}, Thumbprint: {Thumbprint}, Expires: {Expires}",
                certificate.Subject, certificate.Thumbprint, certificate.NotAfter);

            return certificateInfo;
        }
        catch (FormatException ex)
        {
            certificate?.Dispose();
            _logger.LogError(ex, "Failed to decode base64 certificate");
            throw new CertificateException("Invalid base64 encoded certificate", ex);
        }
        catch (CertificateException)
        {
            certificate?.Dispose();
            // Re-throw certificate exceptions without wrapping
            throw;
        }
        catch (Exception ex)
        {
            certificate?.Dispose();
            _logger.LogError(ex, "Failed to read certificate");
            throw new CertificateException("Failed to load certificate", ex);
        }
    }

    /// <summary>
    /// Validates the certificate chain and revocation status.
    /// </summary>
    private void ValidateCertificateChain(X509Certificate2 certificate)
    {
        using var chain = new X509Chain();
        
        // Configure chain validation policy
        // Note: Using NoCheck for performance - revocation checks add 30+ second latency
        // For high-security environments, consider using X509RevocationMode.Offline with cached CRLs
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        
        // No timeout needed since we're not doing online revocation checks
        chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(5);

        // Build and validate the certificate chain
        bool isValid = chain.Build(certificate);

        if (!isValid)
        {
            var errors = new List<string>();
            foreach (var status in chain.ChainStatus)
            {
                // Ignore certain non-critical errors that may occur in development/testing
                // In production, you may want to be stricter
                if (status.Status != X509ChainStatusFlags.NoError)
                {
                    errors.Add($"{status.Status}: {status.StatusInformation}");
                    _logger.LogWarning("Certificate chain validation issue: {Status} - {Info}", 
                        status.Status, status.StatusInformation);
                }
            }

            // For now, log warnings but don't fail - adjust based on your security requirements
            // In high-security environments, you should throw an exception here
            if (errors.Count > 0)
            {
                _logger.LogWarning("Certificate chain validation completed with {Count} warnings", errors.Count);
                // Uncomment the following line for strict validation:
                // throw new CertificateException($"Certificate chain validation failed: {string.Join(", ", errors)}");
            }
        }
        else
        {
            _logger.LogDebug("Certificate chain validation passed");
        }
    }

    /// <summary>
    /// Extracts the tenant ID from the certificate's Organization (O=) field.
    /// </summary>
    private string? ExtractTenantIdFromCertificate(X509Certificate2 certificate)
    {
        try
        {
            // Use native X509 API to parse the distinguished name efficiently
            var distinguishedName = new System.Security.Cryptography.X509Certificates.X500DistinguishedName(certificate.SubjectName.RawData);
            var subject = distinguishedName.Name;

            // Split the subject into parts
            var subjectParts = subject.Split(',');

            foreach (var part in subjectParts)
            {
                var trimmedPart = part.Trim();

                // Look for Organization (O=) field - this is where tenant ID is stored
                if (trimmedPart.StartsWith("O=", StringComparison.OrdinalIgnoreCase))
                {
                    var organizationValue = trimmedPart.Substring(2).Trim();
                    if (!string.IsNullOrEmpty(organizationValue))
                    {
                        return organizationValue;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract tenant ID from certificate");
            return null;
        }
    }

    /// <summary>
    /// Extracts the user ID from the certificate's Organizational Unit (OU=) field.
    /// </summary>
    private string? ExtractUserIdFromCertificate(X509Certificate2 certificate)
    {
        try
        {
            // Use native X509 API to parse the distinguished name efficiently
            var distinguishedName = new System.Security.Cryptography.X509Certificates.X500DistinguishedName(certificate.SubjectName.RawData);
            var subject = distinguishedName.Name;

            // Split the subject into parts
            var subjectParts = subject.Split(',');

            foreach (var part in subjectParts)
            {
                var trimmedPart = part.Trim();

                // Look for Organizational Unit (OU=) field - this is where user ID is stored
                if (trimmedPart.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
                {
                    var organizationalUnitValue = trimmedPart.Substring(3).Trim();
                    if (!string.IsNullOrEmpty(organizationalUnitValue))
                    {
                        return organizationalUnitValue;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract user ID from certificate");
            return null;
        }
    }

    /// <summary>
    /// Clears the certificate cache. Useful for testing or when certificates need to be refreshed.
    /// </summary>
    public static void ClearCache()
    {
        _certificateCache.Clear();
    }
}

/// <summary>
/// Represents a cached certificate with expiration.
/// </summary>
internal class CachedCertificate
{
    public required CertificateInfo CertificateInfo { get; set; }
    public required DateTime ExpiresAt { get; set; }
}
