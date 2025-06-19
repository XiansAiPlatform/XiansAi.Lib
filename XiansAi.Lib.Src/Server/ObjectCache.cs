using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using XiansAi.Server.Base;

namespace Server;

public interface IObjectCache
{
    Task<T?> GetValueAsync<T>(string key);
    Task<bool> SetValueAsync<T>(string key, T value, CacheOptions? options = null);
    Task<bool> DeleteValueAsync(string key);
}

public class ObjectCache : IObjectCache
{
    private readonly IApiService _apiService;
    private readonly ILogger<ObjectCache> _logger;

    /// <summary>
    /// Constructor for dependency injection with IApiService
    /// </summary>
    public ObjectCache(IApiService apiService, ILogger<ObjectCache> logger)
    {
        _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Legacy constructor for backward compatibility - creates instance without DI
    /// </summary>
    public ObjectCache()
    {
        // Create a BaseApiService instance for legacy support
        var httpClient = GetLegacyHttpClient();
        _apiService = new LegacyApiServiceWrapper(httpClient, Globals.LogFactory.CreateLogger<LegacyApiServiceWrapper>());
        _logger = Globals.LogFactory.CreateLogger<ObjectCache>();
    }

    private static HttpClient GetLegacyHttpClient()
    {
        if (!SecureApi.IsReady)
        {
            throw new InvalidOperationException("SecureApi is not ready. Initialize SecureApi before using ObjectCache or use dependency injection.");
        }
        return SecureApi.Instance.Client;
    }

    public async virtual Task<T?> GetValueAsync<T>(string key)
    {
        _logger.LogInformation("Getting value from cache for key: {Key}", key);
        
        try
        {
            var request = new CacheKeyRequest { Key = key };
            return await _apiService.PostAsync<T>("api/agent/cache/get", request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
            return default;
        }
    }

    public async virtual Task<bool> SetValueAsync<T>(string key, T value, CacheOptions? options = null)
    {
        _logger.LogInformation("Setting value in cache for key: {Key}", key);
        
        try
        {
            var request = new CacheSetRequest 
            { 
                Key = key, 
                Value = value,
                RelativeExpirationMinutes = options?.RelativeExpirationMinutes,
                SlidingExpirationMinutes = options?.SlidingExpirationMinutes
            };
            
            await _apiService.PostAsync("api/agent/cache/set", request);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value in cache for key: {Key}", key);
            return false;
        }
    }

    public async virtual Task<bool> DeleteValueAsync(string key)
    {
        _logger.LogInformation("Deleting value from cache for key: {Key}", key);
        
        try
        {
            var request = new CacheKeyRequest { Key = key };
            await _apiService.PostAsync("api/agent/cache/delete", request);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting value from cache for key: {Key}", key);
            return false;
        }
    }

    /// <summary>
    /// Legacy wrapper for BaseApiService to support the parameterless constructor
    /// </summary>
    private class LegacyApiServiceWrapper : BaseApiService
    {
        public LegacyApiServiceWrapper(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
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