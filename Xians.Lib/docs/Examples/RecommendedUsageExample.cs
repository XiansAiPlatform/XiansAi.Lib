using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Examples;

/// <summary>
/// Demonstrates the recommended way to use Xians.Lib.
/// Only requires SERVER_URL and API_KEY - Temporal config is fetched from the server.
/// This matches the pattern used in XiansAi.Lib.Src.
/// </summary>
public class RecommendedUsageExample
{
    /// <summary>
    /// Example 1: Initialize from environment variables (Recommended)
    /// </summary>
    public static async Task InitializeFromEnvironmentAsync()
    {
        // Prerequisites:
        // - Set environment variable: SERVER_URL=https://api.example.com
        // - Set environment variable: API_KEY=your-api-key

        // Create both HTTP and Temporal services in one call
        // Temporal configuration is automatically fetched from:
        // GET /api/agent/settings/flowserver
        var (httpService, temporalService) = await ServiceFactory.CreateServicesFromEnvironmentAsync();

        try
        {
            // Use HTTP service for API calls
            var response = await httpService.GetWithRetryAsync("/api/users");
            var userData = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"User data: {userData}");

            // Use Temporal service (configured automatically from server settings)
            var temporalClient = await temporalService.GetClientAsync();
            Console.WriteLine("Temporal client ready");

            // Start a workflow, etc.
            // var workflowHandle = await temporalClient.StartWorkflowAsync(...);
        }
        finally
        {
            httpService.Dispose();
            temporalService.Dispose();
        }
    }

    /// <summary>
    /// Example 2: Initialize with explicit credentials (Recommended for programmatic use)
    /// </summary>
    public static async Task InitializeWithCredentialsAsync(string serverUrl, string apiKey)
    {
        // Create services with explicit credentials
        // Temporal configuration is fetched from the server automatically
        var (httpService, temporalService) = await ServiceFactory.CreateServicesFromServerAsync(
            serverUrl: serverUrl,
            apiKey: apiKey
        );

        try
        {
            // Both services are ready to use
            Console.WriteLine("Services initialized successfully");
            
            // HTTP service
            var isHealthy = await httpService.IsHealthyAsync();
            Console.WriteLine($"HTTP service healthy: {isHealthy}");
            
            // Temporal service (config was fetched from server)
            var temporalClient = await temporalService.GetClientAsync();
            Console.WriteLine($"Temporal connection healthy: {temporalService.IsConnectionHealthy()}");
        }
        finally
        {
            httpService.Dispose();
            temporalService.Dispose();
        }
    }

    /// <summary>
    /// Example 3: What happens under the hood
    /// </summary>
    public static async Task UnderTheHoodExample()
    {
        // When you call CreateServicesFromEnvironmentAsync():
        
        // Step 1: HTTP client is created with SERVER_URL and API_KEY
        var httpService = ServiceFactory.CreateHttpClientServiceFromEnvironment();
        
        // Step 2: Settings are fetched from the server
        // GET /api/agent/settings/flowserver
        // Returns JSON:
        // {
        //   "flowServerUrl": "temporal.example.com:7233",
        //   "flowServerNamespace": "production",
        //   "flowServerCertBase64": "...",  // optional
        //   "flowServerPrivateKeyBase64": "..."  // optional
        // }
        var serverSettings = await SettingsService.GetSettingsAsync(httpService);
        
        // Step 3: Temporal configuration is created from server settings
        var temporalConfig = serverSettings.ToTemporalConfiguration();
        
        // Step 4: Temporal service is created with fetched configuration
        var temporalService = ServiceFactory.CreateTemporalClientService(temporalConfig);
        
        Console.WriteLine($"Temporal server from settings: {serverSettings.FlowServerUrl}");
        Console.WriteLine($"Temporal namespace from settings: {serverSettings.FlowServerNamespace}");
        
        httpService.Dispose();
        temporalService.Dispose();
    }

    /// <summary>
    /// Example 4: Override Temporal server URL (for development/testing)
    /// </summary>
    public static async Task OverrideTemporalUrlExample()
    {
        // Set environment variable to override the Temporal server URL from settings
        Environment.SetEnvironmentVariable("TEMPORAL_SERVER_URL", "localhost:7233");
        
        // Even though the server might return a different Temporal URL,
        // it will be overridden with localhost:7233
        var (httpService, temporalService) = await ServiceFactory.CreateServicesFromEnvironmentAsync();
        
        try
        {
            var temporalClient = await temporalService.GetClientAsync();
            Console.WriteLine("Connected to local Temporal server (override)");
        }
        finally
        {
            httpService.Dispose();
            temporalService.Dispose();
        }
    }
}

/// <summary>
/// Server settings response structure.
/// This is what the server returns from: GET /api/agent/settings/flowserver
/// </summary>
public class ServerSettingsExample
{
    public string FlowServerUrl { get; set; } = "temporal.example.com:7233";
    public string FlowServerNamespace { get; set; } = "production";
    public string? FlowServerCertBase64 { get; set; }
    public string? FlowServerPrivateKeyBase64 { get; set; }
    public string ApiKey { get; set; } = "...";
    public string? ProviderName { get; set; } = "openai";
    public string ModelName { get; set; } = "gpt-4";
}



