using System.Net;
using Moq;
using Moq.Protected;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Activations;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Unit tests for activation system activities: child-workflow pre-flight
/// (<see cref="ActivationActivities.ValidateActivationAsync"/>) and public status
/// (<see cref="ActivationActivities.GetActivationStatusAsync"/>). Both use the worker
/// owner's HTTP client and SystemScoped flag, matching exists / list / create.
///
/// dotnet test --filter "FullyQualifiedName~ActivationActivitiesTests"
/// </summary>
[Collection("Sequential")]
public class ActivationActivitiesTests : IDisposable
{
    private const string AGENT_NAME = "Fraud Detection Agent";
    private const string ACTIVATION_NAME = "fraud-detection-eu";

    private readonly Mock<HttpMessageHandler> _httpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly XiansAgent _owner;

    public ActivationActivitiesTests()
    {
        XiansContext.CleanupForTests();

        _httpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        _owner = RegisterAgent();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        XiansContext.CleanupForTests();
    }

    private void SetupResponse(
        HttpStatusCode statusCode,
        string content = "",
        Action<HttpRequestMessage>? captureRequest = null)
    {
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captureRequest?.Invoke(req))
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
    }

    private static Task<T> RunActivityAsync<T>(Func<Task<T>> activity) =>
        new ActivityEnvironment().RunAsync(activity);

    [Fact]
    public async Task ActiveActivation_ReturnsActive()
    {
        SetupResponse(HttpStatusCode.OK);

        var status = await RunActivityAsync(() =>
            new ActivationActivities(_owner).ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME));

        Assert.Equal(ActivationCheckStatus.Active, status);
    }

    [Fact]
    public async Task MissingActivation_ReturnsNotFound_InsteadOfThrowing()
    {
        SetupResponse(HttpStatusCode.NotFound, "{\"error\":\"not found\"}");

        var status = await RunActivityAsync(() =>
            new ActivationActivities(_owner).ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME));

        Assert.Equal(ActivationCheckStatus.NotFound, status);
    }

    [Fact]
    public async Task DeactivatedActivation_ReturnsDeactivated_InsteadOfThrowing()
    {
        SetupResponse(HttpStatusCode.Conflict, "{\"error\":\"deactivated\"}");

        var status = await RunActivityAsync(() =>
            new ActivationActivities(_owner).ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME));

        Assert.Equal(ActivationCheckStatus.Deactivated, status);
    }

    [Fact]
    public async Task InvalidRequest_ThrowsNonRetryableApplicationFailure()
    {
        SetupResponse(HttpStatusCode.BadRequest, "{\"error\":\"TenantId is required\"}");

        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(() =>
            RunActivityAsync(() =>
                new ActivationActivities(_owner).ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME)));

        Assert.True(ex.NonRetryable);
    }

    [Fact]
    public async Task TransientServerError_PropagatesForRetry()
    {
        SetupResponse(HttpStatusCode.InternalServerError, "boom");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            RunActivityAsync(() =>
                new ActivationActivities(_owner).ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME)));
    }

    [Fact]
    public async Task ValidateActivationAsync_NoHttpService_ThrowsNonRetryable()
    {
        var offlineOwner = RegisterAgent("offline-preflight", withHttp: false);

        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(() =>
            RunActivityAsync(() =>
                new ActivationActivities(offlineOwner).ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME)));

        Assert.True(ex.NonRetryable);
        Assert.Contains("HTTP service is not available", ex.Message);
    }

    [Fact]
    public async Task ValidateActivationAsync_UsesOwnerSystemScoped_NotTargetAgent()
    {
        var systemOwner = RegisterAgent("validate-owner", systemScoped: true);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, captureRequest: req => captured = req);

        XiansContext.SetTenantId("acting-tenant");
        try
        {
            var status = await RunActivityAsync(() =>
                new ActivationActivities(systemOwner).ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME));

            Assert.Equal(ActivationCheckStatus.Active, status);
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
    public async Task GetActivationStatusAsync_Ok_ReturnsActive()
    {
        SetupResponse(HttpStatusCode.OK);

        var status = await RunActivityAsync(() =>
            new ActivationActivities(_owner).GetActivationStatusAsync(AGENT_NAME, ACTIVATION_NAME));

        Assert.Equal(ActivationCheckStatus.Active, status);
    }

    [Fact]
    public async Task GetActivationStatusAsync_MissingActivation_ReturnsNotFound()
    {
        SetupResponse(HttpStatusCode.NotFound, "{\"error\":\"not found\"}");

        var status = await RunActivityAsync(() =>
            new ActivationActivities(_owner).GetActivationStatusAsync(AGENT_NAME, ACTIVATION_NAME));

        Assert.Equal(ActivationCheckStatus.NotFound, status);
    }

    [Fact]
    public async Task GetActivationStatusAsync_Deactivated_ReturnsDeactivated()
    {
        SetupResponse(HttpStatusCode.Conflict, "{\"error\":\"deactivated\"}");

        var status = await RunActivityAsync(() =>
            new ActivationActivities(_owner).GetActivationStatusAsync(AGENT_NAME, ACTIVATION_NAME));

        Assert.Equal(ActivationCheckStatus.Deactivated, status);
    }

    [Fact]
    public async Task GetActivationStatusAsync_NoHttpService_Throws()
    {
        var offlineOwner = RegisterAgent("offline-owner", withHttp: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunActivityAsync(() =>
                new ActivationActivities(offlineOwner).GetActivationStatusAsync(AGENT_NAME, ACTIVATION_NAME)));

        Assert.Contains("HTTP service is not available", ex.Message);
    }

    [Fact]
    public async Task GetActivationStatusAsync_UsesOwnerSystemScoped_NotTargetAgent()
    {
        // The fixture agent (AGENT_NAME) is non-system-scoped. Resolving the target for HTTP
        // would omit the tenant header. Status must follow the worker owner.
        var systemOwner = RegisterAgent("owner-agent", systemScoped: true);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, captureRequest: req => captured = req);

        XiansContext.SetTenantId("acting-tenant");
        try
        {
            var status = await RunActivityAsync(() =>
                new ActivationActivities(systemOwner).GetActivationStatusAsync(AGENT_NAME, ACTIVATION_NAME));

            Assert.Equal(ActivationCheckStatus.Active, status);
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
    public async Task GetActivationStatusAsync_NonSystemScopedOwner_OmitsHeader_EvenIfTargetIsSystemScoped()
    {
        RegisterAgent("system-target", systemScoped: true);
        var owner = RegisterAgent("manager-agent", systemScoped: false);
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, captureRequest: req => captured = req);

        XiansContext.SetTenantId("acting-tenant");
        try
        {
            var status = await RunActivityAsync(() =>
                new ActivationActivities(owner).GetActivationStatusAsync("system-target", ACTIVATION_NAME));

            Assert.Equal(ActivationCheckStatus.Active, status);
            Assert.NotNull(captured);
            Assert.False(captured!.Headers.Contains(WorkflowConstants.Headers.TenantId));
        }
        finally
        {
            XiansContext.ClearTenantId();
        }
    }

    [Fact]
    public async Task GetActivationStatusAsync_UsesOwnerHttpClient_NotTargetAgent()
    {
        var targetHandler = new Mock<HttpMessageHandler>();
        targetHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("target HTTP client must not be used"));

        using var targetClient = new HttpClient(targetHandler.Object)
        {
            BaseAddress = new Uri("http://target.example")
        };

        RegisterAgent("throwing-target", systemScoped: true, httpClient: targetClient);
        var owner = RegisterAgent("manager-owner");
        SetupResponse(HttpStatusCode.OK);

        var status = await RunActivityAsync(() =>
            new ActivationActivities(owner).GetActivationStatusAsync("throwing-target", ACTIVATION_NAME));

        Assert.Equal(ActivationCheckStatus.Active, status);
    }

    [Fact]
    public async Task GetActivationStatusAsync_BadRequest_ThrowsInvalidOperation()
    {
        SetupResponse(HttpStatusCode.BadRequest, "{\"error\":\"TenantId is required\"}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunActivityAsync(() =>
                new ActivationActivities(_owner).GetActivationStatusAsync(AGENT_NAME, ACTIVATION_NAME)));
    }

    [Fact]
    public async Task GetActivationStatusAsync_TransientServerError_ThrowsHttpRequestException()
    {
        SetupResponse(HttpStatusCode.InternalServerError, "boom");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            RunActivityAsync(() =>
                new ActivationActivities(_owner).GetActivationStatusAsync(AGENT_NAME, ACTIVATION_NAME)));
    }

    [Fact]
    public async Task AgentExistsAsync_Ok_ReturnsTrue()
    {
        SetupResponse(HttpStatusCode.OK);

        var exists = await RunActivityAsync(() =>
            new ActivationActivities(_owner).AgentExistsAsync(AGENT_NAME));

        Assert.True(exists);
    }

    [Fact]
    public async Task AgentExistsAsync_NotFound_ReturnsFalse()
    {
        SetupResponse(HttpStatusCode.NotFound, "{\"error\":\"Agent not found\"}");

        var exists = await RunActivityAsync(() =>
            new ActivationActivities(_owner).AgentExistsAsync(AGENT_NAME));

        Assert.False(exists);
    }

    /// <summary>
    /// Registers an agent. HTTP uses <paramref name="httpClient"/> when provided, otherwise
    /// the fixture client (unless <paramref name="withHttp"/> is false).
    /// </summary>
    private XiansAgent RegisterAgent(
        string? name = null,
        bool systemScoped = false,
        bool withHttp = true,
        HttpClient? httpClient = null)
    {
        Mock<IHttpClientService>? mockHttpService = null;
        if (withHttp)
        {
            var client = httpClient ?? _httpClient;
            mockHttpService = new Mock<IHttpClientService>();
            mockHttpService.Setup(x => x.Client).Returns(client);
            mockHttpService.Setup(x => x.GetHealthyClientAsync()).ReturnsAsync(client);
        }

        var mockTemporalService = new Mock<ITemporalClientService>();
        mockTemporalService.Setup(x => x.IsConnectionHealthy()).Returns(true);

        var options = new XiansOptions
        {
            ApiKey = TestCertificateGenerator.GenerateTestCertificateBase64("test-tenant", "test-user"),
            ServerUrl = "http://localhost"
        };

        var agent = new XiansAgent(
            name ?? AGENT_NAME,
            systemScoped,
            null, // description
            null, // summary
            null, // version
            null, // author
            null, // category
            null, // prompts
            null, // uploader
            mockTemporalService.Object,
            mockHttpService?.Object,
            options,
            null); // cacheService

        XiansContext.RegisterAgent(agent);
        return agent;
    }
}
