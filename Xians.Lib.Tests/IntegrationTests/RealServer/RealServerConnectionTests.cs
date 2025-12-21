using Xians.Lib.Common;
using Xians.Lib.Configuration;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Tests for basic server connectivity, HTTP client, and settings retrieval.
/// These tests verify that the server is reachable and responding correctly.
/// </summary>
[Trait("Category", "RealServer")]
public class RealServerConnectionTests : RealServerTestBase
{
    [Fact]
    public void ShouldHaveValidCredentials()
    {
        if (!RunRealServerTests)
        {
            Assert.Fail("SERVER_URL and API_KEY must be set in .env file to run real server tests");
        }

        Assert.NotEmpty(ServerUrl!);
        Assert.NotEmpty(ApiKey!);
        Assert.True(Uri.IsWellFormedUriString(ServerUrl!, UriKind.Absolute), 
            $"SERVER_URL must be a valid URL. Got: {ServerUrl}");
    }

    [Fact]
    public async Task HttpClient_ShouldConnectToRealServer()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        // Arrange - Create HTTP client with REAL server credentials
        var config = new ServerConfiguration
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };

        using var httpService = ServiceFactory.CreateHttpClientService(config);

        // Act - Try to connect to the REAL server
        var isHealthy = await httpService.IsHealthyAsync();

        // Assert - Should be able to connect
        Assert.True(isHealthy, 
            $"Failed to connect to server at {ServerUrl}. Check your SERVER_URL and API_KEY in .env");
    }

    [Fact]
    public async Task HttpClient_ShouldFetchSettingsFromRealServer()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        // Arrange
        var config = new ServerConfiguration
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };

        using var httpService = ServiceFactory.CreateHttpClientService(config);

        // Act - Fetch settings from REAL server endpoint
        try
        {
            var settings = await SettingsService.GetSettingsAsync(httpService);

            // Assert - Should get valid Temporal configuration
            Assert.NotNull(settings);
            Assert.NotEmpty(settings.FlowServerUrl);
            Assert.NotEmpty(settings.FlowServerNamespace);
            
            Console.WriteLine($"✓ Connected to: {ServerUrl}");
            Console.WriteLine($"✓ FlowServerUrl: {settings.FlowServerUrl}");
            Console.WriteLine($"✓ FlowServerNamespace: {settings.FlowServerNamespace}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Failed to fetch settings from {ServerUrl}/api/agent/settings/flowserver. " +
                       $"Error: {ex.Message}. Check your API_KEY and server endpoint.");
        }
    }

    [Fact]
    public async Task CreateServicesFromEnvironment_ShouldConnectToRealServer()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        // Act - Create services using .env configuration
        var (httpService, temporalService) = 
            await ServiceFactory.CreateServicesFromEnvironmentAsync();

        try
        {
            // Assert - HTTP service should be connected
            Assert.NotNull(httpService);
            var httpHealthy = await httpService.IsHealthyAsync();
            Assert.True(httpHealthy, "HTTP service should be healthy");

            // Assert - Temporal service should be configured (but may not connect if Temporal not available)
            Assert.NotNull(temporalService);
            
            Console.WriteLine("✓ Successfully created services from .env");
            Console.WriteLine($"✓ HTTP service healthy: {httpHealthy}");
            Console.WriteLine($"✓ Temporal service created: {temporalService.IsConnectionHealthy()}");
        }
        finally
        {
            httpService.Dispose();
            temporalService.Dispose();
        }
    }
}


