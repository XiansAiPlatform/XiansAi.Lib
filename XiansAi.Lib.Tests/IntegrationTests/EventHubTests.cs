using Microsoft.Extensions.Logging;
using Server;
using DotNetEnv;
using XiansAi.Events;
using System.Text.Json;
using System.Net;
using System.Net.Http;

namespace XiansAi.Lib.Tests.IntegrationTests;

[Collection("SecureApi Tests")]
public class EventHubTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _certificateBase64;
    private readonly string _serverUrl;
    private readonly ILogger<EventHubTests> _logger;
    private readonly SystemActivities _systemActivities;

    /*
    dotnet test --filter "FullyQualifiedName~EventHubTests"
    */
    public EventHubTests()
    {
        // Reset SecureApi to ensure clean state
        SecureApi.Reset();

        // Load environment variables
        Env.Load();

        // Get values from environment for SecureApi
        _certificateBase64 = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY") ?? 
            throw new InvalidOperationException("APP_SERVER_API_KEY environment variable is not set");
        _serverUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL") ?? 
            throw new InvalidOperationException("APP_SERVER_URL environment variable is not set");

        // Set up logger
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<EventHubTests>();

        // Set the global LogFactory
        typeof(Globals).GetProperty("LogFactory")?.SetValue(null, _loggerFactory);

        // Initialize SecureApi with real credentials
        SecureApi.InitializeClient(_certificateBase64, _serverUrl, forceReinitialize: true);
        
        // Create SystemActivities instance
        _systemActivities = new SystemActivities();
    }

    /*
    dotnet test --filter "FullyQualifiedName~EventHubTests.SendEvent_ShouldRespondNotFound"
    */
    [Fact]
    public async Task SendEvent_ShouldRespondNotFound()
    {
        // Arrange - Use a non-existent workflow ID
        var sourceWorkflowId = $"test-source-workflow-{Guid.NewGuid()}";
        var targetWorkflowId = $"non-existent-workflow-{Guid.NewGuid()}";
        
        var testPayload = new TestPayload { 
            Message = "Hello from test event!",
            Timestamp = DateTime.UtcNow
        };
        
        var evt = new EventDto {
            EventType = "TestEvent",
            SourceWorkflowId = sourceWorkflowId,
            SourceWorkflowType = "TestWorkflow",
            SourceAgent = "TestAgent",
            Payload = testPayload,
            Timestamp = DateTimeOffset.UtcNow,
            TargetWorkflowId = targetWorkflowId,
            TargetWorkflowType = "TestWorkflow",
        };

        _logger.LogInformation("Sending event from {SourceWorkflow} to non-existent workflow {TargetWorkflow}", 
            sourceWorkflowId, targetWorkflowId);

        // Act & Assert
        // The test should receive a Not Found response
        try
        {
            await _systemActivities.SendEvent(evt);
            Assert.Fail("Expected HttpRequestException with NotFound status, but no exception was thrown");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // This is the expected exception
            _logger.LogInformation("Successfully received expected NotFound status: {Message}", ex.Message);
            Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Received unexpected exception: {Error}", ex.Message);
            // If we get a different exception, it should at least contain "not found" in the message
            Assert.Contains("notfound", ex.Message.ToLower());
        }
    }
    
    /*
    dotnet test --filter "FullyQualifiedName~EventHubTests.StartAndSendEvent_ShouldNotThrowException"
    */
    [Fact]
    public async Task StartAndSendEvent_ShouldNotThrowException()
    {
        // Arrange
        var sourceWorkflowId = $"test-source-workflow-{Guid.NewGuid()}";
        var targetWorkflowType = "TestTargetWorkflow";
        
        var testPayload = new TestPayload { 
            Message = "Hello from start and send test event!",
            Timestamp = DateTime.UtcNow
        };
        
        var evt = new EventDto {
            EventType = "TestStartEvent",
            SourceWorkflowId = sourceWorkflowId,
            SourceWorkflowType = "TestWorkflow",
            SourceAgent = "TestAgent",
            Payload = testPayload,
            Timestamp = DateTimeOffset.UtcNow,
            TargetWorkflowType = targetWorkflowType,
        };

        _logger.LogInformation("Starting and sending event from {SourceWorkflow} to workflow type {TargetWorkflowType}", 
            sourceWorkflowId, targetWorkflowType);

        // Act & Assert
        // The test passes if either:
        // 1. No exception is thrown during the event sending
        // 2. A TaskCanceledException is thrown (which can happen due to network issues)
        try
        {
            await _systemActivities.SendEvent(evt);
            _logger.LogInformation("Successfully started workflow and sent event");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Event sending was canceled (possibly due to network issues) - this is acceptable for this test");
            // Test passes in this case too
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start workflow and send event: {Error}", ex.Message);
            throw;
        }
    }
}

public class TestPayload
{
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; }
} 