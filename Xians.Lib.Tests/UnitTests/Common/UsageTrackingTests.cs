using Xunit;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xians.Lib.Agents.Metrics.Models;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Http;
using Xians.Lib.Temporal.Workflows.Messaging.Models;
using Xians.Lib.Tests.TestUtilities;

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
            false, // systemScoped
            null, // description
            null, // summary
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
    public async Task ReportAsync_WithValidRequest_SendsToCorrectEndpoint()
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

        var request = new UsageReportRequest
        {
            TenantId = "test-tenant",
            ParticipantId = "test-user",
            Model = "gpt-4",
            WorkflowId = "test-workflow",
            RequestId = "test-request",
            WorkflowType = "TestAgent",
            Metrics = new List<MetricValue>
            {
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.PromptTokens, Value = 100.0, Unit = "tokens" },
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.CompletionTokens, Value = 50.0, Unit = "tokens" },
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.TotalTokens, Value = 150.0, Unit = "tokens" }
            }
        };

        // Act
        await _agent.Metrics.ReportAsync(request);

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

        var request = new UsageReportRequest
        {
            TenantId = "my-tenant",
            ParticipantId = "user123",
            Model = "gpt-4",
            WorkflowId = "workflow1",
            RequestId = "req1",
            WorkflowType = "Test",
            Metrics = new List<MetricValue>
            {
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.TotalTokens, Value = 150.0, Unit = "tokens" }
            }
        };

        // Act
        await _agent.Metrics.ReportAsync(request);

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

        var request = new UsageReportRequest
        {
            TenantId = "test-tenant",
            ParticipantId = "user123",
            Model = "gpt-4",
            WorkflowId = "workflow1",
            RequestId = "req1",
            WorkflowType = "TestSource",
            Metadata = metadata,
            Metrics = new List<MetricValue>
            {
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.PromptTokens, Value = 100.0, Unit = "tokens" },
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.CompletionTokens, Value = 50.0, Unit = "tokens" },
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.TotalTokens, Value = 150.0, Unit = "tokens" },
                new MetricValue { Category = MetricCategories.Activity, Type = MetricTypes.MessageCount, Value = 5.0, Unit = "count" },
                new MetricValue { Category = MetricCategories.Performance, Type = MetricTypes.ResponseTimeMs, Value = 1234.0, Unit = "ms" }
            }
        };

        // Act
        await _agent.Metrics.ReportAsync(request);

        // Assert
        Assert.NotNull(capturedJson);
        
        // Verify camelCase serialization
        Assert.Contains("\"tenantId\":", capturedJson);
        Assert.Contains("\"participantId\":", capturedJson);
        Assert.Contains("\"model\":", capturedJson);
        Assert.Contains("\"metrics\":", capturedJson);
        Assert.Contains("\"gpt-4\"", capturedJson);
        Assert.Contains("\"TestSource\"", capturedJson);
        Assert.Contains("\"customField\"", capturedJson);
    }

    [Fact]
    public async Task ReportAsync_WhenHttpServiceNotAvailable_DoesNotThrow()
    {
        // Arrange - Clear agent context to simulate no HTTP service
        XiansContext.CleanupForTests();
        
        var request = new UsageReportRequest
        {
            TenantId = "test-tenant",
            ParticipantId = "test-user",
            Model = "gpt-4",
            Metrics = new List<MetricValue>
            {
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.TotalTokens, Value = 150.0, Unit = "tokens" }
            }
        };

        // Act & Assert - Should not throw
        await _agent.Metrics.ReportAsync(request);
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

        var request = new UsageReportRequest
        {
            TenantId = "test-tenant",
            ParticipantId = "test-user",
            Model = "gpt-4",
            WorkflowId = "test-workflow",
            RequestId = "test-request",
            WorkflowType = "Test",
            Metrics = new List<MetricValue>
            {
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.TotalTokens, Value = 150.0, Unit = "tokens" }
            }
        };

        // Act & Assert - Should not throw, just log warning
        await _agent.Metrics.ReportAsync(request);
    }

    [Fact(Skip = "Requires Temporal workflow context - needs fix for XiansContext")]
    public async Task FluentBuilder_MeasuresElapsedTime()
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
        var startTime = DateTime.UtcNow;
        await Task.Delay(100); // Simulate LLM call time
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        
        await XiansContext.Metrics.Track(context)
            .ForModel("gpt-4")
            .WithMetrics(
                (MetricCategories.Tokens, MetricTypes.PromptTokens, 100.0, "tokens"),
                (MetricCategories.Tokens, MetricTypes.CompletionTokens, 50.0, "tokens"),
                (MetricCategories.Performance, MetricTypes.ResponseTimeMs, elapsed, "ms")
            )
            .ReportAsync();

        // Assert - Verify metrics were sent
        Assert.NotNull(capturedJson);
        Assert.Contains("\"metrics\":", capturedJson);
    }

    [Fact(Skip = "Requires Temporal workflow context - needs fix for XiansContext")]
    public async Task FluentBuilder_WithMessageCount_IncludesInReport()
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
        await XiansContext.Metrics.Track(context)
            .ForModel("gpt-4")
            .WithMetrics(
                (MetricCategories.Tokens, MetricTypes.PromptTokens, 100.0, "tokens"),
                (MetricCategories.Tokens, MetricTypes.CompletionTokens, 50.0, "tokens"),
                (MetricCategories.Activity, MetricTypes.MessageCount, 10.0, "count")
            )
            .ReportAsync();

        // Assert
        Assert.NotNull(capturedJson);
        Assert.Contains("\"metrics\":", capturedJson);
    }

    [Fact(Skip = "Requires Temporal workflow context - needs fix for XiansContext")]
    public async Task FluentBuilder_WithMultipleMetrics_IncludesAllInReport()
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
        await XiansContext.Metrics.Track(context)
            .ForModel("gpt-4")
            .WithMetrics(
                (MetricCategories.Tokens, MetricTypes.PromptTokens, 200.0, "tokens"),
                (MetricCategories.Tokens, MetricTypes.CompletionTokens, 100.0, "tokens"),
                (MetricCategories.Tokens, MetricTypes.TotalTokens, 300.0, "tokens"),
                (MetricCategories.Activity, MetricTypes.MessageCount, 15.0, "count")
            )
            .ReportAsync();

        // Assert
        Assert.NotNull(capturedJson);
        Assert.Contains("\"metrics\":", capturedJson);
        Assert.Contains("\"model\":\"gpt-4\"", capturedJson);
    }

    [Fact(Skip = "Requires Temporal workflow context - needs fix for XiansContext")]
    public async Task FluentBuilder_WithMinimalMetrics_Works()
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

        // Act - Minimal usage tracking
        await XiansContext.Metrics.Track(context)
            .ForModel("gpt-4")
            .WithMetric(MetricCategories.Tokens, MetricTypes.TotalTokens, 150.0, "tokens")
            .ReportAsync();

        // Assert
        Assert.NotNull(capturedJson);
        Assert.Contains("\"model\":\"gpt-4\"", capturedJson);
        Assert.Contains("\"metrics\":", capturedJson);
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

    // Helper methods

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

