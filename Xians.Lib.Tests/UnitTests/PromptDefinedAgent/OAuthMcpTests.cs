using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Authentication;
using Moq;
using PromptDefinedAgent.Mcp;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Http;
using Xians.Lib.Temporal;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.UnitTests.PromptDefinedAgent;

[Collection("Sequential")]
public sealed class OAuthMcpTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string AgentName = "Agent";
    private const string ActivationName = "Production";
    private readonly HttpClient _httpClient;
    private readonly XiansAgent _agent;

    public OAuthMcpTests()
    {
        XiansContext.CleanupForTests();
        _httpClient = new HttpClient(new CallbackHandler(RespondAsync))
        {
            BaseAddress = new Uri("http://localhost")
        };
        _agent = CreateAgent();
        XiansContext.RegisterAgent(_agent);
        XiansContext.SetCurrentAgentForTests(_agent);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        XiansContext.CleanupForTests();
    }

    [Fact]
    public async Task TokenCache_PersistsAndReturnsRefreshedTokens()
    {
        var connection = Connection();
        var refreshed = Tokens("new-access");
        var cache = Cache(connection);

        await cache.StoreTokensAsync(refreshed);

        Assert.Same(refreshed, await cache.GetTokensAsync());
    }

    [Fact]
    public async Task TokenCache_RestoresPreviousTokensWhenPersistenceFails()
    {
        ResponseOverride = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var connection = Connection();
        var original = connection.Tokens;
        var cache = Cache(connection);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            cache.StoreTokensAsync(Tokens("new-access")).AsTask());

        Assert.Same(original, await cache.GetTokensAsync());
    }

    [Fact]
    public async Task TokenCache_BlocksReadsDuringPersistence()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ResponseOverride = async request =>
        {
            started.SetResult();
            await release.Task;
            return SecretResponse(request);
        };
        var cache = Cache(Connection());
        var refreshed = Tokens("new-access");

        var write = cache.StoreTokensAsync(refreshed).AsTask();
        await started.Task;
        var read = cache.GetTokensAsync().AsTask();

        Assert.False(read.IsCompleted);
        release.SetResult();
        await write;
        Assert.Same(refreshed, await read);
    }

    [Fact]
    public async Task BuildOAuthOptions_RequiresConnectionKey()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpToolProvider.BuildOAuthOptionsAsync(null, Context()));

        Assert.Contains("requires a connection key", error.Message);
    }

    [Fact]
    public async Task BuildOAuthOptions_ReportsMissingSecret()
    {
        ResponseOverride = request => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/fetch")
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : JsonResponse(Array.Empty<object>()));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpToolProvider.BuildOAuthOptionsAsync("MCP_OAUTH_TEST", Context()));

        Assert.Contains("was not found", error.Message);
    }

    [Fact]
    public async Task BuildOAuthOptions_ReportsMalformedSecret()
    {
        SetConnectionResponses("not-json");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpToolProvider.BuildOAuthOptionsAsync("MCP_OAUTH_TEST", Context()));

        Assert.Contains("failed to deserialize", error.Message);
    }

    [Fact]
    public async Task BuildOAuthOptions_RejectsNonHttpsRemoteRedirect()
    {
        SetConnectionResponses(System.Text.Json.JsonSerializer.Serialize(Connection("http://example.com/callback")));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpToolProvider.BuildOAuthOptionsAsync("MCP_OAUTH_TEST", Context()));

        Assert.Contains("invalid redirect URI", error.Message);
    }

    private Func<HttpRequestMessage, Task<HttpResponseMessage>>? ResponseOverride { get; set; }

    private Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request) =>
        ResponseOverride?.Invoke(request) ?? Task.FromResult(SecretResponse(request));

    private static HttpResponseMessage SecretResponse(HttpRequestMessage request) =>
        JsonResponse(new
        {
            id = "secret-1",
            key = "MCP_OAUTH_TEST",
            value = "stored",
            tenantId = TenantId,
            createdBy = "test",
            createdAt = DateTime.UtcNow
        });

    private void SetConnectionResponses(string value)
    {
        ResponseOverride = request => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/fetch")
                ? JsonResponse(new { value })
                : JsonResponse(new[]
                {
                    new { id = "secret-1", key = "MCP_OAUTH_TEST", createdBy = "test", createdAt = DateTime.UtcNow }
                }));
    }

    private SecretVaultTokenCache Cache(OAuthConnection connection) => new(
        _agent.Secrets.TenantScope(TenantId).AgentScope(AgentName).ActivationScope(ActivationName),
        "secret-1",
        "MCP_OAUTH_TEST",
        connection);

    private static OAuthConnection Connection(string redirectUri = "https://studio.example.com/callback") => new()
    {
        ClientId = "client",
        ClientSecret = "secret",
        RedirectUri = redirectUri,
        Scopes = ["crm.read"],
        Tokens = Tokens("old-access")
    };

    private static TokenContainer Tokens(string accessToken) => new()
    {
        TokenType = "Bearer",
        AccessToken = accessToken,
        RefreshToken = "refresh",
        ExpiresIn = 3600,
        ObtainedAt = DateTimeOffset.UtcNow
    };

    private static XiansToolContext Context() =>
        new(TenantId, AgentName, ActivationName, "participant", null);

    private XiansAgent CreateAgent()
    {
        var httpService = new Mock<IHttpClientService>();
        httpService.Setup(service => service.Client).Returns(_httpClient);
        httpService.Setup(service => service.GetHealthyClientAsync()).ReturnsAsync(_httpClient);
        var temporalService = new Mock<ITemporalClientService>();
        temporalService.Setup(service => service.IsConnectionHealthy()).Returns(true);
        var options = new XiansOptions
        {
            ApiKey = TestCertificateGenerator.GenerateTestCertificateBase64(TenantId, "test-user"),
            ServerUrl = "http://localhost"
        };
        return new XiansAgent(
            AgentName,
            false,
            null, null, null, null, null, null, null,
            temporalService.Object,
            httpService.Object,
            options,
            null);
    }

    private static HttpResponseMessage JsonResponse<T>(T value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request);
    }
}
