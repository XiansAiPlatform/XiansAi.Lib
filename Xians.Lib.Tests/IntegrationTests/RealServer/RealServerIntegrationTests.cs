using DotNetEnv;
using Xians.Lib.Common;
using Xians.Lib.Configuration;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// REAL integration tests that actually connect to the server specified in .env
/// These tests will fail if SERVER_URL or API_KEY are invalid.
/// </summary>
[Trait("Category", "RealServer")]
public class RealServerIntegrationTests : IDisposable
{
    private readonly bool _runRealServerTests;
    private readonly string? _serverUrl;
    private readonly string? _apiKey;

    public RealServerIntegrationTests()
    {
        // Load .env file
        try
        {
            Env.Load();
        }
        catch
        {
            // .env file may not exist
        }

        _serverUrl = Environment.GetEnvironmentVariable("SERVER_URL");
        _apiKey = Environment.GetEnvironmentVariable("API_KEY");
        
        // Only run if we have valid credentials
        _runRealServerTests = !string.IsNullOrEmpty(_serverUrl) && 
                              !string.IsNullOrEmpty(_apiKey);
    }

    [Fact]
    public void ShouldHaveValidCredentials()
    {
        if (!_runRealServerTests)
        {
            Assert.Fail("SERVER_URL and API_KEY must be set in .env file to run real server tests");
        }

        Assert.NotEmpty(_serverUrl!);
        Assert.NotEmpty(_apiKey!);
        Assert.True(Uri.IsWellFormedUriString(_serverUrl!, UriKind.Absolute), 
            $"SERVER_URL must be a valid URL. Got: {_serverUrl}");
    }

    [Fact]
    public async Task HttpClient_ShouldConnectToRealServer()
    {
        if (!_runRealServerTests)
        {
            // Skip if no credentials
            return;
        }

        // Arrange - Create HTTP client with REAL server credentials
        var config = new ServerConfiguration
        {
            ServerUrl = _serverUrl!,
            ApiKey = _apiKey!
        };

        using var httpService = ServiceFactory.CreateHttpClientService(config);

        // Act - Try to connect to the REAL server
        var isHealthy = await httpService.IsHealthyAsync();

        // Assert - Should be able to connect
        Assert.True(isHealthy, 
            $"Failed to connect to server at {_serverUrl}. Check your SERVER_URL and API_KEY in .env");
    }

    [Fact]
    public async Task HttpClient_ShouldFetchSettingsFromRealServer()
    {
        if (!_runRealServerTests)
        {
            return;
        }

        // Arrange
        var config = new ServerConfiguration
        {
            ServerUrl = _serverUrl!,
            ApiKey = _apiKey!
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
            
            Console.WriteLine($"✓ Connected to: {_serverUrl}");
            Console.WriteLine($"✓ FlowServerUrl: {settings.FlowServerUrl}");
            Console.WriteLine($"✓ FlowServerNamespace: {settings.FlowServerNamespace}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Failed to fetch settings from {_serverUrl}/api/agent/settings/flowserver. " +
                       $"Error: {ex.Message}. Check your API_KEY and server endpoint.");
        }
    }

    [Fact]
    public async Task CreateServicesFromEnvironment_ShouldConnectToRealServer()
    {
        if (!_runRealServerTests)
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

    [Fact]
    public async Task RealServer_EndToEndTest()
    {
        if (!_runRealServerTests)
        {
            return;
        }

        Console.WriteLine($"Testing against REAL server: {_serverUrl}");
        
        // Step 1: Create HTTP client
        var config = new ServerConfiguration
        {
            ServerUrl = _serverUrl!,
            ApiKey = _apiKey!
        };
        
        using var httpService = ServiceFactory.CreateHttpClientService(config);
        
        // Step 2: Test connection
        var isHealthy = await httpService.IsHealthyAsync();
        Assert.True(isHealthy, "Server should be reachable");
        Console.WriteLine("✓ Step 1: HTTP connection successful");
        
        // Step 3: Fetch settings
        var settings = await SettingsService.GetSettingsAsync(httpService);
        Assert.NotNull(settings);
        Assert.NotEmpty(settings.FlowServerUrl);
        Console.WriteLine($"✓ Step 2: Settings fetched - Temporal: {settings.FlowServerUrl}");
        
        // Step 4: Create Temporal client with fetched settings
        var temporalConfig = settings.ToTemporalConfiguration();
        using var temporalService = ServiceFactory.CreateTemporalClientService(temporalConfig);
        Assert.NotNull(temporalService);
        Console.WriteLine("✓ Step 3: Temporal client created");
        
        // Note: We don't try to connect to Temporal here as it may not be accessible
        // The important test is that we successfully fetched the config from the server
        
        Console.WriteLine("✓ End-to-end test PASSED - Successfully connected to real server!");
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

