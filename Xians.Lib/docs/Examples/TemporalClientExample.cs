using Xians.Lib.Configuration.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Common;
using Xians.Lib.Common.Infrastructure;

namespace Xians.Lib.Examples;

/// <summary>
/// Example usage of the Temporal client service.
/// </summary>
public class TemporalClientExample
{
    public static async Task RunBasicExample()
    {
        // Create configuration
        var config = new TemporalConfiguration
        {
            ServerUrl = "localhost:7233",
            Namespace = "default"
        };

        // Create Temporal service
        using var temporalService = ServiceFactory.CreateTemporalClientService(config);

        // Get client (connects automatically)
        var client = await temporalService.GetClientAsync();
        Console.WriteLine($"Connected to namespace: {client.Options.Namespace}");

        // Check health
        var isHealthy = temporalService.IsConnectionHealthy();
        Console.WriteLine($"Connection is healthy: {isHealthy}");
    }

    public static async Task RunTlsExample()
    {
        // Configuration with mTLS
        var config = new TemporalConfiguration
        {
            ServerUrl = "temporal.example.com:7233",
            Namespace = "production",
            CertificateBase64 = "your-cert-base64",
            PrivateKeyBase64 = "your-key-base64"
        };

        using var temporalService = ServiceFactory.CreateTemporalClientService(config);

        // Get client with TLS
        var client = await temporalService.GetClientAsync();
        Console.WriteLine($"Secure connection established to namespace: {client.Options.Namespace}");
    }

    public static async Task RunEnvironmentExample()
    {
        // Set environment variables (or use system environment)
        Environment.SetEnvironmentVariable("TEMPORAL_SERVER_URL", "localhost:7233");
        Environment.SetEnvironmentVariable("TEMPORAL_NAMESPACE", "default");

        // Create from environment
        using var temporalService = ServiceFactory.CreateTemporalClientServiceFromEnvironment();

        // Use the client
        var client = await temporalService.GetClientAsync();
        Console.WriteLine($"Connected via environment config: {client.Options.Namespace}");

        // Disconnect when done
        await temporalService.DisconnectAsync();
        Console.WriteLine("Disconnected successfully");
    }

    public static async Task RunReconnectionExample()
    {
        var config = new TemporalConfiguration
        {
            ServerUrl = "localhost:7233",
            Namespace = "default",
            MaxRetryAttempts = 5,
            RetryDelaySeconds = 3
        };

        using var temporalService = ServiceFactory.CreateTemporalClientService(config);

        // Initial connection
        var client = await temporalService.GetClientAsync();
        Console.WriteLine("Initial connection established");

        // Simulate connection issue and force reconnect
        await temporalService.ForceReconnectAsync();
        
        // Get client again (will reconnect automatically)
        client = await temporalService.GetClientAsync();
        Console.WriteLine("Reconnected successfully");
    }
}

