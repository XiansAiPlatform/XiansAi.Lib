using System.Net;
using Moq;
using Moq.Protected;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Unit tests for the activation existence validation performed before starting a workflow
/// under an explicit activation name. Verifies HTTP response mapping (200/404/409/400/5xx),
/// tenant header behavior, and that validation is skipped when no HTTP service is available.
///
/// dotnet test --filter "FullyQualifiedName~ActivationValidationServiceTests"
/// </summary>
[Collection("Sequential")]
public class ActivationValidationServiceTests : IDisposable
{
    private const string AGENT_NAME = "Fraud Detection Agent";
    private const string ACTIVATION_NAME = "fraud-detection-eu";
    private const string TENANT_ID = "test-tenant";

    private readonly Mock<HttpMessageHandler> _httpMessageHandler;
    private readonly HttpClient _httpClient;

    public ActivationValidationServiceTests()
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

    private void SetupResponse(HttpStatusCode statusCode, string content = "", Action<HttpRequestMessage>? captureRequest = null)
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

    [Fact]
    public async Task Ok_CompletesWithoutThrowing()
    {
        SetupResponse(HttpStatusCode.OK);

        await ActivationValidationService.EnsureActivationActiveAsync(
            _httpClient, AGENT_NAME, ACTIVATION_NAME, TENANT_ID, systemScoped: false);
    }

    [Fact]
    public async Task NotFound_ThrowsActivationNotFoundException_WithDetails()
    {
        SetupResponse(HttpStatusCode.NotFound, "{\"error\":\"not found\"}");

        var ex = await Assert.ThrowsAsync<ActivationNotFoundException>(() =>
            ActivationValidationService.EnsureActivationActiveAsync(
                _httpClient, AGENT_NAME, ACTIVATION_NAME, TENANT_ID, systemScoped: false));

        Assert.Equal(AGENT_NAME, ex.AgentName);
        Assert.Equal(ACTIVATION_NAME, ex.ActivationName);
        Assert.Equal(TENANT_ID, ex.TenantId);
        Assert.Contains(ACTIVATION_NAME, ex.Message);
        Assert.Contains(AGENT_NAME, ex.Message);
        Assert.Contains("may not be activated in this tenant", ex.Message);
    }

    [Fact]
    public async Task NotFound_ExceptionIsCatchableAsInvalidOperation_ForBackwardCompatibility()
    {
        SetupResponse(HttpStatusCode.NotFound, "{\"error\":\"not found\"}");

        await Assert.ThrowsAsync<ActivationNotFoundException>(() =>
            ActivationValidationService.EnsureActivationActiveAsync(
                _httpClient, AGENT_NAME, ACTIVATION_NAME, TENANT_ID, systemScoped: false));

        Assert.True(typeof(InvalidOperationException).IsAssignableFrom(typeof(ActivationNotFoundException)));
        Assert.True(typeof(InvalidOperationException).IsAssignableFrom(typeof(ActivationDeactivatedException)));
    }

    [Fact]
    public async Task Conflict_ThrowsActivationDeactivatedException()
    {
        SetupResponse(HttpStatusCode.Conflict, "{\"error\":\"deactivated\"}");

        var ex = await Assert.ThrowsAsync<ActivationDeactivatedException>(() =>
            ActivationValidationService.EnsureActivationActiveAsync(
                _httpClient, AGENT_NAME, ACTIVATION_NAME, TENANT_ID, systemScoped: false));

        Assert.Equal(AGENT_NAME, ex.AgentName);
        Assert.Equal(ACTIVATION_NAME, ex.ActivationName);
        Assert.Contains("deactivated", ex.Message);
    }

