using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Models;

namespace Xians.Lib.Common;

/// <summary>
/// Manages caching of parsed certificates with expiration and eviction policies.
/// </summary>
internal class CertificateCache
{
    private const int MAX_CACHE_SIZE = 1000;
    private const double CACHE_EVICTION_PERCENTAGE = 0.2;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);
    
    private static readonly ConcurrentDictionary<string, CachedCertificate> _cache = new();
    private readonly ILogger? _logger;

    public CertificateCache(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Tries to get a cached certificate if it hasn't expired.
    /// </summary>
    public bool TryGet(string key, out CertificateInfo? certificateInfo)
    {
        if (_cache.TryGetValue(key, out var cachedCert))
        {
            if (DateTime.UtcNow < cachedCert.ExpiresAt)
            {
                _logger?.LogTrace("Certificate loaded from cache");
                certificateInfo = cachedCert.CertificateInfo;
                return true;
            }
            else
            {
                // Remove expired entry
                _cache.TryRemove(key, out _);
                _logger?.LogDebug("Expired certificate removed from cache");
            }
        }

        certificateInfo = null;
        return false;
    }

    /// <summary>
    /// Adds a certificate to the cache.
    /// </summary>
    public void Add(string key, CertificateInfo certificateInfo)
    {
        // Enforce cache size limit
        if (_cache.Count >= MAX_CACHE_SIZE)
        {
            EvictOldestEntries();
        }

        var cached = new CachedCertificate
        {
            CertificateInfo = certificateInfo,
            ExpiresAt = DateTime.UtcNow.Add(CacheExpiration)
        };
        
        _cache.TryAdd(key, cached);
    }

    /// <summary>
    /// Evicts the oldest 20% of cache entries when cache is full.
    /// </summary>
    private void EvictOldestEntries()
    {
        var targetRemoveCount = (int)(_cache.Count * CACHE_EVICTION_PERCENTAGE);
        var removedCount = 0;
        var now = DateTime.UtcNow;

        // Fast path: Remove expired entries first
        foreach (var kvp in _cache)
        {
            if (removedCount >= targetRemoveCount) break;
            
            if (kvp.Value.ExpiresAt < now)
            {
                if (_cache.TryRemove(kvp.Key, out _))
                {
                    removedCount++;
                }
            }
        }

        // If we still need to remove more, take the oldest by expiration
        if (removedCount < targetRemoveCount)
        {
            var remaining = targetRemoveCount - removedCount;
            var toRemove = _cache
                .OrderBy(x => x.Value.ExpiresAt)
                .Take(remaining)
                .Select(x => x.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _cache.TryRemove(key, out _);
                removedCount++;
            }
        }

        _logger?.LogDebug("Evicted {Count} entries from certificate cache", removedCount);
    }

    /// <summary>
    /// Clears the certificate cache.
    /// </summary>
    public static void Clear()
    {
        _cache.Clear();
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

