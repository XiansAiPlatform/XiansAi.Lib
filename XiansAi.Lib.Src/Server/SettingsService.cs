using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using XiansAi.Server.Base;
using XiansAi.Server.Interfaces;

namespace Server;

public class FlowServerSettings
{
    public required string FlowServerUrl { get; set; }
    public required string FlowServerNamespace { get; set; }
    public required string FlowServerCertBase64 { get; set; }
    public required string FlowServerPrivateKeyBase64 { get; set; }
    public required string OpenAIApiKey { get; set; }
    public required string ModelName { get; set; }
}

/// <summary>
/// Settings service with caching and dependency injection support
/// </summary>
public class SettingsService : BaseApiService, ISettingsService
{
    private const string SETTINGS_ENDPOINT = "api/agent/settings/flowserver";
    private readonly IMemoryCache _cache;
    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _semaphore;
    
    // Cache configuration
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);
    private const string CACHE_KEY = "FlowServerSettings";
    
    public SettingsService(
        HttpClient httpClient, 
        IMemoryCache cache,
        ILogger<SettingsService> logger) 
        : base(httpClient, logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger;
        _semaphore = new SemaphoreSlim(1, 1); // Prevent concurrent API calls
    }
    
    public async Task<FlowServerSettings> GetFlowServerSettingsAsync()
    {
        // Try to get from cache first
        if (_cache.TryGetValue(CACHE_KEY, out FlowServerSettings? cachedSettings) && cachedSettings != null)
        {
            _logger.LogDebug("Returning cached settings");
            return cachedSettings;
        }
        
        // Use semaphore to prevent multiple concurrent API calls
        await _semaphore.WaitAsync();
        try
        {
            // Double-check pattern - another thread might have loaded it
            if (_cache.TryGetValue(CACHE_KEY, out cachedSettings) && cachedSettings != null)
            {
                _logger.LogDebug("Returning cached settings (double-check)");
                return cachedSettings;
            }
            
            _logger.LogInformation("Loading settings from server");
            var settings = await GetAsync<FlowServerSettings>(SETTINGS_ENDPOINT);
            
            // Cache the settings
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration,
                Priority = CacheItemPriority.High,
                Size = 1
            };
            
            _cache.Set(CACHE_KEY, settings, cacheOptions);
            _logger.LogInformation("Settings loaded and cached successfully. Server: {ServerUrl}", settings.FlowServerUrl);
            
            return settings;
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    public async Task RefreshSettingsAsync()
    {
        _logger.LogInformation("Refreshing settings cache");
        _cache.Remove(CACHE_KEY);
        await GetFlowServerSettingsAsync(); // This will reload from server
    }
    
    public FlowServerSettings? GetCachedSettings()
    {
        _cache.TryGetValue(CACHE_KEY, out FlowServerSettings? settings);
        return settings;
    }
}

/// <summary>
/// Legacy static class for backward compatibility
/// This will be deprecated in favor of the new ISettingsService
/// </summary>
[Obsolete("Use ISettingsService instead. This class will be removed in a future version.")]
public static class LegacySettingsService
{
    private static readonly ILogger _logger = Globals.LogFactory.CreateLogger<FlowServerSettings>();
    private const string SETTINGS_URL = "api/agent/settings/flowserver";
    private static FlowServerSettings? _settings;

    /// <summary>
    /// Loads settings from the server. Caches the settings after the first fetch.
    /// </summary>
    /// <returns>The flow server settings</returns>
    public static async Task<FlowServerSettings> GetSettingsFromServer()
    {
        return await _settingsLazy.Value;
    }

    /// <summary>
    /// Internal method that actually loads settings from the server
    /// </summary>
    private static async Task<FlowServerSettings> LoadSettingsFromServer()
    {
        if (!SecureApi.IsReady)
        {
            _logger.LogWarning("App server secure API is not ready, cannot load settings from server");
            throw new Exception("App server secure API is not ready, cannot load settings from server");
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var httpResult = await client.GetAsync(SETTINGS_URL);

            if (httpResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"Failed to get settings from server. Status code: {httpResult.StatusCode}");
            }

            return await ParseSettingsResponse(httpResult);
        }
        catch (Exception e)
        {
            _logger.LogError($"Failed to load settings from server {e.Message}");
            throw new Exception($"Failed to load settings from server {e.Message}", e);
        }
    }

    /// <summary>
    /// Parses the server response into a FlowServerSettings object
    /// </summary>
    private static async Task<FlowServerSettings> ParseSettingsResponse(HttpResponseMessage httpResult)
    {
        var response = await httpResult.Content.ReadAsStringAsync();

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            
            var settings = System.Text.Json.JsonSerializer.Deserialize<FlowServerSettings>(response, options);

            if (settings == null)
            {
                _logger.LogError($"Failed to deserialize settings from server: {response}");
                throw new Exception($"Failed to deserialize settings from server: {response}");
            }

            _logger.LogInformation($"Settings loaded from server: {settings.FlowServerUrl}");
            
            return settings;
        } 
        catch (Exception e)
        {
            _logger.LogError($"Failed to deserialize settings from server: {response}", e);
            throw new Exception($"Failed to deserialize settings from server: {response}", e);
        }
    }
}
