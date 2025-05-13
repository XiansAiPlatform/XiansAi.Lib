using Microsoft.Extensions.Logging;
using XiansAi.Memory;

namespace XiansAi.Activity;

public class ActivityBase : DockerActivity
{
    private readonly ILogger _logger;
    private readonly IMemoryHub _memoryHub;

    protected string? AgentName;
    
    protected ActivityBase()
    {
        _logger = Globals.LogFactory?.CreateLogger<ActivityBase>()
            ?? throw new InvalidOperationException("LogFactory not initialized");
        _memoryHub = new MemoryHub();
    }

    protected virtual string GetWorkflowPrefixedKey(string key, bool usePrefix = true)
    {
        if (!usePrefix)
        {
            return key;
        }

        var activityInfo = this.CreateActivity();
        var workflowId = activityInfo.WorkflowId;
        if (string.IsNullOrEmpty(workflowId))
        {
            throw new InvalidOperationException("WorkflowId is not available in the current context.");
        }
        return $"{workflowId}:{key}";
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
    /// <param name="usePrefix">Whether to prefix the key with workflow ID (default: true)</param>
    /// <returns>The cached value if found, otherwise null</returns>
    protected async Task<T?> GetCacheValueAsync<T>(string key, bool usePrefix = true)
    {
        var prefixedKey = GetWorkflowPrefixedKey(key, usePrefix);
        _logger.LogDebug("Getting value from cache for key: {Key}", prefixedKey);
        return await _memoryHub.Cache.GetValueAsync<T>(prefixedKey);
    }

    /// <summary>
    /// Sets a value in the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to store</typeparam>
    /// <param name="key">The key to store the value under</param>
    /// <param name="value">The value to store</param>
    /// <param name="usePrefix">Whether to prefix the key with workflow ID (default: true)</param>
    /// <returns>True if the operation was successful, false otherwise</returns>
    protected async Task<bool> SetCacheValueAsync<T>(string key, T value, bool usePrefix = true)
    {
        var prefixedKey = GetWorkflowPrefixedKey(key, usePrefix);
        _logger.LogDebug("Setting value in cache for key: {Key}", prefixedKey);
        return await _memoryHub.Cache.SetValueAsync(prefixedKey, value);
    }

    /// <summary>
    /// Deletes a value from the cache for the specified key.
    /// </summary>
    /// <param name="key">The key to delete</param>
    /// <param name="usePrefix">Whether to prefix the key with workflow ID (default: true)</param>
    /// <returns>True if the operation was successful, false otherwise</returns>
    protected async Task<bool> DeleteCacheValueAsync(string key, bool usePrefix = true)
    {
        var prefixedKey = GetWorkflowPrefixedKey(key, usePrefix);
        _logger.LogInformation("Deleting value from cache for key: {Key}", prefixedKey);
        return await _memoryHub.Cache.DeleteValueAsync(prefixedKey);
    }
}