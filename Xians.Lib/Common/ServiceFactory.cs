using Microsoft.Extensions.Logging;
using Xians.Lib.Configuration;
using Xians.Lib.Http;
using Xians.Lib.Temporal;

namespace Xians.Lib.Common;

/// <summary>
/// Factory for creating HTTP and Temporal client services.
/// </summary>
public static class ServiceFactory
{
    /// <summary>
    /// Creates HTTP and Temporal services using only SERVER_URL and API_KEY.
    /// Temporal configuration is automatically fetched from the server.
    /// This is the recommended approach - matches XiansAi.Lib.Src pattern.
    /// </summary>
    /// <param name="serverUrl">The application server URL.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="httpLogger">Optional logger for HTTP service.</param>
    /// <param name="temporalLogger">Optional logger for Temporal service.</param>
    /// <returns>A tuple containing both HTTP and Temporal services.</returns>
    public static async Task<(IHttpClientService HttpService, ITemporalClientService TemporalService)> 
        CreateServicesFromServerAsync(
            string serverUrl, 
            string apiKey,
            ILogger<HttpClientService>? httpLogger = null,
            ILogger<TemporalClientService>? temporalLogger = null)
    {
        // Create HTTP service with provided credentials
        var httpConfig = new ServerConfiguration
        {
            ServerUrl = serverUrl,
            ApiKey = apiKey
        };
        
        var httpService = CreateHttpClientService(httpConfig, httpLogger);
        
        // Fetch Temporal settings from server endpoint: /api/agent/settings/flowserver
        var serverSettings = await SettingsService.GetSettingsAsync(httpService);
        var temporalConfig = serverSettings.ToTemporalConfiguration();
        
        // Create Temporal service with fetched settings
        var temporalService = CreateTemporalClientService(temporalConfig, temporalLogger);
        
        return (httpService, temporalService);
    }

    /// <summary>
    /// Creates services using environment variables (SERVER_URL and API_KEY).
    /// Fetches Temporal configuration from the server.
    /// This is the recommended approach - matches XiansAi.Lib.Src pattern.
    /// </summary>
    public static async Task<(IHttpClientService HttpService, ITemporalClientService TemporalService)>
        CreateServicesFromEnvironmentAsync(
            ILogger<HttpClientService>? httpLogger = null,
            ILogger<TemporalClientService>? temporalLogger = null)
    {
        var serverUrl = Environment.GetEnvironmentVariable("SERVER_URL");
        var apiKey = Environment.GetEnvironmentVariable("API_KEY");

        if (string.IsNullOrEmpty(serverUrl))
            throw new InvalidOperationException("SERVER_URL environment variable is required");
        
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("API_KEY environment variable is required");

        return await CreateServicesFromServerAsync(serverUrl, apiKey, httpLogger, temporalLogger);
    }

    /// <summary>
    /// Creates an HTTP client service with the specified configuration.
    /// </summary>
    /// <param name="config">The server configuration.</param>
    /// <param name="logger">Optional custom logger. If not provided, uses the default logger factory.</param>
    /// <returns>A configured HTTP client service.</returns>
    public static IHttpClientService CreateHttpClientService(
        ServerConfiguration config, 
        ILogger<HttpClientService>? logger = null)
    {
        logger ??= LoggerFactory.CreateLogger<HttpClientService>();
        return new HttpClientService(config, logger);
    }

    /// <summary>
    /// Creates an HTTP client service from environment variables.
    /// Expected variables: SERVER_URL, API_KEY.
    /// </summary>
    /// <param name="logger">Optional custom logger.</param>
    /// <returns>A configured HTTP client service.</returns>
    /// <exception cref="InvalidOperationException">Thrown if required environment variables are missing.</exception>
    public static IHttpClientService CreateHttpClientServiceFromEnvironment(
        ILogger<HttpClientService>? logger = null)
    {
        var serverUrl = Environment.GetEnvironmentVariable("SERVER_URL");
        var apiKey = Environment.GetEnvironmentVariable("API_KEY");

        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new InvalidOperationException("SERVER_URL environment variable is required");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("API_KEY environment variable is required");

        var config = new ServerConfiguration
        {
            ServerUrl = serverUrl,
            ApiKey = apiKey
        };

        return CreateHttpClientService(config, logger);
    }

    /// <summary>
    /// Creates a Temporal client service with the specified configuration.
    /// </summary>
    /// <param name="config">The Temporal configuration.</param>
    /// <param name="logger">Optional custom logger. If not provided, uses the default logger factory.</param>
    /// <returns>A configured Temporal client service.</returns>
    public static ITemporalClientService CreateTemporalClientService(
        TemporalConfiguration config,
        ILogger<TemporalClientService>? logger = null)
    {
        logger ??= LoggerFactory.CreateLogger<TemporalClientService>();
        return new TemporalClientService(config, logger);
    }

    /// <summary>
    /// Creates a Temporal client service from environment variables.
    /// Expected variables: TEMPORAL_SERVER_URL, TEMPORAL_NAMESPACE, 
    /// TEMPORAL_CERT_BASE64 (optional), TEMPORAL_KEY_BASE64 (optional).
    /// </summary>
    /// <param name="logger">Optional custom logger.</param>
    /// <returns>A configured Temporal client service.</returns>
    /// <exception cref="InvalidOperationException">Thrown if required environment variables are missing.</exception>
    public static ITemporalClientService CreateTemporalClientServiceFromEnvironment(
        ILogger<TemporalClientService>? logger = null)
    {
        var serverUrl = Environment.GetEnvironmentVariable("TEMPORAL_SERVER_URL");
        var @namespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE");
        var certBase64 = Environment.GetEnvironmentVariable("TEMPORAL_CERT_BASE64");
        var keyBase64 = Environment.GetEnvironmentVariable("TEMPORAL_KEY_BASE64");

        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new InvalidOperationException("TEMPORAL_SERVER_URL environment variable is required");

        if (string.IsNullOrWhiteSpace(@namespace))
            throw new InvalidOperationException("TEMPORAL_NAMESPACE environment variable is required");

        var config = new TemporalConfiguration
        {
            ServerUrl = serverUrl,
            Namespace = @namespace,
            CertificateBase64 = certBase64,
            PrivateKeyBase64 = keyBase64
        };

        return CreateTemporalClientService(config, logger);
    }
}

