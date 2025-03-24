using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using XiansAi.Http;

namespace XiansAi.Server;

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
        if (!SecureApi.IsReady())
        {
            _logger.LogWarning("App server secure API is not ready, skipping cache get operation");
            return default;
        }

        try
        {
            HttpClient client = SecureApi.GetClient();
            var response = await client.GetAsync($"api/client/cache/{key}");
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
            return default;
        }
    }

    public async Task<bool> SetValueAsync<T>(string key, T value)
    {
        _logger.LogInformation("Setting value in cache for key: {Key}", key);
        if (!SecureApi.IsReady())
        {
            _logger.LogWarning("App server secure API is not ready, skipping cache set operation");
            return false;
        }

        try
        {
            HttpClient client = SecureApi.GetClient();
            var response = await client.PostAsync($"api/client/cache/{key}", JsonContent.Create(value));
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
        if (!SecureApi.IsReady())
        {
            _logger.LogWarning("App server secure API is not ready, skipping cache delete operation");
            return false;
        }

        try
        {
            HttpClient client = SecureApi.GetClient();
            var response = await client.DeleteAsync($"api/client/cache/{key}");
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