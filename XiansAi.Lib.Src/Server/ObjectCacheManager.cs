using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Server;

public class ObjectCacheManager
{
    private readonly ILogger _logger;

    public ObjectCacheManager()
    {
        _logger = Globals.LogFactory.CreateLogger<ObjectCacheManager>();
    }

    public async Task<T?> GetValueAsync<T>(string key)
    {
        _logger.LogInformation("Getting value from cache for key: {Key}", key);
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping cache get operation");
            return default;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var request = new CacheKeyRequest { Key = key };
            var response = await client.PostAsJsonAsync("api/agent/cache/get", request);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
            return default;
        }
    }

    public async Task<bool> SetValueAsync<T>(string key, T value, CacheOptions? options = null)
    {
        _logger.LogInformation("Setting value in cache for key: {Key}", key);
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping cache set operation");
            return false;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var request = new CacheSetRequest 
            { 
                Key = key, 
                Value = value,
                RelativeExpirationMinutes = options?.RelativeExpirationMinutes,
                SlidingExpirationMinutes = options?.SlidingExpirationMinutes
            };
            
            var response = await client.PostAsJsonAsync("api/agent/cache/set", request);
            response.EnsureSuccessStatusCode();
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in cache for key: {Key}", key);
            return false;
        }
    }

    public async Task<bool> DeleteValueAsync(string key)
    {
        _logger.LogInformation("Deleting value from cache for key: {Key}", key);
        if (!SecureApi.Instance.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, skipping cache delete operation");
            return false;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var request = new CacheKeyRequest { Key = key };
            var response = await client.PostAsJsonAsync("api/agent/cache/delete", request);
            response.EnsureSuccessStatusCode();
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting value from cache for key: {Key}", key);
            return false;
        }
    }
}

public class CacheKeyRequest
{
    public string Key { get; set; } = string.Empty;
}

public class CacheSetRequest
{
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
    public int? RelativeExpirationMinutes { get; set; }
    public int? SlidingExpirationMinutes { get; set; }
}

public class CacheOptions
{
    public int? RelativeExpirationMinutes { get; set; }
    public int? SlidingExpirationMinutes { get; set; }
} 