    [Fact]
    public async Task BadRequest_ThrowsInvalidOperation()
    {
        SetupResponse(HttpStatusCode.BadRequest, "{\"error\":\"TenantId is required\"}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ActivationValidationService.EnsureActivationActiveAsync(
                _httpClient, AGENT_NAME, ACTIVATION_NAME, TENANT_ID, systemScoped: false));
    }

    [Fact]
    public async Task ServerError_ThrowsHttpRequestException_SoRetryPoliciesApply()
    {
        SetupResponse(HttpStatusCode.InternalServerError, "boom");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            ActivationValidationService.EnsureActivationActiveAsync(
                _httpClient, AGENT_NAME, ACTIVATION_NAME, TENANT_ID, systemScoped: false));
    }

    [Fact]
    public async Task Request_TargetsExistsEndpoint_WithEscapedQueryParams()
    {
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, captureRequest: req => captured = req);

        await ActivationValidationService.EnsureActivationActiveAsync(
            _httpClient, AGENT_NAME, ACTIVATION_NAME, TENANT_ID, systemScoped: false);

        Assert.NotNull(captured);
        var uri = captured!.RequestUri!.AbsoluteUri;
        Assert.Contains(WorkflowConstants.ApiEndpoints.ActivationExists, uri);
        Assert.Contains($"activationName={Uri.EscapeDataString(ACTIVATION_NAME)}", uri);
        Assert.Contains($"agentName={Uri.EscapeDataString(AGENT_NAME)}", uri);
    }

    [Fact]
    public async Task SystemScopedAgent_SendsTenantIdHeader()
    {
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, captureRequest: req => captured = req);

        await ActivationValidationService.EnsureActivationActiveAsync(
            _httpClient, AGENT_NAME, ACTIVATION_NAME, TENANT_ID, systemScoped: true);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues(WorkflowConstants.Headers.TenantId, out var values));
        Assert.Equal(TENANT_ID, values!.Single());
    }

    [Fact]
    public async Task NonSystemScopedAgent_DoesNotSendTenantIdHeader()
    {
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, captureRequest: req => captured = req);

        await ActivationValidationService.EnsureActivationActiveAsync(
            _httpClient, AGENT_NAME, ACTIVATION_NAME, TENANT_ID, systemScoped: false);

        Assert.NotNull(captured);
        Assert.False(captured!.Headers.Contains(WorkflowConstants.Headers.TenantId));
    }

    [Fact]
    public void TypedExceptions_PreserveInnerException_WhenProvided()
    {
        // The typed exceptions accept an optional inner exception so callers can
        // preserve the original cause for diagnostics.
        var cause = new InvalidOperationException("original activity failure");

        var notFound = new ActivationNotFoundException(AGENT_NAME, ACTIVATION_NAME, TENANT_ID, cause);
        Assert.Same(cause, notFound.InnerException);
        Assert.Equal(AGENT_NAME, notFound.AgentName);
        Assert.Equal(ACTIVATION_NAME, notFound.ActivationName);
        Assert.Equal(TENANT_ID, notFound.TenantId);

        var deactivated = new ActivationDeactivatedException(AGENT_NAME, ACTIVATION_NAME, cause);
        Assert.Same(cause, deactivated.InnerException);
        Assert.Equal(AGENT_NAME, deactivated.AgentName);
        Assert.Equal(ACTIVATION_NAME, deactivated.ActivationName);
    }

    [Fact]
    public async Task SignalWithStart_MissingActivation_ThrowsActivationNotFoundException_BeforeStarting()
    {
        // SignalWithStart can create a new workflow, so an explicit activation is validated
        // up front - the typed exception must be thrown before any workflow is started.
        RegisterAgent(systemScoped: false, certificateTenant: TENANT_ID);
        SetupResponse(HttpStatusCode.NotFound, "{\"error\":\"not found\"}");

        var ex = await Assert.ThrowsAsync<ActivationNotFoundException>(() =>
            SubWorkflowService.SignalWithStartCoreAsync(
                $"{AGENT_NAME}:Some Workflow",
                uniqueKeys: [ACTIVATION_NAME],
                workflowArgs: [],
                signalName: "some-signal",
                signalArgs: [],
                activationName: ACTIVATION_NAME,
                executionTimeout: null));

        Assert.Equal(AGENT_NAME, ex.AgentName);
        Assert.Equal(ACTIVATION_NAME, ex.ActivationName);
    }

    [Fact]
    public async Task SignalWithStart_DeactivatedActivation_ThrowsActivationDeactivatedException_BeforeStarting()
    {
        RegisterAgent(systemScoped: false, certificateTenant: TENANT_ID);
        SetupResponse(HttpStatusCode.Conflict, "{\"error\":\"deactivated\"}");

        var ex = await Assert.ThrowsAsync<ActivationDeactivatedException>(() =>
            SubWorkflowService.SignalWithStartCoreAsync(
                $"{AGENT_NAME}:Some Workflow",
                uniqueKeys: [ACTIVATION_NAME],
                workflowArgs: [],
                signalName: "some-signal",
                signalArgs: [],
                activationName: ACTIVATION_NAME,
                executionTimeout: null));

        Assert.Equal(AGENT_NAME, ex.AgentName);
        Assert.Equal(ACTIVATION_NAME, ex.ActivationName);
    }

    [Fact]
    public async Task NoAgentsRegistered_SkipsValidationWithoutThrowing()
    {
        // No agents registered (CleanupForTests in constructor) means no HTTP service is
        // available - validation must be skipped so local/offline scenarios keep working.
        await ActivationValidationService.EnsureActivationActiveAsync(AGENT_NAME, ACTIVATION_NAME);
    }

    [Fact]
    public async Task SystemScopedAgent_UsesActingTenantFromContext_NotCertificateTenant()
    {
        // A system-scoped agent runs workflows for many tenants under one certificate.
        // The activation must be validated in the tenant running the action (from
        // workflow/async-local context), not the certificate's tenant.
        RegisterAgent(systemScoped: true, certificateTenant: "certificate-tenant");
        XiansContext.SetTenantId("acting-tenant");
        try
        {
            HttpRequestMessage? captured = null;
            SetupResponse(HttpStatusCode.OK, captureRequest: req => captured = req);

            await ActivationValidationService.EnsureActivationActiveAsync(AGENT_NAME, ACTIVATION_NAME);

            Assert.NotNull(captured);
            Assert.True(captured!.Headers.TryGetValues(WorkflowConstants.Headers.TenantId, out var values));
            Assert.Equal("acting-tenant", values!.Single());
        }
        finally
        {
            XiansContext.ClearTenantId();
        }
    }

    [Fact]
    public async Task SystemScopedAgent_OutsideTemporalContext_FallsBackToCertificateTenant()
    {
        RegisterAgent(systemScoped: true, certificateTenant: "certificate-tenant");
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, captureRequest: req => captured = req);

        await ActivationValidationService.EnsureActivationActiveAsync(AGENT_NAME, ACTIVATION_NAME);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues(WorkflowConstants.Headers.TenantId, out var values));
        Assert.Equal("certificate-tenant", values!.Single());
    }

    /// <summary>
    /// Registers an agent named AGENT_NAME backed by the mocked HTTP handler, so
    /// EnsureActivationActiveAsync(agentName, activationName) resolves it as the target agent.
    /// </summary>
    private void RegisterAgent(bool systemScoped, string certificateTenant)
    {
        var mockHttpService = new Mock<IHttpClientService>();
        mockHttpService.Setup(x => x.Client).Returns(_httpClient);
        mockHttpService.Setup(x => x.GetHealthyClientAsync()).ReturnsAsync(_httpClient);

        var mockTemporalService = new Mock<ITemporalClientService>();
        mockTemporalService.Setup(x => x.IsConnectionHealthy()).Returns(true);

        var options = new XiansOptions
        {
            ApiKey = TestCertificateGenerator.GenerateTestCertificateBase64(certificateTenant, "test-user"),
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

        XiansContext.RegisterAgent(agent);
    }
}
