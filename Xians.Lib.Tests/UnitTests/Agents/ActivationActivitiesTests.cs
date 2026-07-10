using System.Net;
using Moq;
using Moq.Protected;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Xians.Lib.Agents.Core;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Activations;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Unit tests for the activation validation system activity. Verifies that definitive
/// negative results (not found / deactivated) are returned as a status - so the activity
/// completes successfully and Temporal does not log failed-activity warnings - while
/// invalid requests fail non-retryably and transient errors propagate for retry.
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

    public ActivationActivitiesTests()
    {
        XiansContext.CleanupForTests();

        _httpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        RegisterAgent();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        XiansContext.CleanupForTests();
    }

    private void SetupResponse(HttpStatusCode statusCode, string content = "")
    {
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
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
            new ActivationActivities().ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME));

        Assert.Equal(ActivationCheckStatus.Active, status);
    }

    [Fact]
    public async Task MissingActivation_ReturnsNotFound_InsteadOfThrowing()
    {
        SetupResponse(HttpStatusCode.NotFound, "{\"error\":\"not found\"}");

        var status = await RunActivityAsync(() =>
            new ActivationActivities().ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME));

        Assert.Equal(ActivationCheckStatus.NotFound, status);
    }

    [Fact]
    public async Task DeactivatedActivation_ReturnsDeactivated_InsteadOfThrowing()
    {
        SetupResponse(HttpStatusCode.Conflict, "{\"error\":\"deactivated\"}");

        var status = await RunActivityAsync(() =>
            new ActivationActivities().ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME));

        Assert.Equal(ActivationCheckStatus.Deactivated, status);
    }

    [Fact]
    public async Task InvalidRequest_ThrowsNonRetryableApplicationFailure()
    {
        SetupResponse(HttpStatusCode.BadRequest, "{\"error\":\"TenantId is required\"}");

        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(() =>
            RunActivityAsync(() =>
                new ActivationActivities().ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME)));

        Assert.True(ex.NonRetryable);
    }

    [Fact]
    public async Task TransientServerError_PropagatesForRetry()
    {
        SetupResponse(HttpStatusCode.InternalServerError, "boom");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            RunActivityAsync(() =>
                new ActivationActivities().ValidateActivationAsync(AGENT_NAME, ACTIVATION_NAME)));
    }

    /// <summary>
    /// Registers an agent named AGENT_NAME backed by the mocked HTTP handler, so the
    /// underlying validation service resolves it as the target agent.
    /// </summary>
    private void RegisterAgent()
    {
        var mockHttpService = new Mock<IHttpClientService>();
        mockHttpService.Setup(x => x.Client).Returns(_httpClient);
        mockHttpService.Setup(x => x.GetHealthyClientAsync()).ReturnsAsync(_httpClient);

        var mockTemporalService = new Mock<ITemporalClientService>();
        mockTemporalService.Setup(x => x.IsConnectionHealthy()).Returns(true);

        var options = new XiansOptions
        {
            ApiKey = TestCertificateGenerator.GenerateTestCertificateBase64("test-tenant", "test-user"),
            ServerUrl = "http://localhost"
        };

        var agent = new XiansAgent(
            AGENT_NAME,
            false, // systemScoped
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
