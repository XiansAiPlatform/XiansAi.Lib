using System.Net;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Core.Activations;
using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Activations;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Unit tests for the cross-agent activation inspection and management APIs
/// (<see cref="TenantAgents"/> / <see cref="AgentReference"/> / <see cref="ActivationInfo"/>).
///
/// dotnet test --filter "FullyQualifiedName~AgentReferenceTests"
/// </summary>
[Collection("Sequential")]
public class AgentReferenceTests : IDisposable
{
    private const string OWNER_AGENT_NAME = "owner-agent";
    private const string TARGET_AGENT_NAME = "Fraud Detection Agent";
    private const string ACTIVATION_NAME = "fraud-detection-eu";
    private const string ACTIVATION_ID = "507f1f77bcf86cd799439011";
    private const string TENANT_ID = "test-tenant";

    private readonly Mock<HttpMessageHandler> _httpMessageHandler;
    private readonly HttpClient _httpClient;

    public AgentReferenceTests()
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

    // ---- Agent existence ----

    [Fact]
    public async Task ExistsAsync_Ok_ReturnsTrue()
    {
        var owner = CreateOwner(systemScoped: false);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, "", captureRequest: req => captured = req);

        Assert.True(await owner.Tenant.Agent(TARGET_AGENT_NAME).ExistsAsync());

