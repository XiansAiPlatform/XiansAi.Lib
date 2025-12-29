namespace Xians.Lib.Common.Caching;

/// <summary>
/// Interface for the central caching service.
/// Provides a unified caching layer for knowledge, settings, and other SDK components.
/// </summary>
public interface ICacheService : IDisposable
{
    /// <summary>
    /// Gets a cached value for knowledge.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or default if not found or caching disabled.</returns>
    T? GetKnowledge<T>(string key) where T : class;

    /// <summary>
    /// Sets a cached value for knowledge.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    void SetKnowledge<T>(string key, T value) where T : class;

    /// <summary>
    /// Removes a knowledge item from the cache.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    void RemoveKnowledge(string key);

    /// <summary>
    /// Gets a cached value for settings.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or default if not found or caching disabled.</returns>
    T? GetSettings<T>(string key) where T : class;

    /// <summary>
    /// Sets a cached value for settings.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    void SetSettings<T>(string key, T value) where T : class;

    /// <summary>
    /// Gets a cached value for workflow definitions.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or default if not found or caching disabled.</returns>
    T? GetWorkflowDefinition<T>(string key) where T : class;

    /// <summary>
    /// Sets a cached value for workflow definitions.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    void SetWorkflowDefinition<T>(string key, T value) where T : class;

    /// <summary>
    /// Clears all cached items.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>Cache statistics including count and enabled status.</returns>
    CacheStatistics GetStatistics();
}

