using System.Net;
using System.Net.Http.Json;
using Moq;
using Moq.Protected;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Webhooks.Models;
using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Activations;
using Xunit;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Unit tests for the agent webhook SDK (<see cref="Xians.Lib.Agents.Webhooks.WebhookCollection"/>)
/// and the agent self-info activation status methods on <see cref="XiansAgent"/>.
/// Verifies request targeting, agent/activation auto-resolution, response parsing, tenant header
/// behavior for system-scoped agents, and HTTP status mapping.
///
/// dotnet test --filter "FullyQualifiedName~WebhookCollectionTests"
/// </summary>
[Collection("Sequential")]
public class WebhookCollectionTests : IDisposable
{
    private const string AGENT_NAME = "test-agent";
    private const string ACTIVATION_NAME = "test-activation";
    private const string TENANT_ID = "test-tenant";

    private readonly Mock<HttpMessageHandler> _httpMessageHandler;
    private readonly HttpClient _httpClient;

    public WebhookCollectionTests()
    {
        XiansContext.CleanupForTests();

        _httpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        XiansContext.CleanupForTests();
    }

    // ---- Create ----

    [Fact]
    public async Task CreateAsync_TargetsWebhooksEndpoint_WithAgentAndContextActivation()
    {
        var agent = CreateAgent(systemScoped: false);
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(SampleWebhook()),
            captureRequest: req => captured = req,
            captureBody: body => capturedBody = body);

