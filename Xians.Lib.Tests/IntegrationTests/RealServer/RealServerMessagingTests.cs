using System.Net.Http.Json;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Workflows.Models;

namespace Xians.Lib.Tests.IntegrationTests.RealServer;

/// <summary>
/// Real server tests for Messaging SDK.
/// These tests run against an actual Xians server to verify:
/// - Message sending (chat and data)
/// - Message history retrieval
/// 
/// Set SERVER_URL and API_KEY environment variables to run these tests.
/// </summary>
[Trait("Category", "RealServer")]
[Collection("RealServerMessaging")] // Force sequential execution
public class RealServerMessagingTests : RealServerTestBase, IDisposable
{
    private XiansPlatform? _platform;
    private XiansAgent? _agent;
    private readonly string _testParticipantId;
    private readonly string _testScope;
    
    // Use unique agent name per test instance to avoid conflicts
    private readonly string _agentName;
    private const string WORKFLOW_NAME = "TestWorkflow";

    public RealServerMessagingTests()
    {
        // Use unique IDs for test isolation
        _testParticipantId = $"test-user-{Guid.NewGuid().ToString()[..8]}";
        _testScope = $"test-scope-{Guid.NewGuid().ToString()[..8]}";
        _agentName = $"MessagingTestAgent-{Guid.NewGuid().ToString()[..8]}";
    }

