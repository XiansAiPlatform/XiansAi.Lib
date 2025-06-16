using Server;
using XiansAi.Server.Interfaces;
using XiansAi;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace XiansAi.Server.Extensions;

/// <summary>
/// Factory for creating XiansAi services with proper configuration
/// </summary>
public static class XiansAiServiceFactory
{
    private static ISettingsService? _settingsServiceInstance;
    private static readonly object _lock = new object();
    
    /// <summary>
    /// Creates or gets the singleton settings service instance
    /// </summary>
    /// <returns>The settings service instance</returns>
    public static ISettingsService GetSettingsService()
    {
        if (_settingsServiceInstance != null)
        {
            return _settingsServiceInstance;
        }
        
        lock (_lock)
        {
            if (_settingsServiceInstance != null)
            {
                return _settingsServiceInstance;
            }
            
            // Validate that the required environment variables are set
            if (string.IsNullOrEmpty(PlatformConfig.APP_SERVER_URL))
            {
                throw new InvalidOperationException("APP_SERVER_URL environment variable is required");
            }
            
            if (string.IsNullOrEmpty(PlatformConfig.APP_SERVER_API_KEY))
            {
                throw new InvalidOperationException("APP_SERVER_API_KEY environment variable is required");
            }
            
            // Create HttpClient with proper authorization
            var httpClient = CreateAuthorizedHttpClient(
                PlatformConfig.APP_SERVER_URL,
                PlatformConfig.APP_SERVER_API_KEY
            );
            
            // Create a simple memory cache implementation
            var cache = new SimpleMemoryCache();
            
            // Create a logger using the existing logging infrastructure
            var logger = Globals.LogFactory.CreateLogger("SettingsService");
            
            _settingsServiceInstance = new SettingsService(httpClient, cache, new LoggerAdapter(logger));
            return _settingsServiceInstance;
        }
    }
    
    /// <summary>
    /// Creates an HttpClient with proper certificate-based authorization
    /// </summary>
    /// <param name="serverUrl">The server URL</param>
    /// <param name="certificateBase64">The Base64 encoded certificate for authorization</param>
    /// <returns>Configured HttpClient with authorization headers</returns>
    private static HttpClient CreateAuthorizedHttpClient(string serverUrl, string certificateBase64)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(serverUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        try
        {
            // Convert the Base64 string to a certificate and export it for authorization
            var certificateBytes = Convert.FromBase64String(certificateBase64);
            
            // Suppress warning about X509Certificate2 constructor being obsolete in .NET Core 
            #pragma warning disable SYSLIB0057
            using var clientCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificateBytes);
            #pragma warning restore SYSLIB0057

            // Export the certificate as Base64 and add it to the request headers for authentication
            var exportedCertBytes = clientCertificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Cert);
            var exportedCertBase64 = Convert.ToBase64String(exportedCertBytes);
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {exportedCertBase64}");
            
            return httpClient;
        }
        catch (Exception ex)
        {
            httpClient.Dispose();
            throw new InvalidOperationException($"Failed to configure authorization for HttpClient: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Resets the service instances (primarily for testing)
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            if (_settingsServiceInstance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _settingsServiceInstance = null;
        }
    }
}

/// <summary>
/// Simple memory cache implementation for basic caching needs
/// </summary>
internal class SimpleMemoryCache : Microsoft.Extensions.Caching.Memory.IMemoryCache
{
    private readonly ConcurrentDictionary<object, CacheEntry> _cache = new();
    
    public Microsoft.Extensions.Caching.Memory.ICacheEntry CreateEntry(object key)
    {
        return new SimpleCacheEntry(key, this);
    }
    
    public void Dispose()
    {
        _cache.Clear();
    }
    
    public void Remove(object key)
    {
        _cache.TryRemove(key, out _);
    }
    
    public bool TryGetValue(object key, out object? value)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired)
            {
                _cache.TryRemove(key, out _);
                value = null;
                return false;
            }
            
            value = entry.Value;
            return true;
        }
        
        value = null;
        return false;
    }
    
    internal void Set(object key, object? value, DateTimeOffset? expiration)
    {
        _cache[key] = new CacheEntry(value, expiration);
    }
    
    private class CacheEntry
    {
        public object? Value { get; }
        public DateTimeOffset? Expiration { get; }
        
        public CacheEntry(object? value, DateTimeOffset? expiration)
        {
            Value = value;
            Expiration = expiration;
        }
        
        public bool IsExpired => Expiration.HasValue && DateTimeOffset.UtcNow > Expiration.Value;
    }
}

/// <summary>
/// Simple cache entry implementation
/// </summary>
internal class SimpleCacheEntry : Microsoft.Extensions.Caching.Memory.ICacheEntry
{
    private readonly object _key;
    private readonly SimpleMemoryCache _cache;
    
    public SimpleCacheEntry(object key, SimpleMemoryCache cache)
    {
        _key = key;
        _cache = cache;
        Key = key;
    }
    
    public object Key { get; }
    public object? Value { get; set; }
    public DateTimeOffset? AbsoluteExpiration { get; set; }
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
    public TimeSpan? SlidingExpiration { get; set; }
    public IList<Microsoft.Extensions.Primitives.IChangeToken> ExpirationTokens { get; } = new List<Microsoft.Extensions.Primitives.IChangeToken>();
    public IList<Microsoft.Extensions.Caching.Memory.PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = new List<Microsoft.Extensions.Caching.Memory.PostEvictionCallbackRegistration>();
    public Microsoft.Extensions.Caching.Memory.CacheItemPriority Priority { get; set; }
    public long? Size { get; set; }
    
    public void Dispose()
    {
        var expiration = AbsoluteExpiration;
        if (AbsoluteExpirationRelativeToNow.HasValue)
        {
            expiration = DateTimeOffset.UtcNow.Add(AbsoluteExpirationRelativeToNow.Value);
        }
        
        _cache.Set(_key, Value, expiration);
    }
}

/// <summary>
/// Adapter to convert ILogger to ILogger<T>
/// </summary>
internal class LoggerAdapter : Microsoft.Extensions.Logging.ILogger<SettingsService>
{
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    
    public LoggerAdapter(Microsoft.Extensions.Logging.ILogger logger)
    {
        _logger = logger;
    }
    
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope(state);
    }
    
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return _logger.IsEnabled(logLevel);
    }
    
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log(logLevel, eventId, state, exception, formatter);
    }
} 