        XiansContext.SetIdPostfix(ACTIVATION_NAME);
        try
        {
            var result = await agent.Webhooks.CreateAsync(webhookName: "EmailReceived");

            Assert.NotNull(captured);
            Assert.Equal(HttpMethod.Post, captured!.Method);
            Assert.Contains(WorkflowConstants.ApiEndpoints.AgentWebhooks, captured.RequestUri!.AbsoluteUri);
            Assert.NotNull(capturedBody);
            Assert.Contains($"\"agentName\":\"{AGENT_NAME}\"", capturedBody);
            Assert.Contains($"\"activationName\":\"{ACTIVATION_NAME}\"", capturedBody);
            Assert.Contains("\"webhookName\":\"EmailReceived\"", capturedBody);

            Assert.NotNull(result);
            Assert.Equal("wh-1", result.Id);
            Assert.Equal("Integrator Workflow", result.WorkflowName);
            Assert.Equal(30, result.TimeoutInSeconds);
        }
        finally
        {
            XiansContext.ClearIdPostfix();
        }
    }

    [Fact]
    public async Task CreateAsync_WithExplicitActivation_WorksOutsideContext()
    {
        var agent = CreateAgent(systemScoped: false);
        string? capturedBody = null;
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(SampleWebhook()),
            captureBody: body => capturedBody = body);

        var result = await agent.Webhooks.CreateAsync(activationName: "explicit-act");

        Assert.NotNull(result);
        Assert.NotNull(capturedBody);
        Assert.Contains("\"activationName\":\"explicit-act\"", capturedBody);
    }

    [Fact]
    public async Task CreateAsync_NoActivationAvailable_Throws()
    {
        var agent = CreateAgent(systemScoped: false);
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(SampleWebhook()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.Webhooks.CreateAsync());
    }

    [Fact]
    public async Task CreateAsync_InvalidTimeout_ThrowsArgumentException()
    {
        var agent = CreateAgent(systemScoped: false);
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(SampleWebhook()));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            agent.Webhooks.CreateAsync(activationName: ACTIVATION_NAME, timeoutSeconds: 0));
    }

    [Fact]
    public async Task CreateAsync_ServerError_ThrowsHttpRequestException()
    {
        var agent = CreateAgent(systemScoped: false);
        SetupResponse(HttpStatusCode.BadRequest, new StringContent("{\"error\":\"nope\"}"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            agent.Webhooks.CreateAsync(activationName: ACTIVATION_NAME));
    }

    // ---- List ----

    [Fact]
    public async Task ListAsync_UnwrapsEnvelope_AndScopesToAgent()
    {
        var agent = CreateAgent(systemScoped: false);
        HttpRequestMessage? captured = null;
        var envelope = new { webhooks = new[] { SampleWebhook() } };
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(envelope), captureRequest: req => captured = req);

        var result = await agent.Webhooks.ListAsync();

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Get, captured!.Method);
        var uri = captured.RequestUri!.AbsoluteUri;
        Assert.Contains(WorkflowConstants.ApiEndpoints.AgentWebhooks, uri);
        Assert.Contains($"agentName={Uri.EscapeDataString(AGENT_NAME)}", uri);
        Assert.Single(result);
        Assert.Equal("wh-1", result[0].Id);
    }

    [Fact]
    public async Task ListAsync_InContext_FiltersByActivation()
    {
        var agent = CreateAgent(systemScoped: false);
        HttpRequestMessage? captured = null;
        var envelope = new { webhooks = Array.Empty<WebhookInfo>() };
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(envelope), captureRequest: req => captured = req);

        XiansContext.SetIdPostfix(ACTIVATION_NAME);
        try
        {
            var result = await agent.Webhooks.ListAsync();

            Assert.NotNull(captured);
            Assert.Contains($"activationName={Uri.EscapeDataString(ACTIVATION_NAME)}", captured!.RequestUri!.AbsoluteUri);
            Assert.Empty(result);
        }
        finally
        {
            XiansContext.ClearIdPostfix();
        }
    }

    // ---- Delete ----

    [Fact]
    public async Task DeleteAsync_Ok_ReturnsTrue()
    {
        var agent = CreateAgent(systemScoped: false);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, new StringContent("{\"message\":\"Webhook deleted successfully\"}"),
            captureRequest: req => captured = req);

        var deleted = await agent.Webhooks.DeleteAsync("wh-1");

        Assert.True(deleted);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Delete, captured!.Method);
        Assert.Contains($"{WorkflowConstants.ApiEndpoints.AgentWebhooks}/wh-1", captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        var agent = CreateAgent(systemScoped: false);
        SetupResponse(HttpStatusCode.NotFound, new StringContent(""));

        var deleted = await agent.Webhooks.DeleteAsync("missing");

        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteAsync_EmptyId_ThrowsArgumentException()
    {
        var agent = CreateAgent(systemScoped: false);
        await Assert.ThrowsAsync<ArgumentException>(() => agent.Webhooks.DeleteAsync(""));
    }

    // ---- Tenant header ----

    [Fact]
    public async Task SystemScopedAgent_SendsTenantHeader()
    {
        var agent = CreateAgent(systemScoped: true);
        HttpRequestMessage? captured = null;
        var envelope = new { webhooks = Array.Empty<WebhookInfo>() };
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(envelope), captureRequest: req => captured = req);

        await agent.Webhooks.ListAsync();

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues(WorkflowConstants.Headers.TenantId, out var values));
        Assert.Equal(TENANT_ID, values!.Single());
    }

    [Fact]
    public async Task NonSystemScopedAgent_DoesNotSendTenantHeader()
    {
        var agent = CreateAgent(systemScoped: false);
        HttpRequestMessage? captured = null;
        var envelope = new { webhooks = Array.Empty<WebhookInfo>() };
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(envelope), captureRequest: req => captured = req);

        await agent.Webhooks.ListAsync();

        Assert.NotNull(captured);
        Assert.False(captured!.Headers.Contains(WorkflowConstants.Headers.TenantId));
    }

    // ---- Activation self-info (XiansAgent) ----

    [Fact]
    public async Task GetActivationStatusAsync_Ok_ReturnsActive()
    {
        var agent = CreateAgent(systemScoped: false);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, new StringContent(""), captureRequest: req => captured = req);

        var status = await agent.GetActivationStatusAsync(ACTIVATION_NAME);

        Assert.Equal(ActivationCheckStatus.Active, status);
        Assert.NotNull(captured);
        var uri = captured!.RequestUri!.AbsoluteUri;
        Assert.Contains(WorkflowConstants.ApiEndpoints.ActivationExists, uri);
        Assert.Contains($"agentName={Uri.EscapeDataString(AGENT_NAME)}", uri);
        Assert.Contains($"activationName={Uri.EscapeDataString(ACTIVATION_NAME)}", uri);
    }

    [Fact]
    public async Task GetActivationStatusAsync_NotFound_ReturnsNotFound()
    {
        var agent = CreateAgent(systemScoped: false);
        SetupResponse(HttpStatusCode.NotFound, new StringContent("{\"error\":\"nope\"}"));

        var status = await agent.GetActivationStatusAsync(ACTIVATION_NAME);

        Assert.Equal(ActivationCheckStatus.NotFound, status);
    }

    [Fact]
    public async Task GetActivationStatusAsync_Conflict_ReturnsDeactivated()
    {
        var agent = CreateAgent(systemScoped: false);
        SetupResponse(HttpStatusCode.Conflict, new StringContent("{\"error\":\"deactivated\"}"));

        var status = await agent.GetActivationStatusAsync(ACTIVATION_NAME);

        Assert.Equal(ActivationCheckStatus.Deactivated, status);
    }

    [Fact]
    public async Task ActivationExistsAsync_MapsActiveToTrue_OthersToFalse()
    {
        var agent = CreateAgent(systemScoped: false);

        SetupResponse(HttpStatusCode.OK, new StringContent(""));
        Assert.True(await agent.ActivationExistsAsync(ACTIVATION_NAME));

        // Reset handler with a new not-found response.
        _httpMessageHandler.Reset();
        SetupResponse(HttpStatusCode.NotFound, new StringContent(""));
        Assert.False(await agent.ActivationExistsAsync(ACTIVATION_NAME));
    }

    [Fact]
    public async Task GetActivationStatusAsync_NoActivation_Throws()
    {
        var agent = CreateAgent(systemScoped: false);
        SetupResponse(HttpStatusCode.OK, new StringContent(""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.GetActivationStatusAsync());
    }

    // ---- Helpers ----

    private void SetupResponse(
        HttpStatusCode statusCode,
        HttpContent content,
        Action<HttpRequestMessage>? captureRequest = null,
        Action<string>? captureBody = null)
    {
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                captureRequest?.Invoke(req);
                if (captureBody != null && req.Content != null)
                {
                    captureBody(req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult());
                }
            })
            .ReturnsAsync(new HttpResponseMessage(statusCode) { Content = content });
    }

    private XiansAgent CreateAgent(bool systemScoped)
    {
        var mockHttpService = new Mock<IHttpClientService>();
        mockHttpService.Setup(x => x.Client).Returns(_httpClient);
        mockHttpService.Setup(x => x.GetHealthyClientAsync()).ReturnsAsync(_httpClient);

        var mockTemporalService = new Mock<ITemporalClientService>();
        mockTemporalService.Setup(x => x.IsConnectionHealthy()).Returns(true);

        var options = new XiansOptions
        {
            ApiKey = Xians.Lib.Tests.TestUtilities.TestCertificateGenerator.GenerateTestCertificateBase64(TENANT_ID, "test-user"),
            ServerUrl = "http://localhost"
        };

        var agent = new XiansAgent(
            AGENT_NAME,
            systemScoped,
            null, // description
            null, // summary
            null, // version
            null, // author
            null, // category
            null, // prompts
            null, // uploader
            mockTemporalService.Object,
            mockHttpService.Object,
            options,
            null); // cacheService

        return agent;
    }

    private static WebhookInfo SampleWebhook()
    {
        return new WebhookInfo
        {
            Id = "wh-1",
            Name = "Webhook-EmailReceived-test-activation",
            AgentName = AGENT_NAME,
            ActivationName = ACTIVATION_NAME,
            WorkflowId = $"{TENANT_ID}:{AGENT_NAME}:Integrator Workflow:{ACTIVATION_NAME}",
            WebhookUrl = "/api/user/webhooks/builtin?apikeyId=abc&agentName=test-agent",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            Configuration = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["workflowName"] = System.Text.Json.JsonSerializer.SerializeToElement("Integrator Workflow"),
                ["webhookName"] = System.Text.Json.JsonSerializer.SerializeToElement("EmailReceived"),
                ["participantId"] = System.Text.Json.JsonSerializer.SerializeToElement("webhook"),
                ["timeoutInSeconds"] = System.Text.Json.JsonSerializer.SerializeToElement(30)
            }
        };
    }
}