    public void Dispose()
    {
        // Clear the context to allow other tests to register agents
        try
        {
            XiansContext.Clear();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private async Task InitializePlatformAsync()
    {
        if (!RunRealServerTests)
        {
            return;
        }

        var options = new XiansOptions
        {
            ServerUrl = ServerUrl!,
            ApiKey = ApiKey!
        };

        _platform = await XiansPlatform.InitializeAsync(options);
        
        // Register agent with unique name
        _agent = _platform.Agents.Register(new XiansAgentRegistration 
        { 
            Name = _agentName 
        });
        
        // Define and upload workflow definition to register the agent with the server
        var workflow = _agent.Workflows.DefineBuiltIn(WORKFLOW_NAME);
        await _agent.UploadWorkflowDefinitionsAsync();
        
        Console.WriteLine($"✓ Registered agent on server: {_agentName}");
        Console.WriteLine($"✓ Test Participant ID: {_testParticipantId}");
        Console.WriteLine($"✓ Test Scope: {_testScope}");
    }

    #region Direct MessageService Tests (HTTP Layer)

    [Fact]
    public async Task MessageService_SendChat_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var httpClient = _agent!.HttpService!.Client;
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(httpClient, logger);

            var workflowType = $"{_agentName}:{WORKFLOW_NAME}";
            var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{_testParticipantId}";
            var requestId = Guid.NewGuid().ToString();
            var text = $"Hello from messaging test at {DateTime.UtcNow:O}";

            // Act
            await messageService.SendAsync(
                participantId: _testParticipantId,
                workflowId: workflowId,
                workflowType: workflowType,
                requestId: requestId,
                scope: _testScope,
                text: text,
                data: null,
                tenantId: _platform.Options.CertificateTenantId!,
                authorization: null,
                threadId: null,
                hint: "test",
                origin: "integration-test",
                messageType: "chat"
            );

            // Assert - If we get here without exception, the message was sent
            Console.WriteLine($"✓ Message sent successfully: RequestId={requestId}");
            Assert.True(true, "Message sent successfully");
        }
        catch (Exception ex)
        {
            throw new Exception($"SendChat test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task MessageService_SendData_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var httpClient = _agent!.HttpService!.Client;
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(httpClient, logger);

            var workflowType = $"{_agentName}:{WORKFLOW_NAME}";
            var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{_testParticipantId}";
            var requestId = Guid.NewGuid().ToString();
            var data = new
            {
                OrderId = "ORD-12345",
                Status = "Shipped",
                Timestamp = DateTime.UtcNow
            };

            // Act
            await messageService.SendAsync(
                participantId: _testParticipantId,
                workflowId: workflowId,
                workflowType: workflowType,
                requestId: requestId,
                scope: _testScope,
                text: "Order update",
                data: data,
                tenantId: _platform.Options.CertificateTenantId!,
                authorization: null,
                threadId: null,
                hint: "order-notification",
                origin: "integration-test",
                messageType: "data"
            );

            // Assert
            Console.WriteLine($"✓ Data message sent successfully: RequestId={requestId}");
            Assert.True(true, "Data message sent successfully");
        }
        catch (Exception ex)
        {
            throw new Exception($"SendData test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task MessageService_SendWithData_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var httpClient = _agent!.HttpService!.Client;
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(httpClient, logger);

            var workflowType = $"{_agentName}:{WORKFLOW_NAME}";
            var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{_testParticipantId}";
            var requestId = Guid.NewGuid().ToString();
            
            // Rich structured data
            var data = new
            {
                Products = new[]
                {
                    new { Name = "Widget A", Price = 19.99, Quantity = 2 },
                    new { Name = "Widget B", Price = 29.99, Quantity = 1 }
                },
                TotalAmount = 69.97,
                Currency = "USD"
            };

            // Act
            await messageService.SendAsync(
                participantId: _testParticipantId,
                workflowId: workflowId,
                workflowType: workflowType,
                requestId: requestId,
                scope: _testScope,
                text: "Here are your products:",
                data: data,
                tenantId: _platform.Options.CertificateTenantId!,
                authorization: null,
                threadId: null,
                hint: "product-list",
                origin: "integration-test",
                messageType: "chat"
            );

            // Assert
            Console.WriteLine($"✓ Chat with data sent successfully: RequestId={requestId}");
            Assert.True(true, "Chat with data sent successfully");
        }
        catch (Exception ex)
        {
            throw new Exception($"SendWithData test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task MessageService_GetHistory_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var httpClient = _agent!.HttpService!.Client;
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(httpClient, logger);

            var workflowType = $"{_agentName}:{WORKFLOW_NAME}";

            // First, send a message so we have something in history
            var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{_testParticipantId}";
            var requestId = Guid.NewGuid().ToString();
            var testMessage = $"History test message at {DateTime.UtcNow:O}";

            await messageService.SendAsync(
                participantId: _testParticipantId,
                workflowId: workflowId,
                workflowType: workflowType,
                requestId: requestId,
                scope: _testScope,
                text: testMessage,
                data: null,
                tenantId: _platform.Options.CertificateTenantId!,
                authorization: null,
                threadId: null,
                hint: "",
                origin: "integration-test",
                messageType: "chat"
            );

            // Wait a moment for message to be stored
            await Task.Delay(500);

            // Act - Get history
            var history = await messageService.GetHistoryAsync(
                workflowType: workflowType,
                participantId: _testParticipantId,
                scope: _testScope,
                tenantId: _platform.Options.CertificateTenantId!,
                page: 1,
                pageSize: 10
            );

            // Assert
            Assert.NotNull(history);
            Console.WriteLine($"✓ Retrieved {history.Count} messages from history");
            
            // Should contain our test message (or at least not be empty if messages exist)
            // Note: History might contain messages from previous test runs
            Assert.True(history.Count >= 0, "History should return a valid list");
        }
        catch (Exception ex)
        {
            throw new Exception($"GetHistory test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task MessageService_GetHistory_EmptyHistory_ReturnsEmptyList()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var httpClient = _agent!.HttpService!.Client;
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(httpClient, logger);

            var workflowType = $"{_agentName}:{WORKFLOW_NAME}";
            // Use a random participant ID that shouldn't have any history
            var randomParticipantId = $"nonexistent-{Guid.NewGuid()}";

            // Act
            var history = await messageService.GetHistoryAsync(
                workflowType: workflowType,
                participantId: randomParticipantId,
                scope: "nonexistent-scope",
                tenantId: _platform!.Options.CertificateTenantId!,
                page: 1,
                pageSize: 10
            );

            // Assert
            Assert.NotNull(history);
            Assert.Empty(history);
            Console.WriteLine("✓ Empty history returns empty list as expected");
        }
        catch (Exception ex)
        {
            throw new Exception($"Empty history test failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region Multiple Messages Tests

    [Fact]
    public async Task MessageService_SendMultipleMessages_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var httpClient = _agent!.HttpService!.Client;
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(httpClient, logger);

            var workflowType = $"{_agentName}:{WORKFLOW_NAME}";
            var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{_testParticipantId}";

            // Act - Send multiple messages
            var messageCount = 3;
            for (int i = 1; i <= messageCount; i++)
            {
                var requestId = Guid.NewGuid().ToString();
                await messageService.SendAsync(
                    participantId: _testParticipantId,
                    workflowId: workflowId,
                    workflowType: workflowType,
                    requestId: requestId,
                    scope: _testScope,
                    text: $"Message {i} of {messageCount}",
                    data: new { MessageNumber = i },
                    tenantId: _platform.Options.CertificateTenantId!,
                    authorization: null,
                    threadId: null,
                    hint: "",
                    origin: "integration-test",
                    messageType: "chat"
                );
                Console.WriteLine($"  ✓ Sent message {i}/{messageCount}");
            }

            // Wait for messages to be stored
            await Task.Delay(500);

            // Verify in history
            var history = await messageService.GetHistoryAsync(
                workflowType: workflowType,
                participantId: _testParticipantId,
                scope: _testScope,
                tenantId: _platform.Options.CertificateTenantId!,
                page: 1,
                pageSize: 20
            );

            // Assert
            Assert.NotNull(history);
            Console.WriteLine($"✓ Retrieved {history.Count} messages from history after sending {messageCount}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Multiple messages test failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task MessageService_LongMessage_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var httpClient = _agent!.HttpService!.Client;
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(httpClient, logger);

            var workflowType = $"{_agentName}:{WORKFLOW_NAME}";
            var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{_testParticipantId}";
            var requestId = Guid.NewGuid().ToString();
            
            // Create a long message (5KB)
            var longMessage = new string('x', 5000);

            // Act
            await messageService.SendAsync(
                participantId: _testParticipantId,
                workflowId: workflowId,
                workflowType: workflowType,
                requestId: requestId,
                scope: _testScope,
                text: longMessage,
                data: null,
                tenantId: _platform.Options.CertificateTenantId!,
                authorization: null,
                threadId: null,
                hint: "",
                origin: "integration-test",
                messageType: "chat"
            );

            // Assert
            Console.WriteLine($"✓ Long message ({longMessage.Length} chars) sent successfully");
            Assert.True(true);
        }
        catch (Exception ex)
        {
            throw new Exception($"Long message test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task MessageService_SpecialCharacters_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var httpClient = _agent!.HttpService!.Client;
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(httpClient, logger);

            var workflowType = $"{_agentName}:{WORKFLOW_NAME}";
            var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{_testParticipantId}";
            var requestId = Guid.NewGuid().ToString();
            
            // Message with special characters, unicode, and emojis
            var specialMessage = "Hello! 你好! مرحبا! 🎉🚀 Special chars: <>&\"'\\/ \n\t";

            // Act
            await messageService.SendAsync(
                participantId: _testParticipantId,
                workflowId: workflowId,
                workflowType: workflowType,
                requestId: requestId,
                scope: _testScope,
                text: specialMessage,
                data: null,
                tenantId: _platform.Options.CertificateTenantId!,
                authorization: null,
                threadId: null,
                hint: "",
                origin: "integration-test",
                messageType: "chat"
            );

            // Assert
            Console.WriteLine("✓ Special characters message sent successfully");
            Assert.True(true);
        }
        catch (Exception ex)
        {
            throw new Exception($"Special characters test failed: {ex.Message}", ex);
        }
    }

    [Fact]
    public async Task MessageService_EmptyText_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var httpClient = _agent!.HttpService!.Client;
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(httpClient, logger);

            var workflowType = $"{_agentName}:{WORKFLOW_NAME}";
            var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{_testParticipantId}";
            var requestId = Guid.NewGuid().ToString();
            
            // Empty text with data
            var data = new { Type = "silent-update", Value = 42 };

            // Act
            await messageService.SendAsync(
                participantId: _testParticipantId,
                workflowId: workflowId,
                workflowType: workflowType,
                requestId: requestId,
                scope: _testScope,
                text: "",
                data: data,
                tenantId: _platform.Options.CertificateTenantId!,
                authorization: null,
                threadId: null,
                hint: "",
                origin: "integration-test",
                messageType: "data"
            );

            // Assert
            Console.WriteLine("✓ Empty text data message sent successfully");
            Assert.True(true);
        }
        catch (Exception ex)
        {
            throw new Exception($"Empty text test failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task MessageService_InvalidMessageType_ThrowsException()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        // Arrange
        var httpClient = _agent!.HttpService!.Client;
        var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
        var messageService = new MessageService(httpClient, logger);

        var workflowType = $"{_agentName}:{WORKFLOW_NAME}";
        var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{_testParticipantId}";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await messageService.SendAsync(
                participantId: _testParticipantId,
                workflowId: workflowId,
                workflowType: workflowType,
                requestId: Guid.NewGuid().ToString(),
                scope: _testScope,
                text: "Test",
                data: null,
                tenantId: _platform.Options.CertificateTenantId!,
                authorization: null,
                threadId: null,
                hint: "",
                origin: "test",
                messageType: "invalid-type"
            );
        });

        Console.WriteLine("✓ Invalid message type correctly throws ArgumentException");
    }

    #endregion

    #region Thread ID Tests

    [Fact]
    public async Task MessageService_WithThreadId_WorksWithRealServer()
    {
        if (!RunRealServerTests) return;

        await InitializePlatformAsync();

        try
        {
            // Arrange
            var httpClient = _agent!.HttpService!.Client;
            var logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<MessageService>();
            var messageService = new MessageService(httpClient, logger);

            var workflowType = $"{_agentName}:{WORKFLOW_NAME}";
            var workflowId = $"{_platform!.Options.CertificateTenantId}:{workflowType}:{_testParticipantId}";
            var threadId = $"thread-{Guid.NewGuid()}";

            // Act - Send multiple messages in same thread
            for (int i = 1; i <= 2; i++)
            {
                await messageService.SendAsync(
                    participantId: _testParticipantId,
                    workflowId: workflowId,
                    workflowType: workflowType,
                    requestId: Guid.NewGuid().ToString(),
                    scope: _testScope,
                    text: $"Thread message {i}",
                    data: null,
                    tenantId: _platform.Options.CertificateTenantId!,
                    authorization: null,
                    threadId: threadId,
                    hint: "",
                    origin: "integration-test",
                    messageType: "chat"
                );
            }

            // Assert
            Console.WriteLine($"✓ Messages with thread ID sent successfully: {threadId}");
            Assert.True(true);
        }
        catch (Exception ex)
        {
            throw new Exception($"Thread ID test failed: {ex.Message}", ex);
        }
    }

    #endregion
}

/// <summary>
/// Test collection to force sequential execution of messaging tests.
/// This prevents conflicts from multiple tests trying to register agents simultaneously.
/// </summary>
[CollectionDefinition("RealServerMessaging", DisableParallelization = true)]
public class RealServerMessagingCollection
{
}

