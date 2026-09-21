using System.Net;
using System.Net.Http.Json;
using Moq;
using Moq.Protected;
using Temporalio.Testing;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Secrets.Models;
using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Secrets;
using Xians.Lib.Temporal.Workflows.Secrets.Models;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.UnitTests.Agents;

/// <summary>
/// Unit tests for Secret Vault SDK and system activities.
///
/// dotnet test --filter "FullyQualifiedName~SecretVault"
/// </summary>
[Collection("Sequential")]
public class SecretVaultTests : IDisposable
{
    private const string AGENT_NAME = "test-agent";
    private const string TENANT_ID = "test-tenant";

    private readonly Mock<HttpMessageHandler> _httpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly XiansAgent _agent;

    public SecretVaultTests()
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
    public async Task CreateAsync_PostsToSecretsEndpoint()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(SampleSecret()),
            captureRequest: req => captured = req,
            captureBody: body => capturedBody = body);

        var created = await _agent.Secrets.TenantScope(TENANT_ID).CreateAsync("api-key", "sk-xxx");

        Assert.Equal("sec-1", created.Id);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Contains(WorkflowConstants.ApiEndpoints.Secrets, captured.RequestUri!.AbsoluteUri);
        Assert.Contains("\"key\":\"api-key\"", capturedBody);
        Assert.Contains("\"value\":\"sk-xxx\"", capturedBody);
        Assert.True(captured.Headers.Contains(WorkflowConstants.Headers.TenantId));
    }

    [Fact]
    public async Task CreateAsync_Conflict_Throws()
    {
        SetupResponse(HttpStatusCode.Conflict, new StringContent("{\"error\":\"exists\"}"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _agent.Secrets.TenantScope(TENANT_ID).CreateAsync("api-key", "sk-xxx"));
    }

    [Fact]
    public async Task FailedRequest_DoesNotEmbedServerBodyInException()
    {
        // Activity failures are persisted in Temporal workflow history. The server may echo a
        // secret value in an error payload; that must never appear in the exception message.
        const string leakedSecret = "sk-should-not-leak";
        SetupResponse(HttpStatusCode.InternalServerError, new StringContent($"{{\"error\":\"{leakedSecret}\"}}"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _agent.Secrets.TenantScope(TENANT_ID).CreateAsync("api-key", "sk-xxx"));

        Assert.DoesNotContain(leakedSecret, ex.Message);
        Assert.Contains("InternalServerError", ex.Message);
    }

    [Fact]
    public async Task FetchByKeyAsync_NotFound_ReturnsNull()
    {
        SetupResponse(HttpStatusCode.NotFound, new StringContent(""));

        var result = await _agent.Secrets.TenantScope(TENANT_ID).FetchByKeyAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchByKeyAsync_Ok_ReturnsValue()
    {
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(new { value = "sk-xxx" }),
            captureRequest: req => captured = req);

        var result = await _agent.Secrets.TenantScope(TENANT_ID).AgentScope(AGENT_NAME)
            .FetchByKeyAsync("api-key");

        Assert.Equal("sk-xxx", result!.Value);
        Assert.Contains("/fetch", captured!.RequestUri!.AbsoluteUri);
        Assert.Contains("key=api-key", captured.RequestUri.Query);
        Assert.Contains($"tenantId={TENANT_ID}", captured.RequestUri.Query);
        Assert.Contains($"agentId={Uri.EscapeDataString(AGENT_NAME)}", captured.RequestUri.Query);
    }

    [Fact]
    public async Task ListAsync_ReturnsItems()
    {
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(new[]
        {
            new SecretVaultListItem
            {
                Id = "sec-1",
                Key = "api-key",
                CreatedBy = "user",
                CreatedAt = DateTime.UtcNow
            }
        }));

        var list = await _agent.Secrets.TenantScope(TENANT_ID).ListAsync();

        Assert.Single(list);
        Assert.Equal("api-key", list[0].Key);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        SetupResponse(HttpStatusCode.NotFound, new StringContent(""));

        var result = await _agent.Secrets.TenantScope(TENANT_ID).GetByIdAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_PutsBody()
    {
        HttpRequestMessage? captured = null;
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(SampleSecret("sk-new")),
            captureRequest: req => captured = req);

        var updated = await _agent.Secrets.TenantScope(TENANT_ID).UpdateAsync("sec-1", value: "sk-new");

        Assert.Equal("sk-new", updated.Value);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.Contains("sec-1", captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        SetupResponse(HttpStatusCode.NotFound, new StringContent(""));

        var deleted = await _agent.Secrets.TenantScope(TENANT_ID).DeleteAsync("missing");

        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteAsync_Ok_ReturnsTrue()
    {
        SetupResponse(HttpStatusCode.OK, new StringContent(""));

        var deleted = await _agent.Secrets.TenantScope(TENANT_ID).DeleteAsync("sec-1");

        Assert.True(deleted);
    }

    [Fact]
    public async Task ListSecretsActivity_Ok()
    {
        SetupResponse(HttpStatusCode.OK, JsonContent.Create(new[] { SampleListItem() }));

        var secrets = await new ActivityEnvironment().RunAsync(() =>
            new SecretVaultActivities(_agent).ListSecretsAsync(
                new SecretVaultListActivityRequest
                {
                    Scope = new SecretVaultScopePayload { TenantId = TENANT_ID }
                }));

        Assert.Equal("sec-1", Assert.Single(secrets).Id);
    }

    [Fact]
    public async Task DeleteSecretActivity_NotFound_ReturnsFalse()
    {
        SetupResponse(HttpStatusCode.NotFound, new StringContent(""));

        var deleted = await new ActivityEnvironment().RunAsync(() =>
            new SecretVaultActivities(_agent).DeleteSecretAsync(
                new SecretVaultIdActivityRequest
                {
                    Id = "missing",
                    Scope = new SecretVaultScopePayload { TenantId = TENANT_ID }
                }));

        Assert.False(deleted);
    }

    /// <summary>
    /// Activity arguments and results are persisted in Temporal workflow history, so no Secret Vault
    /// activity may carry a decrypted secret value. Guards against one being reintroduced.
    /// </summary>
    [Fact]
    public void SecretVaultActivities_ExposeNoSecretValues()
    {
        var valueCarrying = new[]
        {
            typeof(SecretVaultCreateRequest),
            typeof(SecretVaultUpdateRequest),
            typeof(SecretVaultGetResponse),
            typeof(SecretVaultFetchResponse),
            typeof(SecretVaultUpdateActivityRequest),
            typeof(SecretVaultFetchActivityRequest)
        };

        var activities = typeof(SecretVaultActivities)
            .GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(Temporalio.Activities.ActivityAttribute), false).Any())
            .ToList();

        Assert.NotEmpty(activities);

        foreach (var activity in activities)
        {
            var crossing = activity.GetParameters().Select(p => p.ParameterType)
                .Append(UnwrapTask(activity.ReturnType));

            foreach (var type in crossing)
            {
                Assert.DoesNotContain(type, valueCarrying);
            }
        }
    }

    private static Type UnwrapTask(Type returnType)
        => returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : returnType;

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

    private static SecretVaultGetResponse SampleSecret(string value = "sk-xxx") => new()
    {
        Id = "sec-1",
        Key = "api-key",
        Value = value,
        TenantId = TENANT_ID,
        CreatedBy = "user",
        CreatedAt = DateTime.UtcNow
    };

    private static SecretVaultListItem SampleListItem() => new()
    {
        Id = "sec-1",
        Key = "api-key",
        TenantId = TENANT_ID,
        CreatedBy = "user",
        CreatedAt = DateTime.UtcNow
    };
}
