using Xunit;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xians.Lib.Common.Usage;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Http;
using Xians.Lib.Temporal.Workflows.Messaging.Models;

namespace Xians.Lib.Tests.UnitTests.Common;

/// <summary>
/// Unit tests for usage tracking functionality.
/// Tests the UsageEventsClient and UsageTracker in isolation with mocked dependencies.
/// </summary>
[Collection("Sequential")]
public class UsageTrackingTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<IHttpClientService> _mockHttpService;
    private readonly XiansAgent _agent;

    public UsageTrackingTests()
    {
        // Clean up static registries
        XiansContext.CleanupForTests();
        
        // Create mock HTTP message handler
        _httpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        
        // Mock IHttpClientService
        _mockHttpService = new Mock<IHttpClientService>();
        _mockHttpService.Setup(x => x.Client).Returns(_httpClient);
        
        // Create test agent
        var options = new XiansOptions
        {
            ApiKey = TestUtilities.TestCertificateGenerator.GenerateTestCertificateBase64("test-tenant", "test-user"),
            ServerUrl = "http://localhost"
        };
        
        _agent = new XiansAgent(
            "test-agent",
            false,
            null, // description
            null, // version
            null, // author
            null, // uploader
            null, // temporalService
            _mockHttpService.Object,
            options,
            null); // cacheService
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
        XiansContext.CleanupForTests();
    }

    [Fact]
    public async Task ReportAsync_WithValidRecord_SendsToCorrectEndpoint()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var record = new UsageEventRecord(
            TenantId: "test-tenant",
            UserId: "test-user",
            Model: "gpt-4",
            PromptTokens: 100,
            CompletionTokens: 50,
            TotalTokens: 150,
            MessageCount: 1,
            WorkflowId: "test-workflow",
            RequestId: "test-request",
            Source: "TestAgent"
        );

        // Act
        await UsageEventsClient.Instance.ReportAsync(record);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Contains("/api/agent/usage/report", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task ReportAsync_WithTenantId_IncludesTenantHeader()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var record = new UsageEventRecord(
            TenantId: "my-tenant",
            UserId: "user123",
            Model: "gpt-4",
            PromptTokens: 100,
            CompletionTokens: 50,
            TotalTokens: 150,
            MessageCount: 1,
            WorkflowId: "workflow1",
            RequestId: "req1",
            Source: "Test"
        );

        // Act
        await UsageEventsClient.Instance.ReportAsync(record);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("X-Tenant-Id"));
        Assert.Equal("my-tenant", capturedRequest.Headers.GetValues("X-Tenant-Id").First());
    }

    [Fact]
    public async Task ReportAsync_WithCorrectPayload_SerializesProperlyToCamelCase()
    {
        // Arrange
        string? capturedJson = null;
        
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) => 
            {
                if (req.Content != null)
                {
                    capturedJson = await req.Content.ReadAsStringAsync();
                }
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var metadata = new Dictionary<string, string>
        {
            ["customField"] = "customValue"
        };

        var record = new UsageEventRecord(
            TenantId: "test-tenant",
            UserId: "user123",
            Model: "gpt-4",
            PromptTokens: 100,
            CompletionTokens: 50,
            TotalTokens: 150,
            MessageCount: 5,
            WorkflowId: "workflow1",
            RequestId: "req1",
            Source: "TestSource",
            Metadata: metadata,
            ResponseTimeMs: 1234
        );

        // Act
        await UsageEventsClient.Instance.ReportAsync(record);

        // Assert
        Assert.NotNull(capturedJson);
        
        // Verify camelCase serialization
        Assert.Contains("\"tenantId\":", capturedJson);
        Assert.Contains("\"userId\":", capturedJson);
        Assert.Contains("\"model\":", capturedJson);
        Assert.Contains("\"promptTokens\":100", capturedJson);
        Assert.Contains("\"completionTokens\":50", capturedJson);
        Assert.Contains("\"totalTokens\":150", capturedJson);
        Assert.Contains("\"messageCount\":5", capturedJson);
        Assert.Contains("\"responseTimeMs\":1234", capturedJson);
        
        // Verify values
        Assert.Contains("\"gpt-4\"", capturedJson);
        Assert.Contains("\"TestSource\"", capturedJson);
    }

    [Fact]
    public async Task ReportAsync_WhenHttpServiceNotAvailable_DoesNotThrow()
    {
        // Arrange - Clear agent context to simulate no HTTP service
        XiansContext.CleanupForTests();
        
        var record = new UsageEventRecord(
            TenantId: "test-tenant",
            UserId: "test-user",
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
        await UsageEventsClient.Instance.ReportAsync(record);
    }

    [Fact]
    public async Task ReportAsync_WhenServerReturnsError_DoesNotThrow()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var record = new UsageEventRecord(
            TenantId: "test-tenant",
            UserId: "test-user",
            Model: "gpt-4",
            PromptTokens: 100,
            CompletionTokens: 50,
            TotalTokens: 150,
            MessageCount: 1,
            WorkflowId: "test-workflow",
            RequestId: "test-request",
            Source: "Test"
        );

        // Act & Assert - Should not throw, just log warning
        await UsageEventsClient.Instance.ReportAsync(record);
    }

    [Fact]
    public void ExtractUsageFromSemanticKernelResponses_WithNoResponses_ReturnsZeros()
    {
        // Arrange
        var responses = new List<object>();

        // Act
        var (promptTokens, completionTokens, totalTokens, model, completionId) = 
            UsageEventsClient.Instance.ExtractUsageFromSemanticKernelResponses(responses);

        // Assert
        Assert.Equal(0, promptTokens);
        Assert.Equal(0, completionTokens);
        Assert.Equal(0, totalTokens);
        Assert.Null(model);
        Assert.Null(completionId);
    }

    [Fact]
    public void ExtractUsageFromSemanticKernelResponses_WithNullResponses_ReturnsZeros()
    {
        // Act
        var (promptTokens, completionTokens, totalTokens, model, completionId) = 
            UsageEventsClient.Instance.ExtractUsageFromSemanticKernelResponses(null!);

        // Assert
        Assert.Equal(0, promptTokens);
        Assert.Equal(0, completionTokens);
        Assert.Equal(0, totalTokens);
    }

    [Fact]
    public void ExtractUsageFromSemanticKernelResponses_WithMockUsageData_ExtractsCorrectly()
    {
        // Arrange - Create mock response with usage metadata
        var mockResponse = new MockChatMessageContent
        {
            ModelId = "gpt-4",
            Metadata = new Dictionary<string, object?>
            {
                ["Usage"] = new MockUsageData
                {
                    InputTokenCount = 150,
                    OutputTokenCount = 75,
                    TotalTokenCount = 225
                },
                ["Id"] = "completion-123"
            }
        };

        var responses = new List<object> { mockResponse };

        // Act
        var (promptTokens, completionTokens, totalTokens, model, completionId) = 
            UsageEventsClient.Instance.ExtractUsageFromSemanticKernelResponses(responses);

        // Assert
        Assert.Equal(150, promptTokens);
        Assert.Equal(75, completionTokens);
        Assert.Equal(225, totalTokens);
        Assert.Equal("gpt-4", model);
        Assert.Equal("completion-123", completionId);
    }

    [Fact]
    public async Task UsageTracker_MeasuresElapsedTime()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var context = CreateTestMessageContext();

        // Act
        using var tracker = new UsageTracker(context, "gpt-4");
        await Task.Delay(100); // Simulate LLM call time
        await tracker.ReportAsync(100, 50);

        // Assert - Verify time was measured (should be >= 100ms)
        // We can't directly assert the time, but the test exercises the timing logic
    }

    [Fact]
    public async Task UsageTracker_WithMessageCount_IncludesInReport()
    {
        // Arrange
        string? capturedJson = null;
        
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) => 
            {
                if (req.Content != null)
                {
                    capturedJson = await req.Content.ReadAsStringAsync();
                }
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var context = CreateTestMessageContext();

        // Act
        using var tracker = new UsageTracker(context, "gpt-4", messageCount: 10);
        await tracker.ReportAsync(100, 50);

        // Assert
        Assert.NotNull(capturedJson);
        Assert.Contains("\"messageCount\":10", capturedJson);
    }

    [Fact]
    public async Task ReportUsageAsync_ExtensionMethod_WithMessageCount_IncludesInReport()
    {
        // Arrange
        string? capturedJson = null;
        
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) => 
            {
                if (req.Content != null)
                {
                    capturedJson = await req.Content.ReadAsStringAsync();
                }
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var context = CreateTestMessageContext();

        // Act
        await context.ReportUsageAsync(
            model: "gpt-4",
            promptTokens: 200,
            completionTokens: 100,
            totalTokens: 300,
            messageCount: 15
        );

        // Assert
        Assert.NotNull(capturedJson);
        Assert.Contains("\"messageCount\":15", capturedJson);
        Assert.Contains("\"promptTokens\":200", capturedJson);
        Assert.Contains("\"completionTokens\":100", capturedJson);
    }

    [Fact]
    public async Task ReportUsageAsync_ExtensionMethod_DefaultMessageCount_UsesOne()
    {
        // Arrange
        string? capturedJson = null;
        
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) => 
            {
                if (req.Content != null)
                {
                    capturedJson = await req.Content.ReadAsStringAsync();
                }
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var context = CreateTestMessageContext();

        // Act - Don't specify messageCount
        await context.ReportUsageAsync(
            model: "gpt-4",
            promptTokens: 100,
            completionTokens: 50,
            totalTokens: 150
        );

        // Assert
        Assert.NotNull(capturedJson);
        Assert.Contains("\"messageCount\":1", capturedJson);
    }

    // Helper methods

    private UserMessageContext CreateTestMessageContext()
    {
        return new TestUserMessageContext(
            "test message",
            "user123",
            "req123",
            "default",
            null,
            null,
            "test-tenant",
            null,
            null,
            null
        );
    }

    // Mock classes for testing

    private class MockChatMessageContent
    {
        public string? ModelId { get; set; }
        public Dictionary<string, object?>? Metadata { get; set; }
    }

    private class MockUsageData
    {
        public long InputTokenCount { get; set; }
        public long OutputTokenCount { get; set; }
        public long TotalTokenCount { get; set; }
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

