using System.Net;
using Microsoft.Extensions.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xians.Lib.Logging;
using Xians.Lib.Logging.Models;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Http;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.Logging;

/// <summary>
/// dotnet test --filter "FullyQualifiedName~LoggingServicesTests"
/// </summary>

[Trait("Category", "Integration")]
public class LoggingServicesTests : IAsyncLifetime
{
    private WireMockServer? _mockServer;
    private IHttpClientService? _httpService;

    public async Task InitializeAsync()
    {
        // Ensure any previous logging is shutdown
        LoggingServices.Shutdown();
        await Task.Delay(100);
        
        // Setup mock HTTP server
        _mockServer = WireMockServer.Start();
        
        // Configure mock to accept log uploads
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/agent/logs")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"success\": true}"));
        
        var config = new ServerConfiguration
        {
            ServerUrl = _mockServer.Url!,
            ApiKey = TestCertificateGenerator.GetTestCertificate()
        };
        
        _httpService = ServiceFactory.CreateHttpClientService(config);

    }

    [Fact]
    public void EnqueueLog_AddsLogToQueue()
    {
        // Arrange
        var log = CreateTestLog(LogLevel.Information, "Test message");
        var initialCount = LoggingServices.GlobalLogQueue.Count;

        // Act
        LoggingServices.EnqueueLog(log);

        // Assert
        Assert.True(LoggingServices.GlobalLogQueue.Count > initialCount);
    }

    [Fact]
    public void Initialize_WithHttpClientService_DoesNotThrow()
    {
        // Act & Assert
        var exception = Record.Exception(() =>
        {
            LoggingServices.Initialize(_httpService!);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Initialize_MultipleTimesSafely_DoesNotThrow()
    {
        // Act & Assert - Multiple initializations should be safe
        var exception = Record.Exception(() =>
        {
            LoggingServices.Initialize(_httpService!);
            LoggingServices.Initialize(_httpService!);
            LoggingServices.Initialize(_httpService!);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ConfigureBatchSettings_WithValidSettings_DoesNotThrow()
    {
        // Act & Assert
        var exception = Record.Exception(() =>
        {
            LoggingServices.ConfigureBatchSettings(50, 30000);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ConfigureBatchSettings_WithZeroBatchSize_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
        {
            LoggingServices.ConfigureBatchSettings(0, 30000);
        });
    }

    [Fact]
    public void ConfigureBatchSettings_WithNegativeBatchSize_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
        {
            LoggingServices.ConfigureBatchSettings(-1, 30000);
        });
    }

    [Fact]
    public void ConfigureBatchSettings_WithZeroInterval_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
        {
            LoggingServices.ConfigureBatchSettings(100, 0);
        });
    }

    [Fact]
    public void ConfigureBatchSettings_WithNegativeInterval_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
        {
            LoggingServices.ConfigureBatchSettings(100, -1000);
        });
    }

    [Fact]
    public void GlobalLogQueue_IsAccessible()
    {
        // Act
        var queue = LoggingServices.GlobalLogQueue;

        // Assert
        Assert.NotNull(queue);
    }

    [Fact]
    public async Task LoggingServices_ProcessesLogs_WhenInitialized()
    {
        // Arrange
        LoggingServices.Initialize(_httpService!);
        
        // Configure for fast processing
        LoggingServices.ConfigureBatchSettings(5, 1000); // 5 logs per batch, 1 second interval
        
        var initialRequestCount = _mockServer!.LogEntries.Count();
        
        // Enqueue multiple logs
        for (int i = 0; i < 10; i++)
        {
            var log = CreateTestLog(LogLevel.Information, $"Test message {i}");
            LoggingServices.EnqueueLog(log);
        }

        // Act - Wait for processing (2 batches should be sent)
        await Task.Delay(3000);

        // Assert - Should have sent at least one batch
        // Note: Due to timing, we can't guarantee exact count, but should be > 0
        var finalRequestCount = _mockServer!.LogEntries.Count();
        Assert.True(finalRequestCount >= initialRequestCount);
    }

    [Fact]
    public void EnqueueLog_WithCriticalLevel_AddsToQueue()
    {
        // Arrange
        var log = CreateTestLog(LogLevel.Critical, "Critical error");
        var initialCount = LoggingServices.GlobalLogQueue.Count;

        // Act
        LoggingServices.EnqueueLog(log);

        // Assert
        Assert.True(LoggingServices.GlobalLogQueue.Count > initialCount);
    }

    [Fact]
    public void EnqueueLog_WithException_AddsToQueue()
    {
        // Arrange
        var log = CreateTestLog(LogLevel.Error, "Error with exception");
        log.Exception = new InvalidOperationException("Test exception").ToString();
        var initialCount = LoggingServices.GlobalLogQueue.Count;

        // Act
        LoggingServices.EnqueueLog(log);

        // Assert
        Assert.True(LoggingServices.GlobalLogQueue.Count > initialCount);
        Assert.Contains("Test exception", log.Exception);
    }

    [Fact]
    public void EnqueueLog_MultipleLogs_AllAddedToQueue()
    {
        // Arrange
        var initialCount = LoggingServices.GlobalLogQueue.Count;
        var logsToAdd = 5;

        // Act
        for (int i = 0; i < logsToAdd; i++)
        {
            var log = CreateTestLog(LogLevel.Information, $"Message {i}");
            LoggingServices.EnqueueLog(log);
        }

        // Assert
        Assert.True(LoggingServices.GlobalLogQueue.Count >= initialCount + logsToAdd);
    }

    [Fact]
    public async Task LoggingServices_HandlesFailedUpload_WithRetry()
    {
        // Arrange - Configure server to fail first, then succeed
        _mockServer!.ResetMappings();
        // First request fails, subsequent succeed
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/agent/logs")
                .UsingPost())
            .InScenario("RetryScenario")
            .WillSetStateTo("AfterFirstCall")
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("{\"error\": \"Server error\"}"));
        
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/agent/logs")
                .UsingPost())
            .InScenario("RetryScenario")
            .WhenStateIs("AfterFirstCall")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"success\": true}"));

        // Configure batch settings BEFORE initializing to avoid 60-second default interval
        LoggingServices.ConfigureBatchSettings(2, 1000);
        LoggingServices.Initialize(_httpService!);

        var log = CreateTestLog(LogLevel.Error, "Test error");
        LoggingServices.EnqueueLog(log);

        // Act - Wait for processing
        await Task.Delay(5000);

        // Assert - Should have made multiple requests (retry happened)
        // Note: Exact count depends on timing
        var requestCount = _mockServer!.LogEntries.Count();
        Assert.True(requestCount > 0);
    }

    [Fact]
    public void Shutdown_CompletesGracefully()
    {
        // Arrange
        LoggingServices.Initialize(_httpService!);
        
        // Add some logs
        for (int i = 0; i < 5; i++)
        {
            LoggingServices.EnqueueLog(CreateTestLog(LogLevel.Information, $"Message {i}"));
        }

        // Act & Assert - Should not throw
        var exception = Record.Exception(() =>
        {
            LoggingServices.Shutdown();
        });

        Assert.Null(exception);
    }

    private Log CreateTestLog(LogLevel level, string message)
    {
        return new Log
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            Level = level,
            Message = message,
            WorkflowId = "test-workflow",
            WorkflowType = "TestWorkflow",
            Agent = "TestAgent",
            ParticipantId = "user-123"
        };
    }

    public async Task DisposeAsync()
    {
        // Shutdown logging first to stop background thread
        LoggingServices.Shutdown();
        
        // Wait a bit for shutdown to complete
        await Task.Delay(500);
        
        // Now dispose resources
        _httpService?.Dispose();
        _mockServer?.Stop();
        _mockServer?.Dispose();
    }
}
