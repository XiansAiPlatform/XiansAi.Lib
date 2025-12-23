using DotNetEnv;
using Xians.Lib.Common;
using Xians.Lib.Common.Exceptions;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Temporal;

namespace Xians.Lib.Tests.IntegrationTests.Temporal;

[Trait("Category", "Integration")]
public class TemporalClientIntegrationTests
{
    private readonly bool _runIntegrationTests;

    public TemporalClientIntegrationTests()
    {
        // Load environment variables
        try
        {
            Env.Load();
        }
        catch
        {
            // .env file may not exist
        }

        _runIntegrationTests = bool.TryParse(
            Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"),
            out var shouldRun) && shouldRun;
    }

    [Fact]
    public void CreateTemporalService_WithValidConfig_ShouldNotThrow()
    {
        if (!_runIntegrationTests)
        {
            // Skip test if integration tests are disabled
            return;
        }

        // Arrange
        var config = new TemporalConfiguration
        {
            ServerUrl = Environment.GetEnvironmentVariable("TEMPORAL_SERVER_URL") ?? "localhost:7233",
            Namespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default"
        };

        // Act & Assert
        using var service = ServiceFactory.CreateTemporalClientService(config);
        Assert.NotNull(service);
    }

    [Fact]
    public async Task GetClientAsync_WithRunningTemporalServer_ShouldConnect()
    {
        if (!_runIntegrationTests)
        {
            // Skip test if integration tests are disabled
            return;
        }

        // Arrange
        var config = new TemporalConfiguration
        {
            ServerUrl = Environment.GetEnvironmentVariable("TEMPORAL_SERVER_URL") ?? "localhost:7233",
            Namespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default"
        };

        using var service = ServiceFactory.CreateTemporalClientService(config);

        // Act
        var client = await service.GetClientAsync();

        // Assert
        Assert.NotNull(client);
        Assert.True(service.IsConnectionHealthy());
    }

    [Fact]
    public async Task IsConnectionHealthy_WithConnectedClient_ShouldReturnTrue()
    {
        if (!_runIntegrationTests)
        {
            return;
        }

        // Arrange
        var config = new TemporalConfiguration
        {
            ServerUrl = Environment.GetEnvironmentVariable("TEMPORAL_SERVER_URL") ?? "localhost:7233",
            Namespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default"
        };

        using var service = ServiceFactory.CreateTemporalClientService(config);
        await service.GetClientAsync(); // Initialize connection

        // Act
        var isHealthy = service.IsConnectionHealthy();

        // Assert
        Assert.True(isHealthy);
    }

    [Fact]
    public async Task GetClientAsync_WithInvalidServer_ShouldThrow()
    {
        // Arrange - Use invalid server that will fail to connect
        var config = new TemporalConfiguration
        {
            ServerUrl = "invalid-server:9999",
            Namespace = "test"
        };

        using var service = ServiceFactory.CreateTemporalClientService(config);

        // Act & Assert - Expect TemporalConnectionException after connection attempts fail
        await Assert.ThrowsAsync<TemporalConnectionException>(async () => 
            await service.GetClientAsync());
    }

    [Fact]
    public void CreateTemporalService_WithInvalidConfig_ShouldThrow()
    {
        // Arrange
        var config = new TemporalConfiguration
        {
            ServerUrl = "",
            Namespace = "test"
        };

        // Act & Assert - Configuration validation throws ConfigurationException
        Assert.Throws<ConfigurationException>(() => 
            ServiceFactory.CreateTemporalClientService(config));
    }

    [Fact]
    public async Task Dispose_AfterConnection_ShouldCleanupResources()
    {
        if (!_runIntegrationTests)
        {
            return;
        }

        // Arrange
        var config = new TemporalConfiguration
        {
            ServerUrl = Environment.GetEnvironmentVariable("TEMPORAL_SERVER_URL") ?? "localhost:7233",
            Namespace = Environment.GetEnvironmentVariable("TEMPORAL_NAMESPACE") ?? "default"
        };

        var service = ServiceFactory.CreateTemporalClientService(config);
        await service.GetClientAsync();

        // Act
        service.Dispose();

        // Assert
        Assert.False(service.IsConnectionHealthy());
    }
}

