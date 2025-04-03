using Microsoft.Extensions.Logging;
using XiansAi.Server;

namespace XiansAi.Activity;

public class ActivityBase : DockerActivity
{
    private readonly ILogger _logger;
    private readonly ObjectCacheManager _cacheManager;

    protected ActivityBase(ObjectCacheManager cacheManager)
    {
        _logger = Globals.LogFactory?.CreateLogger<ActivityBase>()
            ?? throw new InvalidOperationException("LogFactory not initialized");
        _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
    }

    public ILogger GetLogger()
    {
        return _logger;
    }

    /// <summary>
    /// Gets a value from the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve</typeparam>
    /// <param name="key">The key to look up</param>
    /// <returns>The cached value if found, otherwise null</returns>
    protected async Task<T?> GetCacheValueAsync<T>(string key)
    {
        _logger.LogInformation("Getting value from cache for key: {Key}", key);
        return await _cacheManager.GetValueAsync<T>(key);
    }

    /// <summary>
    /// Sets a value in the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to store</typeparam>
    /// <param name="key">The key to store the value under</param>
    /// <param name="value">The value to store</param>
    /// <returns>True if the operation was successful, false otherwise</returns>
    protected async Task<bool> SetCacheValueAsync<T>(string key, T value)
    {
        _logger.LogInformation("Setting value in cache for key: {Key}", key);
        return await _cacheManager.SetValueAsync(key, value);
    }

    /// <summary>
    /// Deletes a value from the cache for the specified key.
    /// </summary>
    /// <param name="key">The key to delete</param>
    /// <returns>True if the operation was successful, false otherwise</returns>
    protected async Task<bool> DeleteCacheValueAsync(string key)
    {
        _logger.LogInformation("Deleting value from cache for key: {Key}", key);
        return await _cacheManager.DeleteValueAsync(key);
    }
}
