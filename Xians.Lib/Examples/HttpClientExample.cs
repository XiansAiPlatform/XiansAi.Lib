using Xians.Lib.Configuration;
using Xians.Lib.Http;
using Xians.Lib.Common;

namespace Xians.Lib.Examples;

/// <summary>
/// Example usage of the HTTP client service.
/// </summary>
public class HttpClientExample
{
    public static async Task RunBasicExample()
    {
        // Create configuration
        var config = new ServerConfiguration
        {
            ServerUrl = "https://api.example.com",
            ApiKey = "your-api-key-here"
        };

        // Create HTTP service
        using var httpService = ServiceFactory.CreateHttpClientService(config);

        // Test connection
        await httpService.TestConnectionAsync();
        Console.WriteLine("Connection successful!");

        // Make a GET request with retry
        var response = await httpService.GetWithRetryAsync("/api/users");
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {content}");
        }
    }

    public static async Task RunEnvironmentExample()
    {
        // Set environment variables (or use system environment)
        Environment.SetEnvironmentVariable("SERVER_URL", "https://api.example.com");
        Environment.SetEnvironmentVariable("API_KEY", "your-api-key-here");

        // Create from environment
        using var httpService = ServiceFactory.CreateHttpClientServiceFromEnvironment();

        // Check health
        var isHealthy = await httpService.IsHealthyAsync();
        Console.WriteLine($"Service is healthy: {isHealthy}");

        // Use the client
        var client = httpService.Client;
        var response = await client.GetAsync("/api/status");
        Console.WriteLine($"Status: {response.StatusCode}");
    }

    public static async Task RunAdvancedExample()
    {
        var config = new ServerConfiguration
        {
            ServerUrl = "https://api.example.com",
            ApiKey = "your-api-key-here",
            // Customize retry and timeout settings
            MaxRetryAttempts = 5,
            RetryDelaySeconds = 3,
            TimeoutSeconds = 60
        };

        using var httpService = ServiceFactory.CreateHttpClientService(config);

        // Execute custom operation with retry
        var result = await httpService.ExecuteWithRetryAsync(async () =>
        {
            var client = await httpService.GetHealthyClientAsync();
            var response = await client.GetAsync("/api/data");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        });

        Console.WriteLine($"Result: {result}");

        // Force reconnection if needed
        await httpService.ForceReconnectAsync();
        Console.WriteLine("Reconnected successfully");
    }
}

