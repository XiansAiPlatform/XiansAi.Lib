using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Common.Caching;

/// <summary>
/// Central caching service for Xians.Lib.
/// Provides a unified caching layer for knowledge, settings, and other SDK components.
/// </summary>
public class CacheService : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<CacheService> _logger;

    /// <summary>
    /// Initializes a new instance of the CacheService.
    /// </summary>
    /// <param name="options">Cache configuration options.</param>
    /// <param name="logger">Optional logger instance.</param>
    public CacheService(CacheOptions? options = null, ILogger<CacheService>? logger = null)
    {
        _options = options ?? new CacheOptions();
        _options.Validate();
        
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = logger ?? Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<CacheService>();

        _logger.LogInformation(
            "Cache service initialized: Enabled={Enabled}, DefaultTTL={DefaultTtl}min, Knowledge={KnowledgeTtl}min",
            _options.Enabled,
            _options.DefaultTtlMinutes,
            _options.Knowledge.TtlMinutes);
    }

    /// <summary>
    /// Gets a cached value for knowledge.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or default if not found or caching disabled.</returns>
    public T? GetKnowledge<T>(string key) where T : class
    {
        return Get<T>(key, _options.Knowledge);
    }

    /// <summary>
    /// Sets a cached value for knowledge.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    public void SetKnowledge<T>(string key, T value) where T : class
    {
        Set(key, value, _options.Knowledge);
    }

    /// <summary>
    /// Removes a knowledge item from the cache.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    public void RemoveKnowledge(string key)
    {
        Remove(key);
    }

    /// <summary>
    /// Gets a cached value for settings.
    /// </summary>
    public T? GetSettings<T>(string key) where T : class
    {
        return Get<T>(key, _options.Settings);
    }

    /// <summary>
    /// Sets a cached value for settings.
    /// </summary>
    public void SetSettings<T>(string key, T value) where T : class
    {
        Set(key, value, _options.Settings);
    }

    /// <summary>
    /// Gets a cached value for workflow definitions.
    /// </summary>
    public T? GetWorkflowDefinition<T>(string key) where T : class
    {
        return Get<T>(key, _options.WorkflowDefinitions);
    }

    /// <summary>
    /// Sets a cached value for workflow definitions.
    /// </summary>
    public void SetWorkflowDefinition<T>(string key, T value) where T : class
    {
        Set(key, value, _options.WorkflowDefinitions);
    }

    /// <summary>
    /// Gets a value from the cache with aspect-specific configuration.
    /// </summary>
    private T? Get<T>(string key, CacheAspectOptions aspectOptions) where T : class
    {
        // Check if caching is globally disabled or disabled for this aspect
        if (!_options.Enabled || !aspectOptions.Enabled)
        {
            _logger.LogTrace("Cache disabled for key: {Key}", key);
            return null;
        }

        if (_cache.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("Cache hit: {Key}", key);
            return value;
        }

        _logger.LogDebug("Cache miss: {Key}", key);
        return null;
    }

    /// <summary>
    /// Sets a value in the cache with aspect-specific configuration.
    /// </summary>
    private void Set<T>(string key, T value, CacheAspectOptions aspectOptions) where T : class
    {
        // Check if caching is globally disabled or disabled for this aspect
        if (!_options.Enabled || !aspectOptions.Enabled)
        {
            _logger.LogTrace("Cache disabled, skipping set for key: {Key}", key);
            return;
        }

        if (value == null)
        {
            _logger.LogWarning("Attempted to cache null value for key: {Key}", key);
            return;
        }

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(aspectOptions.TtlMinutes)
        };

        _cache.Set(key, value, cacheOptions);
        
        _logger.LogDebug(
            "Cached value: Key={Key}, TTL={Ttl}min", 
            key, 
            aspectOptions.TtlMinutes);
    }

    /// <summary>
    /// Removes an item from the cache.
    /// </summary>
    private void Remove(string key)
    {
        _cache.Remove(key);
        _logger.LogDebug("Cache invalidated: {Key}", key);
    }

    /// <summary>
    /// Clears all cached items.
    /// </summary>
    public void Clear()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0); // Remove 100% of entries
            _logger.LogInformation("Cache cleared");
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        if (_cache is MemoryCache memoryCache)
        {
            return new CacheStatistics
            {
                Count = memoryCache.Count,
                IsEnabled = _options.Enabled
            };
        }

        return new CacheStatistics
        {
            Count = 0,
            IsEnabled = _options.Enabled
        };
    }

    public void Dispose()
    {
        _cache.Dispose();
        _logger.LogDebug("Cache service disposed");
    }
}

/// <summary>
/// Cache statistics.
/// </summary>
public class CacheStatistics
{
    public int Count { get; set; }
    public bool IsEnabled { get; set; }
}

