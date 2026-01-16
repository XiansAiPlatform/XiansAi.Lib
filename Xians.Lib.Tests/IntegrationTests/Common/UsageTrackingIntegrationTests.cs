using Xunit;
using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xians.Lib.Common.Usage;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Http;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Temporal.Workflows.Messaging.Models;
using System.Text.Json;

namespace Xians.Lib.Tests.IntegrationTests.Common;

/// <summary>
/// Integration tests for usage tracking using WireMock.
/// Tests the full HTTP request/response cycle without hitting a real server.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Sequential")]
public class UsageTrackingIntegrationTests : IAsyncLifetime
{
    private WireMockServer? _mockServer;
    private IHttpClientService? _httpService;
    private XiansAgent? _agent;

    public async Task InitializeAsync()
    {
        // Clean up static registries
        XiansContext.CleanupForTests();
        
        // Start WireMock server
        _mockServer = WireMockServer.Start();
        
        // Create HTTP service pointing to mock server
        var config = new ServerConfiguration
        {
            ServerUrl = _mockServer.Url!,
            ApiKey = TestUtilities.TestCertificateGenerator.GenerateTestCertificateBase64("test-tenant", "test-user")
        };
        
        _httpService = new HttpClientService(config, Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<HttpClientService>());
        
        // Create test agent
        var options = new XiansOptions
        {
            ServerUrl = _mockServer.Url!,
            ApiKey = config.ApiKey
        };
        
        _agent = new XiansAgent(
            "integration-test-agent",
            false,
            null, // description
            null, // version
            null, // author
            null, // uploader
            null, // temporalService
            _httpService,
            options,
            null); // cacheService
        
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _httpService?.Dispose();
        _mockServer?.Stop();
        _mockServer?.Dispose();
        XiansContext.CleanupForTests();
        
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ReportAsync_WithValidRecord_SendsCorrectPayload()
    {
        // Arrange
        var receivedRequests = new List<string>();
        
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/usage/report")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithCallback(req =>
                {
                    receivedRequests.Add(req.Body ?? string.Empty);
                    return new WireMock.ResponseMessage();
                }));

        var record = new UsageEventRecord(
            TenantId: "test-tenant",
            UserId: "user123",
            Model: "gpt-4",
            PromptTokens: 150,
            CompletionTokens: 75,
            TotalTokens: 225,
            MessageCount: 5,
            WorkflowId: "workflow-123",
            RequestId: "req-456",
            Source: "IntegrationTest",
            Metadata: new Dictionary<string, string>
            {
                ["test_key"] = "test_value"
            },
            ResponseTimeMs: 1500
        );

        // Act
        await UsageEventsClient.Instance.ReportAsync(record);

        // Wait a bit for async operation
        await Task.Delay(100);

        // Assert
        Assert.Single(receivedRequests);
        var payload = receivedRequests[0];
        
