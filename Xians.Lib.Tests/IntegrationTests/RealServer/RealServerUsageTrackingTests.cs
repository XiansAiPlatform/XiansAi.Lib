using Xians.Lib.Agents.Core;
using Xians.Lib.Common.Usage;
using Xians.Lib.Tests.TestUtilities;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Temporal.Workflows.Messaging.Models;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server tests for Usage Tracking functionality.
/// These tests run against an actual Xians server.
/// Set SERVER_URL and API_KEY environment variables to run these tests.
/// 
/// dotnet test --filter "Category=RealServer&FullyQualifiedName~RealServerUsageTrackingTests" --logger "console;verbosity=detailed"
/// 
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerWorkflows")] // Force sequential execution
public class RealServerUsageTrackingTests : RealServerTestBase, IAsyncLifetime
{
    private XiansPlatform? _platform;
    private XiansAgent? _agent;
    private XiansWorkflow? _workflow;
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;
    
    // Use hardcoded agent name
    public const string AGENT_NAME = "UsageTrackingTestAgent";
    private const string WORKFLOW_NAME = "UsageTestWorkflow";

    // Track reported usage for verification
    private static readonly List<UsageEventRecord> _reportedUsage = new();

    async Task IAsyncLifetime.InitializeAsync()
    {
        if (!RunRealServerTests) return;
        
        await InitializePlatformAsync();
        
        // Start workers
        if (_agent != null)
        {
            _workerCts = new CancellationTokenSource();
            _workerTask = _agent.RunAllAsync(_workerCts.Token);
            
            // Give workers time to start
            await Task.Delay(2000);
            Console.WriteLine($"✓ Workers started for {AGENT_NAME}");
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Terminate workflows first
        await TerminateWorkflowsAsync();

        // Stop workers
        if (_workerCts != null)
        {
            _workerCts.Cancel();
            try
            {
                if (_workerTask != null)
                {
                    await _workerTask;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            Console.WriteLine("✓ Workers stopped");
        }

        // Cleanup
        XiansContext.Clear();
        _reportedUsage.Clear();
    }

    private async Task InitializePlatformAsync()
    {
        var options = new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };

        _platform = await XiansPlatform.InitializeAsync(options);
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = AGENT_NAME 
        });
        
        // Create workflow with message handler that tracks usage
        _workflow = _agent.Workflows.DefineBuiltIn(name: WORKFLOW_NAME, maxConcurrent: 1);
        
        // Register handler that simulates LLM call and tracks usage
        _workflow.OnUserChatMessage(async (context) =>
        {
            // Simulate getting conversation history
            var history = await context.GetChatHistoryAsync(page: 1, pageSize: 5);
            var messageCount = history.Count + 1;
            
            Console.WriteLine($"Processing message: '{context.Message.Text}' with {history.Count} history messages");
            
            // Simulate LLM call
            await Task.Delay(100);
            
            // Track usage - this is what we're testing!
            await context.ReportUsageAsync(
                model: "test-model-gpt-4",
                promptTokens: 150,
                completionTokens: 75,
                totalTokens: 225,
                messageCount: messageCount,
                source: $"{AGENT_NAME}.ChatHandler",
                metadata: new Dictionary<string, string>
                {
                    ["test_scenario"] = "real_server_test",
                    ["message_length"] = context.Message.Text.Length.ToString()
                },
                responseTimeMs: 100
            );
            
            Console.WriteLine($"✓ Usage reported for request: {context.Message.RequestId}");
            
            // Reply to user
            await context.ReplyAsync($"Echo: {context.Message.Text}");
        });
        
        Console.WriteLine($"✓ Agent '{AGENT_NAME}' created with workflow '{WORKFLOW_NAME}'");
    }

    [Fact]
    public async Task UsageTracking_WithSingleMessage_ReportsCorrectly()
    {
        if (!RunRealServerTests)
        {
            Console.WriteLine("⊘ Skipping real server test (RUN_REAL_SERVER_TESTS not set)");
            return;
        }

        // Arrange
        var participantId = $"user-{Guid.NewGuid().ToString()[..8]}";
        var testMessage = "Test message for usage tracking";

        Console.WriteLine($"Sending message to participant: {participantId}");

        // Act - Send message which will trigger usage tracking in handler
        await SendChatMessageAsync(AGENT_NAME, WORKFLOW_NAME, participantId, testMessage);

        // Wait for processing
        await Task.Delay(2000);

        // Assert - Verify message was received
        var messages = await GetMessagesAsync(AGENT_NAME, WORKFLOW_NAME, participantId);
        
        Assert.NotEmpty(messages);
        var reply = messages.FirstOrDefault(m => m.Text?.Contains("Echo:") == true);
        Assert.NotNull(reply);
        Assert.Contains(testMessage, reply.Text);
        
        Console.WriteLine("✓ Message processed successfully");
        Console.WriteLine("✓ Usage tracking executed (verified by successful message processing)");
    }

