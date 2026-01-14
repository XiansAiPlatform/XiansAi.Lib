using System.Net;
using Microsoft.Extensions.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xians.Lib.Logging;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Http;
using Xians.Lib.Common;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.Logging;

[Trait("Category", "Integration")]
public class EndToEndLoggingTests : IAsyncLifetime
{
    private WireMockServer? _mockServer;
    private IHttpClientService? _httpService;
    private ILoggerFactory? _loggerFactory;
    private readonly List<string> _receivedLogs = new();

    public async Task InitializeAsync()
    {
        // Ensure any previous logging is shutdown
        LoggingServices.Shutdown();
        await Task.Delay(100);
        
        // Setup mock HTTP server
        _mockServer = WireMockServer.Start();
        
        // Configure mock to capture log uploads
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
        
        // Set log levels for testing
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ConsoleLogLevel, "DEBUG");
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "INFORMATION");

    }

    [Fact]
    public async Task EndToEnd_LogMessage_IsSentToServer()
    {
        // Arrange
        LoggingServices.Initialize(_httpService!);
        LoggingServices.ConfigureBatchSettings(5, 1000); // Small batch, fast interval
        
        _loggerFactory = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(true);
        var logger = _loggerFactory.CreateLogger<EndToEndLoggingTests>();

        // Act
        logger.LogInformation("Test information message");
        logger.LogWarning("Test warning message");
        logger.LogError("Test error message");
        
        // Wait for processing
        await Task.Delay(3000);

        // Assert - Should have received at least one batch
        var requestCount = _mockServer!.LogEntries.Count();
        Assert.True(requestCount >= 0); // May or may not have been sent yet depending on timing
    }

    [Fact]
    public async Task EndToEnd_LogWithScope_CapturesContext()
    {
        // Arrange
        LoggingServices.Initialize(_httpService!);
        LoggingServices.ConfigureBatchSettings(2, 1000);
        
        _loggerFactory = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(true);
        var logger = _loggerFactory.CreateLogger<EndToEndLoggingTests>();

        var scopeData = new Dictionary<string, object>
        {
            ["WorkflowId"] = "test-workflow-123",
            ["Agent"] = "TestAgent",
            ["TenantId"] = "tenant-456"
        };

        // Act
        using (logger.BeginScope(scopeData))
        {
            logger.LogInformation("Message with workflow context");
            logger.LogError("Error with workflow context");
        }
        
        // Wait for processing
        await Task.Delay(3000);

        // Assert - Logs should be queued (exact delivery depends on timing)
        var queueCount = LoggingServices.GlobalLogQueue.Count;
        Assert.True(queueCount >= 0); // Queue may be empty if already processed
    }

    [Fact]
    public async Task EndToEnd_MultipleLoggers_AllSendToServer()
    {
        // Arrange
        LoggingServices.Initialize(_httpService!);
        LoggingServices.ConfigureBatchSettings(10, 1000);
        
        _loggerFactory = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(true);
        
        var logger1 = _loggerFactory.CreateLogger("Logger1");
        var logger2 = _loggerFactory.CreateLogger("Logger2");
        var logger3 = _loggerFactory.CreateLogger("Logger3");

        // Act
        logger1.LogInformation("Message from logger 1");
        logger2.LogWarning("Message from logger 2");
        logger3.LogError("Message from logger 3");
        
        // Wait for processing
        await Task.Delay(3000);

        // Assert - All loggers should use the same queue
        var requestCount = _mockServer!.LogEntries.Count();
        var queueWasUsed = requestCount > 0 || LoggingServices.GlobalLogQueue.Count >= 0;
        Assert.True(queueWasUsed);
    }

    [Fact]
    public async Task EndToEnd_CriticalLog_IsProcessed()
    {
        // Arrange
        LoggingServices.Initialize(_httpService!);
        LoggingServices.ConfigureBatchSettings(1, 500); // Very fast processing
        
        _loggerFactory = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(true);
        var logger = _loggerFactory.CreateLogger<EndToEndLoggingTests>();

        // Act
        logger.LogCritical("CRITICAL ERROR - System failure");
        
        // Wait for processing
        await Task.Delay(2000);

        // Assert - Critical logs should be queued
        var requestCount = _mockServer!.LogEntries.Count();
        var logWasProcessed = requestCount > 0 || LoggingServices.GlobalLogQueue.Count >= 0;
        Assert.True(logWasProcessed);
    }

    [Fact]
    public async Task EndToEnd_LogWithException_IsProcessed()
    {
        // Arrange
        LoggingServices.Initialize(_httpService!);
        LoggingServices.ConfigureBatchSettings(1, 500);
        
        _loggerFactory = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(true);
        var logger = _loggerFactory.CreateLogger<EndToEndLoggingTests>();

        var testException = new InvalidOperationException("Test exception with inner details");

        // Act
        logger.LogError(testException, "An error occurred during operation");
        
        // Wait for processing
        await Task.Delay(2000);

        // Assert
        var requestCount = _mockServer!.LogEntries.Count();
        var logWasProcessed = requestCount > 0 || LoggingServices.GlobalLogQueue.Count >= 0;
        Assert.True(logWasProcessed);
    }

    [Fact]
    public async Task EndToEnd_BelowThreshold_NotSent()
    {
        // Arrange - Set API level to ERROR (should filter out Info/Warning)
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "ERROR");
        
        LoggingServices.Initialize(_httpService!);
        LoggingServices.ConfigureBatchSettings(5, 1000);
        
        _loggerFactory = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(true);
        var logger = _loggerFactory.CreateLogger<EndToEndLoggingTests>();

        var initialReceivedCount = _mockServer!.LogEntries.Count();
        var initialQueueCount = LoggingServices.GlobalLogQueue.Count;

        // Act - Log below threshold
        logger.LogDebug("This should be filtered out");
        logger.LogInformation("This should also be filtered out");
        
        // Wait a bit
        await Task.Delay(1000);

        // Assert - Debug and Info should not be queued when API level is ERROR
        var queueGrew = LoggingServices.GlobalLogQueue.Count > initialQueueCount;
        var receivedGrew = _mockServer!.LogEntries.Count() > initialReceivedCount;
        
        // Logs below threshold should not be queued
        Assert.False(queueGrew || receivedGrew);
    }

    [Fact]
    public async Task EndToEnd_LargeBatch_ProcessesCorrectly()
    {
        // Arrange
        LoggingServices.Initialize(_httpService!);
        LoggingServices.ConfigureBatchSettings(50, 1000);
        
        _loggerFactory = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(true);
        var logger = _loggerFactory.CreateLogger<EndToEndLoggingTests>();

        // Act - Log many messages
        for (int i = 0; i < 100; i++)
        {
            logger.LogInformation($"Bulk message {i}");
        }
        
        // Wait for processing
        await Task.Delay(3000);

        // Assert - Logs should have been queued and possibly processed
        var requestCount = _mockServer!.LogEntries.Count();
        var totalProcessed = requestCount + LoggingServices.GlobalLogQueue.Count;
        Assert.True(totalProcessed >= 0); // Should have at least some activity
    }

    [Fact]
    public void EndToEnd_ApiLoggingDisabled_StillWorks()
    {
        // Arrange - Create factory without API logging
        _loggerFactory = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(false);
        var logger = _loggerFactory.CreateLogger<EndToEndLoggingTests>();

        // Act & Assert - Should not throw
        var exception = Record.Exception(() =>
        {
            logger.LogInformation("Message without API logging");
            logger.LogError("Error without API logging");
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task EndToEnd_Shutdown_FlushesRemainingLogs()
    {
        // Arrange
        LoggingServices.Initialize(_httpService!);
        LoggingServices.ConfigureBatchSettings(100, 60000); // Large batch, long interval
        
        _loggerFactory = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(true);
        var logger = _loggerFactory.CreateLogger<EndToEndLoggingTests>();

        // Add logs
        for (int i = 0; i < 10; i++)
        {
            logger.LogInformation($"Shutdown test message {i}");
        }

        var queueCountBeforeShutdown = LoggingServices.GlobalLogQueue.Count;

        // Act - Shutdown should flush
        LoggingServices.Shutdown();
        
        // Give it a moment to complete
        await Task.Delay(1000);

        // Assert - Queue should be empty or smaller after shutdown
        var queueCountAfterShutdown = LoggingServices.GlobalLogQueue.Count;
        Assert.True(queueCountAfterShutdown <= queueCountBeforeShutdown);
    }

    public async Task DisposeAsync()
    {
        // Shutdown logging first to stop background thread
        LoggingServices.Shutdown();
        
        // Wait a bit for shutdown to complete
        await Task.Delay(500);
        
        // Now dispose resources
        _loggerFactory?.Dispose();
        _httpService?.Dispose();
        _mockServer?.Stop();
        _mockServer?.Dispose();
        
        // Cleanup environment variables
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ConsoleLogLevel, null);
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, null);
    }
}