        Assert.NotNull(captured);
        var uri = captured!.RequestUri!.AbsoluteUri;
        Assert.Contains(WorkflowConstants.ApiEndpoints.AgentExists, uri);
        Assert.Contains($"agentName={Uri.EscapeDataString(TARGET_AGENT_NAME)}", uri);
    }

    [Fact]
    public async Task ExistsAsync_NotFound_ReturnsFalse()
    {
        var owner = CreateOwner(systemScoped: false);
        SetupResponse(HttpStatusCode.NotFound, "{\"error\":\"Agent not found\"}");

        Assert.False(await owner.Tenant.Agent(TARGET_AGENT_NAME).ExistsAsync());
    }

    [Fact]
    public async Task ExistsAsync_BadRequest_ThrowsInvalidOperation()
    {
        var owner = CreateOwner(systemScoped: false);
        SetupResponse(HttpStatusCode.BadRequest, "{\"error\":\"AgentName is required\"}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            owner.Tenant.Agent(TARGET_AGENT_NAME).ExistsAsync());
    }

    [Fact]
    public async Task ExistsAsync_ServerError_ThrowsHttpRequestException()
    {
        var owner = CreateOwner(systemScoped: false);
        SetupResponse(HttpStatusCode.InternalServerError, "boom");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            owner.Tenant.Agent(TARGET_AGENT_NAME).ExistsAsync());
    }

    // ---- Activation status ----

    [Fact]
    public async Task GetActivationStatusAsync_Ok_ReturnsActive()
    {
        var owner = CreateOwner(systemScoped: false);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, "", captureRequest: req => captured = req);

        var status = await owner.Tenant.Agent(TARGET_AGENT_NAME).GetActivationStatusAsync(ACTIVATION_NAME);

        Assert.Equal(ActivationCheckStatus.Active, status);
        Assert.NotNull(captured);
        var uri = captured!.RequestUri!.AbsoluteUri;
        Assert.Contains(WorkflowConstants.ApiEndpoints.ActivationExists, uri);
        Assert.Contains($"agentName={Uri.EscapeDataString(TARGET_AGENT_NAME)}", uri);
        Assert.Contains($"activationName={Uri.EscapeDataString(ACTIVATION_NAME)}", uri);
    }

    [Fact]
    public async Task GetActivationStatusAsync_NotFound_ReturnsNotFound()
    {
        var owner = CreateOwner(systemScoped: false);
        SetupResponse(HttpStatusCode.NotFound, "{\"error\":\"activation not found\"}");

        var status = await owner.Tenant.Agent(TARGET_AGENT_NAME).GetActivationStatusAsync(ACTIVATION_NAME);

        Assert.Equal(ActivationCheckStatus.NotFound, status);
    }

    [Fact]
    public async Task GetActivationStatusAsync_Conflict_ReturnsDeactivated()
    {
        var owner = CreateOwner(systemScoped: false);
        SetupResponse(HttpStatusCode.Conflict, "{\"error\":\"deactivated\"}");

        var status = await owner.Tenant.Agent(TARGET_AGENT_NAME).GetActivationStatusAsync(ACTIVATION_NAME);

        Assert.Equal(ActivationCheckStatus.Deactivated, status);
    }

    [Fact]
    public async Task GetActivationStatusAsync_BadRequest_ThrowsInvalidOperation()
    {
        var owner = CreateOwner(systemScoped: false);
        SetupResponse(HttpStatusCode.BadRequest, "{\"error\":\"TenantId is required\"}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            owner.Tenant.Agent(TARGET_AGENT_NAME).GetActivationStatusAsync(ACTIVATION_NAME));
    }

    [Fact]
    public async Task GetActivationStatusAsync_ServerError_ThrowsHttpRequestException()
    {
        var owner = CreateOwner(systemScoped: false);
        SetupResponse(HttpStatusCode.InternalServerError, "boom");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            owner.Tenant.Agent(TARGET_AGENT_NAME).GetActivationStatusAsync(ACTIVATION_NAME));
    }

    [Fact]
    public async Task ActivationExistsAsync_TrueOnlyWhenActive()
    {
        var owner = CreateOwner(systemScoped: false);
        var other = owner.Tenant.Agent(TARGET_AGENT_NAME);

        SetupResponse(HttpStatusCode.OK, "");
        Assert.True(await other.ActivationExistsAsync(ACTIVATION_NAME));

        _httpMessageHandler.Reset();
        SetupResponse(HttpStatusCode.NotFound, "{\"error\":\"activation not found\"}");
        Assert.False(await other.ActivationExistsAsync(ACTIVATION_NAME));

        _httpMessageHandler.Reset();
        SetupResponse(HttpStatusCode.Conflict, "{\"error\":\"deactivated\"}");
        Assert.False(await other.ActivationExistsAsync(ACTIVATION_NAME));
    }

    [Fact]
    public async Task GetActivationStatusAsync_EmptyActivationName_Throws()
    {
        var owner = CreateOwner(systemScoped: false);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            owner.Tenant.Agent(TARGET_AGENT_NAME).GetActivationStatusAsync(""));
    }

    // ---- List / Create / Activate / Deactivate ----

    [Fact]
    public async Task ListActivationsAsync_ReturnsBoundHandles()
    {
        var owner = CreateOwner(systemScoped: false);
        HttpRequestMessage? captured = null;
        var payload = new[]
        {
            SampleActivation(active: true),
            SampleActivation(id: "id-2", name: "other-act", active: false)
        };
        SetupJsonResponse(HttpStatusCode.OK, payload, captureRequest: req => captured = req);

        var list = await owner.Tenant.Agent(TARGET_AGENT_NAME).ListActivationsAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal(ACTIVATION_ID, list[0].Id);
        Assert.True(list[0].IsActive);
        Assert.False(list[1].IsActive);

        Assert.NotNull(captured);
        var uri = captured!.RequestUri!.AbsoluteUri;
        Assert.Contains(WorkflowConstants.ApiEndpoints.Activations, uri);
        Assert.Contains($"agentName={Uri.EscapeDataString(TARGET_AGENT_NAME)}", uri);

        // Bound handle can call activate (verify it targets the activate endpoint).
        _httpMessageHandler.Reset();
        HttpRequestMessage? activateReq = null;
        SetupJsonResponse(
            HttpStatusCode.OK,
            new { message = "ok", activation = SampleActivation(active: true) },
            captureRequest: req => activateReq = req);

        await list[0].ActivateAsync();

        Assert.NotNull(activateReq);
        Assert.Contains($"{ACTIVATION_ID}/activate", activateReq!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateActivationAsync_PostsBody_AndReturnsBoundHandle()
    {
        var owner = CreateOwner(systemScoped: false);
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        SetupJsonResponse(
            HttpStatusCode.OK,
            SampleActivation(active: false),
            captureRequest: req => captured = req,
            captureBody: body => capturedBody = body);

        var created = await owner.Tenant.Agent(TARGET_AGENT_NAME).CreateActivationAsync(
            name: ACTIVATION_NAME,
            description: "EU region",
            participantId: "participant-1",
            workflows:
            [
                new WorkflowConfig
                {
                    WorkflowType = $"{TARGET_AGENT_NAME}:Main",
                    Inputs = [new WorkflowInputValue { Name = "region", Value = "eu" }]
                }
            ]);

        Assert.Equal(ACTIVATION_ID, created.Id);
        Assert.Equal(ACTIVATION_NAME, created.Name);
        Assert.False(created.IsActive);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Contains(WorkflowConstants.ApiEndpoints.Activations, captured.RequestUri!.AbsoluteUri);
        Assert.NotNull(capturedBody);
        Assert.Contains($"\"name\":\"{ACTIVATION_NAME}\"", capturedBody);
        Assert.Contains($"\"agentName\":\"{TARGET_AGENT_NAME}\"", capturedBody);
        Assert.Contains("\"description\":\"EU region\"", capturedBody);
        Assert.Contains("\"participantId\":\"participant-1\"", capturedBody);
        Assert.Contains("\"workflowType\"", capturedBody);
    }

    [Fact]
    public async Task ActivateAsync_ById_PostsToActivateEndpoint_AndParsesEnvelope()
    {
        var owner = CreateOwner(systemScoped: false);
        HttpRequestMessage? captured = null;
        SetupJsonResponse(
            HttpStatusCode.OK,
            new
            {
                message = "activated",
                workflowIds = new[] { "wf-1" },
                workflowCount = 1,
                activation = SampleActivation(active: true)
            },
            captureRequest: req => captured = req);

        var result = await owner.Tenant.Agent(TARGET_AGENT_NAME).ActivateAsync(ACTIVATION_ID);

        Assert.True(result.IsActive);
        Assert.Equal(ACTIVATION_ID, result.Id);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Contains($"{ACTIVATION_ID}/activate", captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task DeactivateAsync_ById_PostsToDeactivateEndpoint_AndParsesEnvelope()
    {
        var owner = CreateOwner(systemScoped: false);
        HttpRequestMessage? captured = null;
        SetupJsonResponse(
            HttpStatusCode.OK,
            new
            {
                message = "deactivated",
                activation = SampleActivation(active: false)
            },
            captureRequest: req => captured = req);

        var result = await owner.Tenant.Agent(TARGET_AGENT_NAME).DeactivateAsync(ACTIVATION_ID);

        Assert.False(result.IsActive);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Contains($"{ACTIVATION_ID}/deactivate", captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task ActivateAsync_WithWorkflowConfig_SendsBody()
    {
        var owner = CreateOwner(systemScoped: false);
        string? capturedBody = null;
        SetupJsonResponse(
            HttpStatusCode.OK,
            new { message = "ok", activation = SampleActivation(active: true) },
            captureBody: body => capturedBody = body);

        await owner.Tenant.Agent(TARGET_AGENT_NAME).ActivateAsync(
            ACTIVATION_ID,
            workflowConfiguration:
            [
                new WorkflowConfig { WorkflowType = "W", Inputs = [] }
            ]);

        Assert.NotNull(capturedBody);
        Assert.Contains("\"workflowConfiguration\"", capturedBody);
        Assert.Contains("\"workflowType\":\"W\"", capturedBody);
    }

    // ---- Tenant header ----

    [Fact]
    public async Task SystemScopedOwner_SendsTenantIdHeader()
    {
        var owner = CreateOwner(systemScoped: true);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, "", captureRequest: req => captured = req);

        XiansContext.SetTenantId(TENANT_ID);
        try
        {
            await owner.Tenant.Agent(TARGET_AGENT_NAME).ExistsAsync();

            Assert.NotNull(captured);
            Assert.True(captured!.Headers.TryGetValues(WorkflowConstants.Headers.TenantId, out var values));
            Assert.Equal(TENANT_ID, values!.Single());
        }
        finally
        {
            XiansContext.ClearTenantId();
        }
    }

    [Fact]
    public async Task NonSystemScopedOwner_DoesNotSendTenantIdHeader()
    {
        var owner = CreateOwner(systemScoped: false);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, "", captureRequest: req => captured = req);

        await owner.Tenant.Agent(TARGET_AGENT_NAME).ExistsAsync();

        Assert.NotNull(captured);
        Assert.False(captured!.Headers.Contains(WorkflowConstants.Headers.TenantId));
    }

    [Fact]
    public async Task GetActivationStatusAsync_SystemScopedOwner_SendsTenantIdHeader()
    {
        var owner = CreateOwner(systemScoped: true);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, "", captureRequest: req => captured = req);

        XiansContext.SetTenantId(TENANT_ID);
        try
        {
            var status = await owner.Tenant.Agent(TARGET_AGENT_NAME).GetActivationStatusAsync(ACTIVATION_NAME);

            Assert.Equal(ActivationCheckStatus.Active, status);
            Assert.NotNull(captured);
            Assert.True(captured!.Headers.TryGetValues(WorkflowConstants.Headers.TenantId, out var values));
            Assert.Equal(TENANT_ID, values!.Single());
        }
        finally
        {
            XiansContext.ClearTenantId();
        }
    }

    [Fact]
    public async Task GetActivationStatusAsync_NonSystemScopedOwner_DoesNotSendTenantIdHeader()
    {
        var owner = CreateOwner(systemScoped: false);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, "", captureRequest: req => captured = req);

        await owner.Tenant.Agent(TARGET_AGENT_NAME).GetActivationStatusAsync(ACTIVATION_NAME);

        Assert.NotNull(captured);
        Assert.False(captured!.Headers.Contains(WorkflowConstants.Headers.TenantId));
    }

    // ---- Construction ----

    [Fact]
    public void Agent_WithColonInName_Throws()
    {
        var owner = CreateOwner(systemScoped: false);

        Assert.Throws<ArgumentException>(() => owner.Tenant.Agent("bad:name"));
    }

    [Fact]
    public void Agent_ExposesTargetName()
    {
        var owner = CreateOwner(systemScoped: false);

        var other = owner.Tenant.Agent($"  {TARGET_AGENT_NAME}  ");

        Assert.Equal(TARGET_AGENT_NAME, other.Name);
    }

    [Fact]
    public void Agent_AcceptsNorwegianName_AndNfcNormalizes()
    {
        var owner = CreateOwner(systemScoped: false);
        var decomposed = "Ka\u030Are";

        var other = owner.Tenant.Agent($"  {decomposed}  ");

        Assert.Equal("Kåre", other.Name);
    }

    [Fact]
    public async Task ExistsAsync_NorwegianAgentName_UrlEncodesUtf8()
    {
        var owner = CreateOwner(systemScoped: false);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, "", captureRequest: req => captured = req);

        Assert.True(await owner.Tenant.Agent("Kjøpsassistent").ExistsAsync());

        var uri = captured!.RequestUri!.AbsoluteUri;
        Assert.Contains($"agentName={Uri.EscapeDataString("Kjøpsassistent")}", uri);
        Assert.Contains("Kj%C3%B8psassistent", uri);
    }

    [Fact]
    public async Task UnboundActivationInfo_ActivateAsync_Throws()
    {
        var unbound = new ActivationInfo { Id = ACTIVATION_ID, Name = ACTIVATION_NAME };

        await Assert.ThrowsAsync<InvalidOperationException>(() => unbound.ActivateAsync());
    }

    // ---- Helpers ----

    private static object SampleActivation(
        string? id = null,
        string? name = null,
        bool active = true)
    {
        return new
        {
            id = id ?? ACTIVATION_ID,
            name = name ?? ACTIVATION_NAME,
            agentName = TARGET_AGENT_NAME,
            description = (string?)null,
            participantId = (string?)null,
            createdBy = "system",
            createdAt = DateTime.UtcNow,
            tenantId = TENANT_ID,
            workflowConfiguration = (object?)null,
            workflowIds = Array.Empty<string>(),
            active,
            activatedAt = active ? DateTime.UtcNow : (DateTime?)null,
            deactivatedAt = active ? (DateTime?)null : DateTime.UtcNow
        };
    }

    private void SetupResponse(
        HttpStatusCode statusCode,
        string content,
        Action<HttpRequestMessage>? captureRequest = null)
    {
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => captureRequest?.Invoke(req))
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
    }

    private void SetupJsonResponse(
        HttpStatusCode statusCode,
        object payload,
        Action<HttpRequestMessage>? captureRequest = null,
        Action<string>? captureBody = null)
    {
        var json = JsonSerializer.Serialize(payload);
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
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private XiansAgent CreateOwner(bool systemScoped)
    {
        var mockHttpService = new Mock<IHttpClientService>();
        mockHttpService.Setup(x => x.Client).Returns(_httpClient);
        mockHttpService.Setup(x => x.GetHealthyClientAsync()).ReturnsAsync(_httpClient);

        var mockTemporalService = new Mock<ITemporalClientService>();
        mockTemporalService.Setup(x => x.IsConnectionHealthy()).Returns(true);

        var options = new XiansOptions
        {
            ApiKey = TestCertificateGenerator.GenerateTestCertificateBase64(TENANT_ID, "test-user"),
            ServerUrl = "http://localhost"
        };

        return new XiansAgent(
            OWNER_AGENT_NAME,
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
    }
}
