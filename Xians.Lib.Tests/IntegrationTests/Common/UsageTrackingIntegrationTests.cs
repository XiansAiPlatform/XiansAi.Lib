using WireMock.Server;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xians.Lib.Agents.Metrics.Models;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Http;
using Xians.Lib.Configuration.Models;
using Xians.Lib.Temporal.Workflows.Messaging.Models;
using Xians.Lib.Tests.TestUtilities;

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
            null, // summary
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
    public async Task ReportAsync_WithValidRequest_SendsCorrectPayload()
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

        var request = new UsageReportRequest
        {
            TenantId = "test-tenant",
            ParticipantId = "user123",
            Model = "gpt-4",
            WorkflowId = "workflow-123",
            RequestId = "req-456",
            WorkflowType = "IntegrationTest",
            Metadata = new Dictionary<string, string>
            {
                ["test_key"] = "test_value"
            },
            Metrics = new List<MetricValue>
            {
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.PromptTokens, Value = 150.0, Unit = "tokens" },
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.CompletionTokens, Value = 75.0, Unit = "tokens" },
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.TotalTokens, Value = 225.0, Unit = "tokens" },
                new MetricValue { Category = MetricCategories.Activity, Type = MetricTypes.MessageCount, Value = 5.0, Unit = "count" },
                new MetricValue { Category = MetricCategories.Performance, Type = MetricTypes.ResponseTimeMs, Value = 1500.0, Unit = "ms" }
            }
        };

        // Act
        await _agent!.Metrics.ReportAsync(request);

        // Wait a bit for async operation
        await Task.Delay(100);

        // Assert
        Assert.Single(receivedRequests);
        var payload = receivedRequests[0];
        
        // Verify JSON structure (camelCase)
        Assert.Contains("\"tenantId\":\"test-tenant\"", payload);
        Assert.Contains("\"userId\":\"user123\"", payload);
        Assert.Contains("\"model\":\"gpt-4\"", payload);
        Assert.Contains("\"metrics\":", payload);
        Assert.Contains("\"test_key\"", payload);
    }

    [Fact(Skip = "Requires Temporal workflow context - needs fix for XiansContext")]
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

        var request = new UsageReportRequest
        {
            TenantId = "my-special-tenant",
            ParticipantId = "user123",
            Model = "gpt-4",
            WorkflowId = "workflow-1",
            RequestId = "req-1",
            WorkflowType = "Test",
            Metrics = new List<MetricValue>
            {
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.TotalTokens, Value = 150.0, Unit = "tokens" }
            }
        };

        // Act
        await _agent!.Metrics.ReportAsync(request);
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

        var request = new UsageReportRequest
        {
            TenantId = "test-tenant",
            ParticipantId = "user123",
            Model = "gpt-4",
            Metrics = new List<MetricValue>
            {
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.TotalTokens, Value = 150.0, Unit = "tokens" }
            }
        };

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(async () =>
        {
            await _agent!.Metrics.ReportAsync(request);
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

        var request = new UsageReportRequest
        {
            TenantId = "test-tenant",
            ParticipantId = "user123",
            Model = "gpt-4",
            Metrics = new List<MetricValue>
            {
                new MetricValue { Category = MetricCategories.Tokens, Type = MetricTypes.TotalTokens, Value = 150.0, Unit = "tokens" }
            }
        };

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () =>
        {
            await _agent!.Metrics.ReportAsync(request);
            await Task.Delay(100);
        });

        Assert.Null(exception);
    }

    [Fact(Skip = "Requires Temporal workflow context - needs fix for XiansContext")]
    public async Task FluentBuilder_ReportsWithTiming()
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
        var startTime = DateTime.UtcNow;
        await Task.Delay(50); // Simulate LLM call
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        
        await XiansContext.Metrics.Track(context)
            .ForModel("gpt-4")
            .WithMetrics(
                (MetricCategories.Tokens, MetricTypes.PromptTokens, 100.0, "tokens"),
                (MetricCategories.Tokens, MetricTypes.CompletionTokens, 50.0, "tokens"),
                (MetricCategories.Activity, MetricTypes.MessageCount, 1.0, "count"),
                (MetricCategories.Performance, MetricTypes.ResponseTimeMs, elapsed, "ms")
            )
            .ReportAsync();

        await Task.Delay(100);

        // Assert
        Assert.Single(receivedRequests);
        var payload = receivedRequests[0];
        Assert.Contains("\"metrics\":", payload);
    }

    [Fact(Skip = "Requires Temporal workflow context - needs fix for XiansContext")]
    public async Task FluentBuilder_SendsCorrectData()
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
        await XiansContext.Metrics.Track(context)
            .ForModel("gpt-3.5-turbo")
            .FromSource("ExtensionMethodTest")
            .WithMetrics(
                (MetricCategories.Tokens, MetricTypes.PromptTokens, 200.0, "tokens"),
                (MetricCategories.Tokens, MetricTypes.CompletionTokens, 100.0, "tokens"),
                (MetricCategories.Tokens, MetricTypes.TotalTokens, 300.0, "tokens"),
                (MetricCategories.Activity, MetricTypes.MessageCount, 10.0, "count"),
                (MetricCategories.Performance, MetricTypes.ResponseTimeMs, 2000.0, "ms")
            )
            .WithMetadata("test", "value")
            .ReportAsync();

        await Task.Delay(100);

        // Assert
        Assert.Single(receivedRequests);
        var payload = receivedRequests[0];
        
        Assert.Contains("\"model\":\"gpt-3.5-turbo\"", payload);
        Assert.Contains("\"metrics\":", payload);
        Assert.Contains("\"source\":\"ExtensionMethodTest\"", payload);
        Assert.Contains("\"test\"", payload);
    }

    [Fact(Skip = "Requires Temporal workflow context - needs fix for XiansContext")]
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
        await XiansContext.Metrics.Track(context)
            .ForModel("gpt-4")
            .WithMetric(MetricCategories.Tokens, MetricTypes.TotalTokens, 150.0, "tokens")
            .WithMetric(MetricCategories.Activity, MetricTypes.MessageCount, 1.0, "count")
            .ReportAsync();
            
        await XiansContext.Metrics.Track(context)
            .ForModel("gpt-4")
            .WithMetric(MetricCategories.Tokens, MetricTypes.TotalTokens, 300.0, "tokens")
            .WithMetric(MetricCategories.Activity, MetricTypes.MessageCount, 5.0, "count")
            .ReportAsync();
            
        await XiansContext.Metrics.Track(context)
            .ForModel("claude-3")
            .WithMetric(MetricCategories.Tokens, MetricTypes.TotalTokens, 225.0, "tokens")
            .WithMetric(MetricCategories.Activity, MetricTypes.MessageCount, 3.0, "count")
            .ReportAsync();

        await Task.Delay(200);

        // Assert
        Assert.Equal(3, receivedRequests.Count);
        
        Assert.Contains("\"model\":\"gpt-4\"", receivedRequests[0]);
        Assert.Contains("\"model\":\"gpt-4\"", receivedRequests[1]);
        Assert.Contains("\"model\":\"claude-3\"", receivedRequests[2]);
    }

    [Fact(Skip = "Requires Temporal workflow context - needs fix for XiansContext")]
    public async Task FluentBuilder_WithMetadata_IncludesInPayload()
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
        await XiansContext.Metrics.Track(context)
            .ForModel("gpt-4")
            .FromSource("TestSource")
            .WithMetrics(
                (MetricCategories.Tokens, MetricTypes.PromptTokens, 100.0, "tokens"),
                (MetricCategories.Tokens, MetricTypes.CompletionTokens, 50.0, "tokens"),
                (MetricCategories.Activity, MetricTypes.MessageCount, 1.0, "count")
            )
            .WithMetadata("temperature", "0.7")
            .WithMetadata("max_tokens", "2000")
            .WithMetadata("stream", "true")
            .ReportAsync();

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

