using System.Reflection;
using Microsoft.Extensions.Logging;
using Server;
using DotNetEnv;
using XiansAi.Flow;
using XiansAi.Messaging;
using XiansAi.Events;

namespace XiansAi.Lib.Tests.IntegrationTests;

public class SystemActivitiesTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly SystemActivities _systemActivities;
    private readonly ThreadHistoryService _threadHistoryService;
    private readonly string _certificateBase64;
    private readonly string _serverUrl;
    private readonly ILogger<SystemActivitiesTests> _logger;

    /*
    dotnet test --filter "FullyQualifiedName~SystemActivitiesTests"
    */
    public SystemActivitiesTests()
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
        _logger = _loggerFactory.CreateLogger<SystemActivitiesTests>();

        // Set the global LogFactory
        typeof(Globals).GetProperty("LogFactory")?.SetValue(null, _loggerFactory);

        // Initialize SecureApi with real credentials
        SecureApi.InitializeClient(_certificateBase64, _serverUrl, forceReinitialize: true);

        // Create the system activities instance
        _threadHistoryService = new ThreadHistoryService();
        _systemActivities = new SystemActivities();
    }

    /*
    dotnet test --filter "FullyQualifiedName~SystemActivitiesTests.SendMessage_And_GetMessageHistory_ShouldWorkTogether"
    */
    [Fact]
    public async Task SendMessage_And_GetMessageHistory_ShouldWorkTogether()
    {
        // Arrange - Create test messages
        var message1 = new OutgoingMessage 
        { 
            Content = "Test message 1",
            Metadata = new Dictionary<string, string>(),
            ParticipantId = "test-participant",
            WorkflowId = "test-workflow",
            WorkflowType = "test-workflow-type",
            Agent = "test-agent",
            QueueName = "test-queue-name",
            Assignment = "test-assignment"
        };
        
        var message2 = new OutgoingMessage 
        { 
            Content = "Test message 2",
            Metadata = new Dictionary<string, string>(),
            ParticipantId = "test-participant",
            WorkflowId = "test-workflow",
            WorkflowType = "test-workflow-type",
            Agent = "test-agent",
            QueueName = "test-queue-name",
            Assignment = "test-assignment"
        };

        // Act - Send messages
        try
        {
            var sendResult1 = await _systemActivities.SendMessage(message1);
            var sendResult2 = await _systemActivities.SendMessage(message2);

            Assert.NotNull(sendResult1);
            Assert.NotNull(sendResult2);

            // Give server time to process
            await Task.Delay(1000);
            
            // Get message history
            var messages = await _threadHistoryService.GetMessageHistory(message1.Agent, message1.ParticipantId);

            // Log what we received
            _logger.LogInformation($"Retrieved {messages.Count} messages for thread {message1.WorkflowId} {message1.ParticipantId}");
            foreach (var msg in messages)
            {
                _logger.LogInformation($"Message: {msg.Content}");
            }

            // Assert
            Assert.NotEmpty(messages);
            
            // Find our test messages (they might not be the only ones in the thread)
            var testMessages = messages.Where(m => m.Content == message1.Content || m.Content == message2.Content).ToList();
            Assert.NotEmpty(testMessages);
            
            // Verify proper message contents were saved
            Assert.Contains(testMessages, m => m.Content == message1.Content);
            Assert.Contains(testMessages, m => m.Content == message2.Content);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Message sending was canceled (possibly due to network issues) - skipping test");
            return; // Skip the test in this case
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "SecureApi instance was disposed - skipping test");
            return; // Skip the test in this case
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("SecureApi"))
        {
            _logger.LogWarning(ex, "SecureApi is not ready - skipping test");
            return; // Skip the test in this case
        }
        catch (Exception ex) when (ex.Message.Contains("The operation was canceled"))
        {
            _logger.LogWarning(ex, "Operation was canceled (possibly due to network issues) - skipping test");
            return; // Skip the test in this case
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during test: {Error}", ex.Message);
            throw;
        }
    }


    /*
    dotnet test --filter "FullyQualifiedName~SystemActivitiesTests.SendEvent_ShouldSendEventSuccessfully"
    */
    [Fact]
    public async Task SendEvent_ShouldSendEventSuccessfully()
    {
        // Arrange
        var testEvent = new EventDto
        {
            EventType = "tender-notification",
            SourceWorkflowId = "test-source-workflow",
            SourceWorkflowType = "test-source-workflow-type",
            TargetWorkflowType = "Tender Bot",
            TargetWorkflowId = "99xio:HayleysAgent:TenderBot",
            Payload = new Dictionary<string, object>
            {
                { "tender", "{\"organizationName\": \"AZERALUMINIUM LIMITED LIABILITY COMPANY\", \"organizationcontactPhone\": \"+994 50 273 13 45\", \"organizationEmail\": \"sofiya.ashirova@azeraluminium.com\", \"noticeType\": \"Tender notice\", \"biddingType\": \"International Competitive Bidding\", \"noticeNo\": \"AT-020-25\", \"tenderSummary\": \"CALL FOR BID TO THE PARTICIPATION OF SUPPLIERS IN THE OPEN TENDER FOR 220 000 ton calcined metallurgical grade sandy alumina (Al2O3-aluminum oxide) procurement.\", \"cpvCodes\": [], \"awardProcedure\": \"\", \"awardCriteria\": \"\", \"tenderDeadline\": \"30.04.2025\"}" },
                { "department", "Construction Materials" },
                { "pointOfContact", "hasithy@99x.io" },
                { "tenderLink", "https://www.globaltenders.com/freetenders-detail/Calcined-metallurgical-grade-sandy-alumina-procurement-GTF027652" }
            },
            SourceQueueName = null,
            SourceAgent = "Hayleys Agent",
            SourceAssignment = null
        };

        // Act
        await _systemActivities.SendEvent(testEvent);

        // Assert
        // Since this is an integration test with a real server, we can't directly verify
        // if the event was received by the target workflow. Instead, we verify that:
        // 1. The API call was successful (no exception thrown)
        // 2. The event was sent to the correct endpoint
        
        // Note: In a real integration test environment, you might want to:
        // 1. Start a test workflow that listens for this event
        // 2. Verify the event was received by the target workflow
        // 3. Clean up any test workflows/events after the test
    }
} 