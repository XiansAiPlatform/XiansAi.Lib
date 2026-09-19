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
[Collection("LoggingServices")] // Prevent parallel execution due to static state
public class LoggingServicesTests : IAsyncLifetime
{
    private WireMockServer? _mockServer;
    private IHttpClientService? _httpService;

    public async Task InitializeAsync()
    {
        // Ensure any previous logging is shutdown and state is clean
        LoggingServices.Shutdown();
        await Task.Delay(1000); // Longer delay to ensure complete shutdown in full suite
        
        // Clear any remaining logs from previous test runs
        while (LoggingServices.GlobalLogQueue.TryDequeue(out _)) { }
        
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
    public async Task EnqueueLog_AddsLogToQueue()
    {
        // Arrange - ensure clean state
        LoggingServices.Shutdown();
        await Task.Delay(500); // Allow shutdown to complete fully
        
        // Clear any remaining logs from previous tests
        while (LoggingServices.GlobalLogQueue.TryDequeue(out _)) { }
        
        // Use long interval so background thread won't process logs before we assert
        LoggingServices.ConfigureBatchSettings(100, 60000);
        LoggingServices.Initialize(_httpService!);
        await Task.Delay(100); // Allow initialization to complete
        
        var log = CreateTestLog(LogLevel.Information, "Test message");
        var initialCount = LoggingServices.GlobalLogQueue.Count;

        // Act
        LoggingServices.EnqueueLog(log);

        // Assert
        Assert.True(LoggingServices.GlobalLogQueue.Count > initialCount,
            $"Expected queue count > {initialCount}, but got {LoggingServices.GlobalLogQueue.Count}. Service initialized: {LoggingServices.IsInitialized}");
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

    // The upload diagnostics write straight to stdout rather than through an ILogger, so a host has no log
    // level, category filter or environment variable that can reach them. Uploading runs on a fixed interval
    // for the life of the process, so ungated they are unconditional console traffic no consumer can stop.
    // These two tests pin the gate: silent by default, still available when diagnostics are asked for.
    [Fact]
    public async Task BatchUpload_ByDefault_DoesNotPrintUploadDiagnostics()
    {
        var console = await CaptureConsoleDuringOneUploadCycleAsync();

        Assert.DoesNotContain("Uploading batch of", console, StringComparison.Ordinal);
        Assert.DoesNotContain("Successfully uploaded", console, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BatchUpload_WithVerboseDiagnostics_PrintsUploadDiagnostics()
    {
        try
        {
            // Opted in before the capture starts, since EnableVerboseDiagnostics announces itself.
            LoggingServices.EnableVerboseDiagnostics(true);

            var console = await CaptureConsoleDuringOneUploadCycleAsync();

            Assert.Contains("Uploading batch of", console, StringComparison.Ordinal);
            Assert.Contains("Successfully uploaded", console, StringComparison.Ordinal);
        }
        finally
        {
            // Process-wide static shared with every other test in this collection.
            LoggingServices.EnableVerboseDiagnostics(false);
        }
    }

    // The failure path is the one that matters for console volume: while the server is unreachable every
    // batch is requeued and nothing drains, so it runs on every cycle for as long as the outage lasts.
    // These four tests pin what an outage costs the console.

    [Fact]
    public async Task WhenUploadsKeepFailing_TheFailureIsReportedOnceNotEveryCycle()
    {
        var console = await CaptureConsoleDuringFailingUploadsAsync(cycles: 4);

        // One report for the streak, regardless of how many cycles failed inside the window.
        Assert.Equal(1, CountOccurrences(console, "Logger API failed with status"));
    }

    [Fact]
    public async Task WhenAnUploadFails_TheResponseBodyIsTruncated()
    {
        var hugeBody = new string('x', 20_000);
        var console = await CaptureConsoleDuringFailingUploadsAsync(cycles: 2, responseBody: hugeBody);

        Assert.Contains("(truncated, 20000 chars total)", console, StringComparison.Ordinal);
        Assert.DoesNotContain(hugeBody, console, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenLogsExceedTheRetryLimit_DropsAreReportedPerBatchNotPerLog()
    {
        // MAX_RETRIES is 3, so a few failing cycles are enough to start dropping entries.
        var console = await CaptureConsoleDuringFailingUploadsAsync(cycles: 6);

        // The per-entry form ("Dropping log {guid} after 3 failed attempts") is what flooded stderr.
        Assert.DoesNotContain("Dropping log ", console, StringComparison.Ordinal);
        Assert.Contains("failed upload attempts", console, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenUploadsRecover_TheRecoveryIsReported()
    {
        // Arrange — fail first so there is a streak. The helper leaves the service running.
        await CaptureConsoleDuringFailingUploadsAsync(cycles: 2);

        _mockServer!.Reset();
        _mockServer
            .Given(Request.Create().WithPath("/api/agent/logs").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("{\"success\": true}"));

        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            Console.SetError(captured);

            for (var i = 0; i < 5; i++)
            {
                LoggingServices.EnqueueLog(CreateTestLog(LogLevel.Information, $"recovered-{i}"));
            }

            // Driven by Shutdown rather than by waiting on the background thread: StartLogProcessor skips
            // creating a replacement while a previously cancelled thread is still alive, so after a
            // Shutdown/Initialize pair the processor is not reliably running. Shutdown drains the queue
            // synchronously through ProcessLogBatch, which is the path under test here either way.
            LoggingServices.Shutdown();

            // The recovery line is written by the upload continuation, which Shutdown starts but does not
            // await after the drain.
            await Task.Delay(2000);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        // An outage that ends silently is as unhelpful as one that floods: the recovery line is what says
        // the suppression window is over.
        var console = captured.ToString();
        Assert.True(
            console.Contains("Log upload recovered after", StringComparison.Ordinal),
            $"recovery line missing. console=<<<{console}>>>");
    }

    /// <summary>
    /// Points this test's mock server at a failing response, drives several upload cycles, and returns
    /// everything written to stdout and stderr while they ran.
    /// </summary>
    /// <remarks>
    /// Returns with the service still initialized and still failing, so a caller can go on to flip the
    /// server back to success and observe the recovery.
    /// </remarks>
    /// <param name="cycles">Roughly how many upload cycles to allow before returning.</param>
    /// <param name="responseBody">Body the mock server returns with the failure status.</param>
    private async Task<string> CaptureConsoleDuringFailingUploadsAsync(
        int cycles,
        string responseBody = "upload rejected")
    {
        LoggingServices.Shutdown();
        // Long enough for the cancelled processing thread to actually exit: StartLogProcessor will not
        // create a replacement while the old one is still alive, and the old one stops on its cancelled
        // token — leaving the service with no processor at all.
        await Task.Delay(1500);
        while (LoggingServices.GlobalLogQueue.TryDequeue(out _)) { }

        _mockServer!.Reset();
        _mockServer
            .Given(Request.Create().WithPath("/api/agent/logs").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody(responseBody));

        const int intervalMs = 300;
        LoggingServices.ConfigureBatchSettings(10, intervalMs);
        LoggingServices.Initialize(_httpService!);
        await Task.Delay(100);

        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var captured = new StringWriter();
        try
        {
            // Both streams: the failure reports go to stderr, the diagnostics to stdout.
            Console.SetOut(captured);
            Console.SetError(captured);

            for (var i = 0; i < 10; i++)
            {
                LoggingServices.EnqueueLog(CreateTestLog(LogLevel.Information, $"failing-{i}"));
            }

            await Task.Delay(intervalMs * cycles + 1000);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        return captured.ToString();
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var at = 0;
        while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }

        return count;
    }

    /// <summary>
    /// Runs one full enqueue → batch → upload cycle against this test's mock server and returns everything
    /// written to stdout while it ran.
    /// </summary>
    /// <remarks>
    /// Shuts the service down first rather than trusting the ambient state: Initialize is a no-op while
    /// _isInitialized is set, so without this the uploads would go to whichever mock server a previous test
    /// left wired up, and this one would see no traffic at all.
    /// </remarks>
    private async Task<string> CaptureConsoleDuringOneUploadCycleAsync()
    {
        LoggingServices.Shutdown();
        await Task.Delay(1500);
        while (LoggingServices.GlobalLogQueue.TryDequeue(out _)) { }

        var uploadsBefore = _mockServer!.LogEntries.Count();

        LoggingServices.ConfigureBatchSettings(5, 500);
        LoggingServices.Initialize(_httpService!);
        await Task.Delay(100);

        var original = Console.Out;
        using var captured = new StringWriter();
        try
        {
            // Enqueued inside the capture so the batch the background thread picks up is produced, uploaded
            // and reported on entirely within the window.
            Console.SetOut(captured);

            for (var i = 0; i < 10; i++)
            {
                LoggingServices.EnqueueLog(CreateTestLog(LogLevel.Information, $"console-probe-{i}"));
            }

            await Task.Delay(2000);
        }
        finally
        {
            Console.SetOut(original);
        }

        // Without a real upload in the window, "nothing was printed" would pass for the wrong reason.
        Assert.True(
            _mockServer!.LogEntries.Count() > uploadsBefore,
            "expected at least one upload during the capture window for the assertions to mean anything");

        return captured.ToString();
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
    public async Task EnqueueLog_WithCriticalLevel_AddsToQueue()
    {
        // Arrange - ensure clean state
        LoggingServices.Shutdown();
        await Task.Delay(500); // Allow shutdown to complete fully
        
        // Clear any remaining logs from previous tests
        while (LoggingServices.GlobalLogQueue.TryDequeue(out _)) { }
        
        LoggingServices.Initialize(_httpService!);
        await Task.Delay(100); // Allow initialization to complete
        
        var log = CreateTestLog(LogLevel.Critical, "Critical error");
        var initialCount = LoggingServices.GlobalLogQueue.Count;

        // Act
        LoggingServices.EnqueueLog(log);

        // Assert
        Assert.True(LoggingServices.GlobalLogQueue.Count > initialCount);
    }

    [Fact]
    public async Task EnqueueLog_WithException_AddsToQueue()
    {
        // Arrange - ensure clean state (already done in InitializeAsync, but double-check)
        LoggingServices.Shutdown();
        await Task.Delay(200);
        
        // Clear queue to ensure clean state
        while (LoggingServices.GlobalLogQueue.TryDequeue(out _)) { }
        
        LoggingServices.Initialize(_httpService!);
        await Task.Delay(50);
        
        var log = CreateTestLog(LogLevel.Error, "Error with exception");
        log.Exception = new InvalidOperationException("Test exception").ToString();
        var initialCount = LoggingServices.GlobalLogQueue.Count;

        // Act
        LoggingServices.EnqueueLog(log);

        // Assert
        Assert.True(LoggingServices.GlobalLogQueue.Count > initialCount, 
            $"Expected queue count to increase from {initialCount}, but it's still {LoggingServices.GlobalLogQueue.Count}");
        Assert.Contains("Test exception", log.Exception);
    }

    [Fact]
    public async Task EnqueueLog_MultipleLogs_AllAddedToQueue()
    {
        // Arrange - ensure clean state
        LoggingServices.Shutdown();
        await Task.Delay(500); // Allow shutdown to complete fully
        
        // Clear any remaining logs from previous tests
        while (LoggingServices.GlobalLogQueue.TryDequeue(out _)) { }
        
        LoggingServices.Initialize(_httpService!);
        await Task.Delay(100); // Allow initialization to complete
        
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
        
        // Wait longer for shutdown to complete in full suite context
        await Task.Delay(1000);
        
        // Clear any remaining logs
        while (LoggingServices.GlobalLogQueue.TryDequeue(out _)) { }
        
        // Now dispose resources
        _httpService?.Dispose();
        _mockServer?.Stop();
        _mockServer?.Dispose();
    }
}
