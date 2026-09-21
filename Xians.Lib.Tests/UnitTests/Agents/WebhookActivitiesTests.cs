using System.Net;
using System.Net.Http.Json;
using Moq;
using Moq.Protected;
using Temporalio.Testing;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Webhooks.Models;
using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Webhooks;
using Xians.Lib.Temporal.Workflows.Webhooks.Models;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Unit tests for webhook system activities (workflow stubs).
/// Collection HTTP tests live in <see cref="WebhookCollectionTests"/>.
///
/// dotnet test --filter "FullyQualifiedName~WebhookActivitiesTests"
/// </summary>
[Collection("Sequential")]
public class WebhookActivitiesTests : IDisposable
{
    private const string AGENT_NAME = "test-agent";
    private const string ACTIVATION_NAME = "test-activation";
    private const string TENANT_ID = "test-tenant";

    private readonly Mock<HttpMessageHandler> _httpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly XiansAgent _agent;

    public WebhookActivitiesTests()
    {
        XiansContext.CleanupForTests();

        _httpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        _agent = CreateAgent();
        XiansContext.RegisterAgent(_agent);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        XiansContext.CleanupForTests();
    }

    [Fact]
    public async Task CreateWebhookActivity_PostsBody()
    {
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(SampleWebhook()),
            captureRequest: req => captured = req);

        var created = await new ActivityEnvironment().RunAsync(() =>
            new WebhookActivities(_agent).CreateWebhookAsync(new WebhookCreateActivityRequest
            {
                AgentName = AGENT_NAME,
                ActivationName = ACTIVATION_NAME,
                WebhookName = "EmailReceived"
            }));

        Assert.Equal("wh-1", created.Id);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Contains(WorkflowConstants.ApiEndpoints.AgentWebhooks, captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task ListWebhooksActivity_Ok()
    {
        var envelope = new { webhooks = new[] { SampleWebhook() } };
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(envelope));

        var listed = await new ActivityEnvironment().RunAsync(() =>
            new WebhookActivities(_agent).ListWebhooksAsync(new WebhookListActivityRequest
            {
                AgentName = AGENT_NAME,
                ActivationName = ACTIVATION_NAME
            }));

        Assert.Single(listed);
        Assert.Equal("wh-1", listed[0].Id);
    }

    [Fact]
    public async Task DeleteWebhookActivity_NotFound_ReturnsFalse()
    {
        SetupResponse(HttpStatusCode.NotFound, new StringContent(""));

        var deleted = await new ActivityEnvironment().RunAsync(() =>
            new WebhookActivities(_agent).DeleteWebhookAsync("missing"));

        Assert.False(deleted);
    }

    private void SetupResponse(
        HttpStatusCode statusCode,
        HttpContent content,
        Action<HttpRequestMessage>? captureRequest = null)
    {
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captureRequest?.Invoke(req))
            .ReturnsAsync(new HttpResponseMessage(statusCode) { Content = content });
    }

    private XiansAgent CreateAgent()
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
            AGENT_NAME,
            false,
            null, null, null, null, null, null, null,
            mockTemporalService.Object,
            mockHttpService.Object,
            options,
            null);
    }

    private static WebhookInfo SampleWebhook() => new()
    {
        Id = "wh-1",
        Name = "Webhook-EmailReceived-test-activation",
        AgentName = AGENT_NAME,
        ActivationName = ACTIVATION_NAME,
        WebhookUrl = "/api/user/webhooks/builtin?apikeyId=abc",
        IsEnabled = true,
        CreatedAt = DateTime.UtcNow
    };
}
