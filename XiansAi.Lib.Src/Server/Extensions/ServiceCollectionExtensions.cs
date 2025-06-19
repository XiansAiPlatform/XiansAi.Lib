using Server;
using XiansAi.Server.Interfaces;
using XiansAi;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Temporal;
using XiansAi.Server.Base;

namespace XiansAi.Server.Extensions;

/// <summary>
/// Strategy interface for different authorization methods
/// </summary>
public interface IAuthorizationStrategy
{
    /// <summary>
    /// Determines if this strategy can handle the given credential
    /// </summary>
    bool CanHandle(string credential);
    
    /// <summary>
    /// Configures the HttpClient with the appropriate authorization
    /// </summary>
    void ConfigureAuthorization(HttpClient client, string credential);
}

/// <summary>
/// Strategy for certificate-based authorization
/// </summary>
public class CertificateAuthorizationStrategy : IAuthorizationStrategy
{
    public bool CanHandle(string credential)
    {
        if (string.IsNullOrEmpty(credential))
            return false;
            
        try
        {
            // Try to parse as Base64 and create certificate
            var certificateBytes = Convert.FromBase64String(credential);
            
            #pragma warning disable SYSLIB0057
            using var certificate = new X509Certificate2(certificateBytes);
            #pragma warning restore SYSLIB0057
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ConfigureAuthorization(HttpClient client, string credential)
    {
        try
        {
            var certificateBytes = Convert.FromBase64String(credential);
            
            #pragma warning disable SYSLIB0057
            using var clientCertificate = new X509Certificate2(certificateBytes);
            #pragma warning restore SYSLIB0057

            // Export the certificate as Base64 and add it to the request headers for authentication
            var exportedCertBytes = clientCertificate.Export(X509ContentType.Cert);
            var exportedCertBase64 = Convert.ToBase64String(exportedCertBytes);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {exportedCertBase64}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to configure certificate-based authorization: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Strategy for simple bearer token authorization
/// </summary>
public class BearerTokenAuthorizationStrategy : IAuthorizationStrategy
{
    public bool CanHandle(string credential)
    {
        // This is the fallback strategy - it can handle any non-empty string
        return !string.IsNullOrEmpty(credential);
    }

    public void ConfigureAuthorization(HttpClient client, string credential)
    {
        if (string.IsNullOrEmpty(credential))
        {
            throw new InvalidOperationException("Bearer token cannot be null or empty");
        }
        
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {credential}");
    }
}

/// <summary>
/// Factory for creating authorization strategies
/// </summary>
public static class AuthorizationStrategyFactory
{
    private static readonly List<IAuthorizationStrategy> _strategies = new()
    {
        new CertificateAuthorizationStrategy(),
        new BearerTokenAuthorizationStrategy() // This should be last as it's the fallback
    };

    /// <summary>
    /// Gets the appropriate authorization strategy for the given credential
    /// </summary>
    public static IAuthorizationStrategy GetStrategy(string credential)
    {
        if (string.IsNullOrEmpty(credential))
        {
            throw new InvalidOperationException("Credential is required for authorization");
        }

        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(credential));
        
        if (strategy == null)
        {
            throw new InvalidOperationException("No suitable authorization strategy found for the provided credential");
        }

        return strategy;
    }
}

/// <summary>
/// Extension methods for configuring XiansAi services in the DI container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds XiansAi services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="serverUrl">Optional server URL (will use environment variable if not provided)</param>
    /// <param name="apiKey">Optional API key (will use environment variable if not provided)</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddXiansAiServices(
        this IServiceCollection services, 
        string? serverUrl = null, 
        string? apiKey = null)
    {
        // Use provided values or fall back to environment variables
        var effectiveServerUrl = serverUrl ?? PlatformConfig.APP_SERVER_URL;
        var effectiveApiKey = apiKey ?? PlatformConfig.APP_SERVER_API_KEY;
        
        // Validate required configuration
        if (string.IsNullOrEmpty(effectiveServerUrl))
        {
            throw new InvalidOperationException(
                "Server URL is required. Provide it via parameter or APP_SERVER_URL environment variable.");
        }
        
        if (string.IsNullOrEmpty(effectiveApiKey))
        {
            throw new InvalidOperationException(
                "API Key is required. Provide it via parameter or APP_SERVER_API_KEY environment variable.");
        }
        
        // Register HttpClient with authorization
        services.AddHttpClient<ISettingsService, SettingsService>(client =>
        {
            client.BaseAddress = new Uri(effectiveServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            
            // Configure authorization
            ConfigureAuthorization(client, effectiveApiKey);
        });
        
        // Register HttpClient for FlowDefinitionUploader
        services.AddHttpClient<IFlowDefinitionUploader, FlowDefinitionUploader>(client =>
        {
            client.BaseAddress = new Uri(effectiveServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            
            // Configure authorization
            ConfigureAuthorization(client, effectiveApiKey);
        });
        
        // Register IApiService with BaseApiService implementation
        services.AddHttpClient<IApiService, BaseApiService>(client =>
        {
            client.BaseAddress = new Uri(effectiveServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            
            // Configure authorization
            ConfigureAuthorization(client, effectiveApiKey);
        });
        
        // Register SystemActivities that uses IApiService
        services.AddScoped<SystemActivities>();
        
        // Register memory cache if not already registered
        services.TryAddSingleton<IMemoryCache, MemoryCache>();
        
        // Register the settings service as singleton
        services.AddSingleton<ISettingsService, SettingsService>();
        
        // Register the flow definition uploader as singleton
        services.AddSingleton<IFlowDefinitionUploader, FlowDefinitionUploader>();
        
        // Register Temporal client service
        services.AddSingleton<TemporalClientService>();

        return services;
    }
    
    /// <summary>
    /// Adds XiansAi services with custom HttpClient configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureClient">Action to configure the HttpClient</param>
    /// <param name="serverUrl">Optional server URL</param>
    /// <param name="apiKey">Optional API key</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddXiansAiServices(
        this IServiceCollection services,
        Action<HttpClient> configureClient,
        string? serverUrl = null,
        string? apiKey = null)
    {
        // Use provided values or fall back to environment variables
        var effectiveServerUrl = serverUrl ?? PlatformConfig.APP_SERVER_URL;
        var effectiveApiKey = apiKey ?? PlatformConfig.APP_SERVER_API_KEY;
        
        // Validate required configuration
        if (string.IsNullOrEmpty(effectiveServerUrl))
        {
            throw new InvalidOperationException(
                "Server URL is required. Provide it via parameter or APP_SERVER_URL environment variable.");
        }
        
        if (string.IsNullOrEmpty(effectiveApiKey))
        {
            throw new InvalidOperationException(
                "API Key is required. Provide it via parameter or APP_SERVER_API_KEY environment variable.");
        }
        
        // Register HttpClient with custom configuration and authorization
        services.AddHttpClient<ISettingsService, SettingsService>(client =>
        {
            client.BaseAddress = new Uri(effectiveServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            
            // Apply custom configuration first
            configureClient(client);
            
            // Configure authorization (this should come after custom config to ensure it's not overridden)
            ConfigureAuthorization(client, effectiveApiKey);
        });
        
        // Register HttpClient for FlowDefinitionUploader
        services.AddHttpClient<IFlowDefinitionUploader, FlowDefinitionUploader>(client =>
        {
            client.BaseAddress = new Uri(effectiveServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            
            // Configure authorization
            ConfigureAuthorization(client, effectiveApiKey);
        });
        
        // Register IApiService with BaseApiService implementation
        services.AddHttpClient<IApiService, BaseApiService>(client =>
        {
            client.BaseAddress = new Uri(effectiveServerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            
            // Configure authorization
            ConfigureAuthorization(client, effectiveApiKey);
        });
        
        // Register SystemActivities that uses IApiService
        services.AddScoped<SystemActivities>();
        
        // Register memory cache if not already registered
        services.TryAddSingleton<IMemoryCache, MemoryCache>();
        
        // Remove the conflicting singleton registration - AddHttpClient already registers the service
        // but we need to make it singleton instead of transient
        services.AddSingleton<ISettingsService>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(typeof(SettingsService).Name);
            var cache = serviceProvider.GetRequiredService<IMemoryCache>();
            var logger = serviceProvider.GetRequiredService<ILogger<SettingsService>>();
            return new SettingsService(httpClient, cache, logger);
        });
        
        // Register the flow definition uploader as singleton
        services.AddSingleton<IFlowDefinitionUploader, FlowDefinitionUploader>();
        
        // Register Temporal client service
        services.AddSingleton<TemporalClientService>();

        return services;
    }
    
    /// <summary>
    /// Configures authorization for the HttpClient using the appropriate strategy
    /// </summary>
    /// <param name="client">The HttpClient to configure</param>
    /// <param name="credential">The credential (certificate or API key)</param>
    internal static void ConfigureAuthorization(HttpClient client, string credential)
    {
        var strategy = AuthorizationStrategyFactory.GetStrategy(credential);
        strategy.ConfigureAuthorization(client, credential);
    }
}

/// <summary>
/// Extension methods to add TryAdd functionality for services
/// </summary>
internal static class ServiceCollectionTryAddExtensions
{
    public static IServiceCollection TryAddSingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (!services.Any(x => x.ServiceType == typeof(TService)))
        {
            services.AddSingleton<TService, TImplementation>();
        }
        return services;
    }
    
    public static IServiceCollection TryAddSingleton<TService>(this IServiceCollection services)
        where TService : class
    {
        if (!services.Any(x => x.ServiceType == typeof(TService)))
        {
            services.AddSingleton<TService>();
        }
        return services;
    }
}

/// <summary>
/// Legacy factory for backward compatibility - will be deprecated
/// </summary>
[Obsolete("Use dependency injection with AddXiansAiServices() instead. This factory will be removed in a future version.")]
public static class XiansAiServiceFactory
{
    private static IServiceProvider? _serviceProvider;
    private static readonly object _lock = new object();
    
    /// <summary>
    /// Gets the settings service instance (legacy method)
    /// </summary>
    /// <returns>The settings service instance</returns>
    public static ISettingsService GetSettingsService()
    {
        if (_serviceProvider == null)
        {
            lock (_lock)
            {
                if (_serviceProvider == null)
                {
                    // Get configuration from environment variables
                    var serverUrl = PlatformConfig.APP_SERVER_URL;
                    var apiKey = PlatformConfig.APP_SERVER_API_KEY;
                    
                    if (string.IsNullOrEmpty(serverUrl))
                    {
                        throw new InvalidOperationException(
                            "Server URL is required. Set the APP_SERVER_URL environment variable.");
                    }
                    
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        throw new InvalidOperationException(
                            "API Key is required. Set the APP_SERVER_API_KEY environment variable.");
                    }
                    
                    // Create a service collection and configure services
                    var services = new ServiceCollection();
                    services.AddLogging(builder => builder.AddConsole());
                    
                    // Configure HttpClient manually for the obsolete factory
                    services.AddHttpClient<SettingsService>(client =>
                    {
                        client.BaseAddress = new Uri(serverUrl);
                        client.Timeout = TimeSpan.FromSeconds(30);
                        
                        // Configure authorization
                        ServiceCollectionExtensions.ConfigureAuthorization(client, apiKey);
                    });
                    
                    // Register HttpClient for FlowDefinitionUploader
                    services.AddHttpClient<FlowDefinitionUploader>(client =>
                    {
                        client.BaseAddress = new Uri(serverUrl);
                        client.Timeout = TimeSpan.FromSeconds(30);
                        
                        // Configure authorization
                        ServiceCollectionExtensions.ConfigureAuthorization(client, apiKey);
                    });
                    
                    // Register memory cache
                    services.AddSingleton<IMemoryCache, MemoryCache>();
                    
                    // Register SettingsService as singleton using the configured HttpClient
                    services.AddSingleton<ISettingsService>(serviceProvider =>
                    {
                        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                        var httpClient = httpClientFactory.CreateClient(typeof(SettingsService).Name);
                        var cache = serviceProvider.GetRequiredService<IMemoryCache>();
                        var logger = serviceProvider.GetRequiredService<ILogger<SettingsService>>();
                        return new SettingsService(httpClient, cache, logger);
                    });
                    
                    // Register FlowDefinitionUploader as singleton using the configured HttpClient
                    services.AddSingleton<IFlowDefinitionUploader>(serviceProvider =>
                    {
                        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                        var httpClient = httpClientFactory.CreateClient(typeof(FlowDefinitionUploader).Name);
                        var logger = serviceProvider.GetRequiredService<ILogger<FlowDefinitionUploader>>();
                        return new FlowDefinitionUploader(httpClient, logger);
                    });
                    
                    _serviceProvider = services.BuildServiceProvider();
                }
            }
        }
        
        return _serviceProvider.GetRequiredService<ISettingsService>();
    }
    
    /// <summary>
    /// Gets the flow definition uploader service instance (legacy method)
    /// </summary>
    /// <returns>The flow definition uploader service instance</returns>
    public static IFlowDefinitionUploader GetFlowDefinitionUploader()
    {
        // Ensure the service provider is initialized (reuse the same initialization as GetSettingsService)
        GetSettingsService();
        
        return _serviceProvider!.GetRequiredService<IFlowDefinitionUploader>();
    }
    
    /// <summary>
    /// Resets the service provider (primarily for testing)
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _serviceProvider = null;
        }
    }
} 