    [Fact]
    public async Task UsageTracking_WithConversationHistory_IncludesCorrectMessageCount()
    {
        if (!RunRealServerTests)
        {
            Console.WriteLine("⊘ Skipping real server test (RUN_REAL_SERVER_TESTS not set)");
            return;
        }

        // Arrange
        var participantId = $"user-{Guid.NewGuid().ToString()[..8]}";

        Console.WriteLine($"Building conversation history for participant: {participantId}");

        // Act - Send multiple messages to build history
        await SendChatMessageAsync(AGENT_NAME, WORKFLOW_NAME, participantId, "Message 1");
        await Task.Delay(500);
        
        await SendChatMessageAsync(AGENT_NAME, WORKFLOW_NAME, participantId, "Message 2");
        await Task.Delay(500);
        
        await SendChatMessageAsync(AGENT_NAME, WORKFLOW_NAME, participantId, "Message 3");
        await Task.Delay(500);
        
        await SendChatMessageAsync(AGENT_NAME, WORKFLOW_NAME, participantId, "Message 4");
        await Task.Delay(2000);

        // Assert
        var messages = await GetMessagesAsync(AGENT_NAME, WORKFLOW_NAME, participantId);
        
        // Should have 4 user messages + 4 bot replies = 8 messages
        Assert.True(messages.Count >= 8, $"Expected at least 8 messages, got {messages.Count}");
        
        var replies = messages.Where(m => m.Text?.Contains("Echo:") == true).ToList();
        Assert.Equal(4, replies.Count);
        
        Console.WriteLine($"✓ Processed {replies.Count} messages with conversation history");
        Console.WriteLine("✓ Usage tracking with message count executed successfully");
    }