        // Verify JSON structure (camelCase)
        Assert.Contains("\"tenantId\":\"test-tenant\"", payload);
        Assert.Contains("\"userId\":\"user123\"", payload);
        Assert.Contains("\"model\":\"gpt-4\"", payload);
        Assert.Contains("\"promptTokens\":150", payload);
        Assert.Contains("\"completionTokens\":75", payload);
        Assert.Contains("\"totalTokens\":225", payload);
        Assert.Contains("\"messageCount\":5", payload);
        Assert.Contains("\"responseTimeMs\":1500", payload);
        Assert.Contains("\"test_key\"", payload);
    }

    [Fact]
    public async Task ReportAsync_WithTenantId_IncludesTenantHeader()
    {
        // Arrange
        string? capturedTenantHeader = null;
        
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/usage/report")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithCallback(req =>
                {
                    if (req.Headers != null && req.Headers.TryGetValue("X-Tenant-Id", out var values))
                    {
                        capturedTenantHeader = values.FirstOrDefault();
                    }
                    return new WireMock.ResponseMessage();
                }));

        var record = new UsageEventRecord(
            TenantId: "my-special-tenant",
            UserId: "user123",
            Model: "gpt-4",
            PromptTokens: 100,
            CompletionTokens: 50,
            TotalTokens: 150,
            MessageCount: 1,
            WorkflowId: "workflow-1",
            RequestId: "req-1",
            Source: "Test"
        );

        // Act
        await UsageEventsClient.Instance.ReportAsync(record);
        await Task.Delay(100);

        // Assert
        Assert.Equal("my-special-tenant", capturedTenantHeader);
    }

    [Fact]
    public async Task ReportAsync_WhenServerReturns500_DoesNotThrow()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/usage/report")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBody("Internal Server Error"));

        var record = new UsageEventRecord(
            TenantId: "test-tenant",
            UserId: "user123",
            Model: "gpt-4",
            PromptTokens: 100,
            CompletionTokens: 50,
            TotalTokens: 150,
            MessageCount: 1,
            WorkflowId: null,
            RequestId: null,
            Source: "Test"
        );

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(async () =>
        {
            await UsageEventsClient.Instance.ReportAsync(record);
            await Task.Delay(100);
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task ReportAsync_WhenServerReturns400_DoesNotThrow()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/usage/report")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithBody("{\"error\": \"Invalid request\"}"));

        var record = new UsageEventRecord(
            TenantId: "test-tenant",
            UserId: "user123",
            Model: "gpt-4",
            PromptTokens: 100,
            CompletionTokens: 50,
            TotalTokens: 150,
            MessageCount: 1,
            WorkflowId: null,
            RequestId: null,
            Source: "Test"
        );

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () =>
        {
            await UsageEventsClient.Instance.ReportAsync(record);
            await Task.Delay(100);
        });

        Assert.Null(exception);
    }

    [Fact]
    public async Task UsageTracker_ReportsWithTiming()
    {
        // Arrange
        var receivedRequests = new List<string>();
        
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/usage/report")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithCallback(req =>
                {
                    receivedRequests.Add(req.Body ?? string.Empty);
                    return new WireMock.ResponseMessage();
                }));

        var context = CreateTestMessageContext();

        // Act
        using (var tracker = new UsageTracker(context, "gpt-4", messageCount: 1))
        {
            await Task.Delay(50); // Simulate LLM call
            await tracker.ReportAsync(100, 50);
        }

        await Task.Delay(100);

        // Assert
        Assert.Single(receivedRequests);
        var payload = receivedRequests[0];
        
        Assert.Contains("\"responseTimeMs\":", payload);
        
        // Parse to verify responseTimeMs is present and > 0
        var doc = JsonDocument.Parse(payload);
        var responseTimeMs = doc.RootElement.GetProperty("responseTimeMs").GetInt64();
        Assert.True(responseTimeMs >= 50, $"Expected responseTimeMs >= 50, got {responseTimeMs}");
    }

    [Fact]
    public async Task ExtensionMethod_ReportUsageAsync_SendsCorrectData()
    {
        // Arrange
        var receivedRequests = new List<string>();
        
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/usage/report")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithCallback(req =>
                {
                    receivedRequests.Add(req.Body ?? string.Empty);
                    return new WireMock.ResponseMessage();
                }));

        var context = CreateTestMessageContext();

        // Act
        await context.ReportUsageAsync(
            model: "gpt-3.5-turbo",
            promptTokens: 200,
            completionTokens: 100,
            totalTokens: 300,
            messageCount: 10,
            source: "ExtensionMethodTest",
            metadata: new Dictionary<string, string>
            {
                ["test"] = "value"
            },
            responseTimeMs: 2000
        );

        await Task.Delay(100);

        // Assert
        Assert.Single(receivedRequests);
        var payload = receivedRequests[0];
        
        Assert.Contains("\"model\":\"gpt-3.5-turbo\"", payload);
        Assert.Contains("\"promptTokens\":200", payload);
        Assert.Contains("\"completionTokens\":100", payload);
        Assert.Contains("\"totalTokens\":300", payload);
        Assert.Contains("\"messageCount\":10", payload);
        Assert.Contains("\"source\":\"ExtensionMethodTest\"", payload);
        Assert.Contains("\"responseTimeMs\":2000", payload);
        Assert.Contains("\"test\"", payload);
    }

    [Fact]
    public async Task MultipleReports_AllGetSentSuccessfully()
    {
        // Arrange
        var receivedRequests = new List<string>();
        
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/usage/report")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithCallback(req =>
                {
                    receivedRequests.Add(req.Body ?? string.Empty);
                    return new WireMock.ResponseMessage();
                }));

        var context = CreateTestMessageContext();

        // Act - Send multiple reports
        await context.ReportUsageAsync("gpt-4", 100, 50, 150, messageCount: 1);
        await context.ReportUsageAsync("gpt-4", 200, 100, 300, messageCount: 5);
        await context.ReportUsageAsync("claude-3", 150, 75, 225, messageCount: 3);

        await Task.Delay(200);

        // Assert
        Assert.Equal(3, receivedRequests.Count);
        
        Assert.Contains("\"promptTokens\":100", receivedRequests[0]);
        Assert.Contains("\"promptTokens\":200", receivedRequests[1]);
        Assert.Contains("\"model\":\"claude-3\"", receivedRequests[2]);
    }

    [Fact]
    public async Task UsageTracker_WithMetadata_IncludesInPayload()
    {
        // Arrange
        var receivedRequests = new List<string>();
        
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/agent/usage/report")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithCallback(req =>
                {
                    receivedRequests.Add(req.Body ?? string.Empty);
                    return new WireMock.ResponseMessage();
                }));

        var context = CreateTestMessageContext();
        
        var metadata = new Dictionary<string, string>
        {
            ["temperature"] = "0.7",
            ["max_tokens"] = "2000",
            ["stream"] = "true"
        };

        // Act
        using (var tracker = new UsageTracker(context, "gpt-4", messageCount: 1, source: "TestSource", metadata: metadata))
        {
            await tracker.ReportAsync(100, 50);
        }

        await Task.Delay(100);

        // Assert
        Assert.Single(receivedRequests);
        var payload = receivedRequests[0];
        
        Assert.Contains("\"temperature\"", payload);
        Assert.Contains("\"max_tokens\"", payload);
        Assert.Contains("\"stream\"", payload);
    }

    // Helper methods

    private UserMessageContext CreateTestMessageContext()
    {
        return new TestUserMessageContext(
            "test message",
            "user123",
            "req-123",
            "default",
            null,
            null,
            "test-tenant",
            null,
            null,
            null
        );
    }

    private class TestUserMessageContext : UserMessageContext
    {
        public TestUserMessageContext(
            string text,
            string participantId,
            string requestId,
            string? scope,
            string? hint,
            object? data,
            string tenantId,
            string? authorization,
            string? threadId,
            Dictionary<string, string>? metadata)
            : base(text, participantId, requestId, scope, hint, data, tenantId, authorization, threadId, metadata)
        {
        }

        public override Task ReplyAsync(string response)
        {
            return Task.CompletedTask;
        }

        public override Task<List<DbMessage>> GetChatHistoryAsync(int page = 1, int pageSize = 10)
        {
            return Task.FromResult(new List<DbMessage>());
        }

        public override Task<string?> GetLastHintAsync()
        {
            return Task.FromResult<string?>(null);
        }
    }
}

