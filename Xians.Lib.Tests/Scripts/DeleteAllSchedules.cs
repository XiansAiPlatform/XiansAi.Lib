using DotNetEnv;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Temporal;

namespace Xians.Lib.Tests.Scripts;

/// <summary>
/// Script to connect to Temporal and delete all running schedules in the configured namespace.
/// Run via: dotnet test --filter "FullyQualifiedName~DeleteAllSchedules_Run"
///
/// Configuration (load from .env or environment):
/// - Direct Temporal: TEMPORAL_SERVER_URL (default: localhost:7233), TEMPORAL_NAMESPACE (default: default),
///   TEMPORAL_CERT_BASE64, TEMPORAL_KEY_BASE64 (optional, for mTLS)
/// - Or from server: SERVER_URL, API_KEY (fetches Temporal config from Xians server)
/// </summary>
public static class DeleteAllSchedules
{
    public static async Task RunAsync()
    {
        // Load .env from test project directory
        var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env");
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }
        else
        {
            try { Env.Load(); } catch { /* .env may not exist */ }
        }

        Console.WriteLine("Connecting to Temporal...");

        using var temporalService = await GetTemporalServiceAsync();
        var client = await temporalService.GetClientAsync();

        Console.WriteLine($"Connected to {client.Options.Namespace}. Listing schedules...\n");

        var scheduleListStream = client.ListSchedulesAsync();
        var deletedCount = 0;
        var errorCount = 0;

        await foreach (var scheduleEntry in scheduleListStream)
        {
            var scheduleId = scheduleEntry.Id;
            try
            {
                var handle = client.GetScheduleHandle(scheduleId);
                await handle.DeleteAsync();
                Console.WriteLine($"  Deleted: {scheduleId}");
                deletedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Failed to delete '{scheduleId}': {ex.Message}");
                errorCount++;
            }
        }

        Console.WriteLine($"\nDone. Deleted: {deletedCount}, Errors: {errorCount}");
    }

    private static async Task<ITemporalClientService> GetTemporalServiceAsync()
    {
        // Prefer direct Temporal config if explicitly set
        var temporalServerUrl = Environment.GetEnvironmentVariable("TEMPORAL_SERVER_URL");
        var temporalNamespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE");

        if (!string.IsNullOrWhiteSpace(temporalServerUrl) && !string.IsNullOrWhiteSpace(temporalNamespace))
        {
            var config = new TemporalConfiguration
            {
                ServerUrl = temporalServerUrl,
                Namespace = temporalNamespace,
                CertificateBase64 = EnvironmentVariableReader.GetOptional("TEMPORAL_CERT_BASE64"),
                PrivateKeyBase64 = EnvironmentVariableReader.GetOptional("TEMPORAL_KEY_BASE64")
            };
            return ServiceFactory.CreateTemporalClientService(config);
        }

        // Fallback: fetch Temporal config from Xians server (requires SERVER_URL, API_KEY)
        try
        {
            var (_, temporalService) = await ServiceFactory.CreateServicesFromEnvironmentAsync();
            return temporalService;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("environment variable"))
        {
            // No server config - use local defaults
            var config = new TemporalConfiguration
            {
                ServerUrl = "localhost:7233",
                Namespace = "default"
            };
            return ServiceFactory.CreateTemporalClientService(config);
        }
    }
}