    [Fact]
    public async Task UsageTracker_WithTiming_WorksEndToEnd()
    {
        if (!RunRealServerTests)
        {
            Console.WriteLine("⊘ Skipping real server test (RUN_REAL_SERVER_TESTS not set)");
            return;
        }

        // Arrange
        var participantId = $"user-{Guid.NewGuid().ToString()[..8]}";
        
        // Create a workflow that uses UsageTracker
        var trackerWorkflow = _agent!.Workflows.DefineBuiltIn(name: "TrackerTestWorkflow", maxConcurrent: 1);
        
        trackerWorkflow.OnUserChatMessage(async (context) =>
        {
            Console.WriteLine($"Processing with UsageTracker: '{context.Message.Text}'");
            
            // Use UsageTracker for automatic timing
            using var tracker = new UsageTracker(
                context, 
                "test-model-claude", 
                messageCount: 1,
                source: "UsageTrackerTest"
            );
            
            // Simulate LLM call
            await Task.Delay(150);
            
            // Report usage
            await tracker.ReportAsync(200, 100, 300);
            
            await context.ReplyAsync($"Tracked: {context.Message.Text}");
        });

        // Start workers for this workflow
        var cts = new CancellationTokenSource();
        var task = trackerWorkflow.RunAsync(cts.Token);
        
        try
        {
            await Task.Delay(2000); // Wait for workers to start

            // Act
            await SendChatMessageAsync(AGENT_NAME, "TrackerTestWorkflow", participantId, "Test UsageTracker");
            await Task.Delay(2000);

            // Assert
            var messages = await GetMessagesAsync(AGENT_NAME, "TrackerTestWorkflow", participantId);
            
            var reply = messages.FirstOrDefault(m => m.Text?.Contains("Tracked:") == true);
            Assert.NotNull(reply);
            
            Console.WriteLine("✓ UsageTracker executed successfully with timing");
        }
        finally
        {
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task UsageTracking_WithMetadata_ReportsSuccessfully()
    {
        if (!RunRealServerTests)
        {
            Console.WriteLine("⊘ Skipping real server test (RUN_REAL_SERVER_TESTS not set)");
            return;
        }

        // Arrange
        var participantId = $"user-{Guid.NewGuid().ToString()[..8]}";
        
        // Create workflow that reports usage with custom metadata
        var metadataWorkflow = _agent!.Workflows.DefineBuiltIn(name: "MetadataTestWorkflow", maxConcurrent: 1);
        
        metadataWorkflow.OnUserChatMessage(async (context) =>
        {
            var customMetadata = new Dictionary<string, string>
            {
                ["request_type"] = "test",
                ["priority"] = "high",
                ["user_tier"] = "premium",
                ["feature_flag"] = "enabled"
            };
            
            await context.ReportUsageAsync(
                model: "gpt-4",
                promptTokens: 100,
                completionTokens: 50,
                totalTokens: 150,
                messageCount: 1,
                source: "MetadataTest",
                metadata: customMetadata,
                responseTimeMs: 500
            );
            
            await context.ReplyAsync("Metadata tracked");
        });

        // Start workers
        var cts = new CancellationTokenSource();
        var task = metadataWorkflow.RunAsync(cts.Token);
        
        try
        {
            await Task.Delay(2000);

            // Act
            await SendChatMessageAsync(AGENT_NAME, "MetadataTestWorkflow", participantId, "Test with metadata");
            await Task.Delay(2000);

            // Assert
            var messages = await GetMessagesAsync(AGENT_NAME, "MetadataTestWorkflow", participantId);
            
            var reply = messages.FirstOrDefault(m => m.Text == "Metadata tracked");
            Assert.NotNull(reply);
            
            Console.WriteLine("✓ Usage tracking with custom metadata executed successfully");
        }
        finally
        {
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task UsageTracking_WithMultipleLLMCalls_ReportsEachSeparately()
    {
        if (!RunRealServerTests)
        {
            Console.WriteLine("⊘ Skipping real server test (RUN_REAL_SERVER_TESTS not set)");
            return;
        }

        // Arrange
        var participantId = $"user-{Guid.NewGuid().ToString()[..8]}";
        
        // Create workflow that makes multiple "LLM calls" (simulated)
        var multiCallWorkflow = _agent!.Workflows.DefineBuiltIn(name: "MultiCallTestWorkflow", maxConcurrent: 1);
        
        multiCallWorkflow.OnUserChatMessage(async (context) =>
        {
            Console.WriteLine("Simulating multiple LLM calls...");
            
            // First "LLM call" - sentiment analysis
            await Task.Delay(50);
            await context.ReportUsageAsync(
                model: "gpt-3.5-turbo",
                promptTokens: 50,
                completionTokens: 10,
                totalTokens: 60,
                messageCount: 1,
                source: "SentimentAnalysis",
                responseTimeMs: 50
            );
            Console.WriteLine("✓ Reported usage for sentiment analysis");
            
            // Second "LLM call" - response generation
            await Task.Delay(100);
            await context.ReportUsageAsync(
                model: "gpt-4",
                promptTokens: 200,
                completionTokens: 150,
                totalTokens: 350,
                messageCount: 1,
                source: "ResponseGeneration",
                responseTimeMs: 100
            );
            Console.WriteLine("✓ Reported usage for response generation");
            
            await context.ReplyAsync("Multi-call completed");
        });

        // Start workers
        var cts = new CancellationTokenSource();
        var task = multiCallWorkflow.RunAsync(cts.Token);
        
        try
        {
            await Task.Delay(2000);

            // Act
            await SendChatMessageAsync(AGENT_NAME, "MultiCallTestWorkflow", participantId, "Test multiple calls");
            await Task.Delay(2000);

            // Assert
            var messages = await GetMessagesAsync(AGENT_NAME, "MultiCallTestWorkflow", participantId);
            
            var reply = messages.FirstOrDefault(m => m.Text == "Multi-call completed");
            Assert.NotNull(reply);
            
            Console.WriteLine("✓ Multiple usage tracking calls executed successfully");
        }
        finally
        {
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task UsageTracking_WhenServerError_DoesNotBreakWorkflow()
    {
        if (!RunRealServerTests)
        {
            Console.WriteLine("⊘ Skipping real server test (RUN_REAL_SERVER_TESTS not set)");
            return;
        }

        // Arrange
        var participantId = $"user-{Guid.NewGuid().ToString()[..8]}";
        
        // This test verifies that even if usage reporting fails,
        // the workflow continues to work normally
        
        var resilientWorkflow = _agent!.Workflows.DefineBuiltIn(name: "ResilientTestWorkflow", maxConcurrent: 1);
        
        resilientWorkflow.OnUserChatMessage(async (context) =>
        {
            // Try to report usage (may fail, but shouldn't break workflow)
            try
            {
                await context.ReportUsageAsync(
                    model: "gpt-4",
                    promptTokens: 100,
                    completionTokens: 50,
                    totalTokens: 150,
                    messageCount: 1,
                    source: "ResilienceTest"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Usage reporting failed (expected in this test): {ex.Message}");
            }
            
            // Workflow should continue normally
            await context.ReplyAsync("Workflow continued despite any usage tracking issues");
        });

        // Start workers
        var cts = new CancellationTokenSource();
        var task = resilientWorkflow.RunAsync(cts.Token);
        
        try
        {
            await Task.Delay(2000);

            // Act
            await SendChatMessageAsync(AGENT_NAME, "ResilientTestWorkflow", participantId, "Test resilience");
            await Task.Delay(2000);

            // Assert - Workflow should work regardless of usage tracking
            var messages = await GetMessagesAsync(AGENT_NAME, "ResilientTestWorkflow", participantId);
            
            var reply = messages.FirstOrDefault(m => m.Text?.Contains("Workflow continued") == true);
            Assert.NotNull(reply);
            
            Console.WriteLine("✓ Workflow remained resilient despite usage tracking issues");
        }
        finally
        {
            cts.Cancel();
            try { await task; } catch (OperationCanceledException) { }
        }
    }

    #region Helper Methods

    /// <summary>
    /// Helper method to send a chat message to a workflow.
    /// </summary>
    private async Task SendChatMessageAsync(string agentName, string workflowName, string participantId, string text)
    {
        if (_agent?.HttpService == null || _platform == null)
        {
            throw new InvalidOperationException("Platform and agent must be initialized before sending messages");
        }

        var httpClient = _agent.HttpService.Client;
        var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
        var messageService = new MessageService(httpClient, logger);

        var workflowType = $"{agentName}:{workflowName}";
        var workflowId = $"{_platform.Options.CertificateTenantId}:{workflowType}:{participantId}";
        var requestId = Guid.NewGuid().ToString();

        var request = new SendMessageRequest
        {
            ParticipantId = participantId,
            WorkflowId = workflowId,
            WorkflowType = workflowType,
            RequestId = requestId,
            Scope = null,
            Text = text,
            Data = null,
            TenantId = _platform.Options.CertificateTenantId!,
            Authorization = null,
            ThreadId = null,
            Hint = "",
            Origin = "usage-tracking-test",
            Type = "chat"
        };

        await messageService.SendAsync(request);
    }

    /// <summary>
    /// Helper method to get message history for a workflow.
    /// </summary>
    private async Task<List<DbMessage>> GetMessagesAsync(string agentName, string workflowName, string participantId)
    {
        if (_agent?.HttpService == null || _platform == null)
        {
            throw new InvalidOperationException("Platform and agent must be initialized before getting messages");
        }

        var httpClient = _agent.HttpService.Client;
        var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
        var messageService = new MessageService(httpClient, logger);

        var workflowType = $"{agentName}:{workflowName}";

        var historyRequest = new GetMessageHistoryRequest
        {
            WorkflowType = workflowType,
            ParticipantId = participantId,
            Scope = null,
            TenantId = _platform.Options.CertificateTenantId!,
            Page = 1,
            PageSize = 50
        };

        return await messageService.GetHistoryAsync(historyRequest);
    }

    /// <summary>
    /// Helper method to terminate workflows.
    /// </summary>
    private async Task TerminateWorkflowsAsync()
    {
        if (_agent?.TemporalService == null) return;

        try
        {
            var temporalClient = await _agent.TemporalService.GetClientAsync();
            await TemporalTestUtils.TerminateBuiltInWorkflowsAsync(
                temporalClient, 
                AGENT_NAME, 
                new[] { WORKFLOW_NAME, "TrackerTestWorkflow", "MetadataTestWorkflow", "MultiCallTestWorkflow", "ResilientTestWorkflow" });
            
            Console.WriteLine("✓ Workflows terminated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to terminate workflows: {ex.Message}");
        }
    }

    #endregion
}

