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
    public async Task TokenCache_KeepsRefreshedTokensWhenPersistenceFails()
    {
        var attempts = 0;
        ResponseOverride = _ =>
        {
            attempts++;
            return Task.FromResult(EmptyResponse(HttpStatusCode.InternalServerError));
        };
        var connection = Connection();
        var original = connection.Tokens;
        var cache = Cache(connection);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            cache.StoreTokensAsync(Tokens("new-access")).AsTask());

        Assert.NotSame(original, await cache.GetTokensAsync());
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task TokenCache_AllowsReadsDuringPersistence()
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
        var reads = Enumerable.Range(0, 3).Select(_ => cache.GetTokensAsync().AsTask()).ToArray();

        Assert.All(reads, read => Assert.True(read.IsCompleted));
        Assert.All(await Task.WhenAll(reads), token => Assert.Same(refreshed, token));
        release.SetResult();
        await write;
    }

    [Fact]
    public async Task TokenCache_PropagatesCancellation()
    {
        var cache = Cache(Connection());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.GetTokensAsync(cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.StoreTokensAsync(Tokens("new-access"), cancellation.Token).AsTask());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BuildOAuthOptions_RequiresConnectionKey(string? connectionKey)
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpToolProvider.BuildOAuthOptionsAsync(connectionKey, Context()));

        Assert.Contains("requires a connection key", error.Message);
    }

    [Fact]
    public async Task BuildOAuthOptions_ReportsMissingSecret()
    {
        ResponseOverride = request => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/fetch")
                ? EmptyResponse(HttpStatusCode.NotFound)
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

    [Fact]
    public async Task BuildOAuthOptions_AllowsLoopbackHttpRedirect()
    {
        SetConnectionResponses(System.Text.Json.JsonSerializer.Serialize(Connection("http://localhost/callback")));

        var options = await McpToolProvider.BuildOAuthOptionsAsync("MCP_OAUTH_TEST", Context());

        Assert.Equal("http://localhost/callback", options.RedirectUri.ToString());
    }

    [Fact]
    public async Task BuildOAuthOptions_AllowsPublicClientWithoutSecret()
    {
        var connection = Connection(clientSecret: null);
        SetConnectionResponses(System.Text.Json.JsonSerializer.Serialize(connection));

        var options = await McpToolProvider.BuildOAuthOptionsAsync("MCP_OAUTH_TEST", Context());

        Assert.Null(options.ClientSecret);
    }

    [Fact]
    public async Task OAuthConnectionResolver_ReusesMetadataAndTokenCache()
    {
        var fetches = 0;
        var lists = 0;
        var value = System.Text.Json.JsonSerializer.Serialize(Connection());
        ResponseOverride = request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/fetch"))
            {
                Interlocked.Increment(ref fetches);
                return Task.FromResult(JsonResponse(new { value }));
            }
            Interlocked.Increment(ref lists);
            return Task.FromResult(JsonResponse(new[]
            {
                new { id = "secret-1", key = "MCP_OAUTH_TEST", createdBy = "test", createdAt = DateTime.UtcNow }
            }));
        };
        var resolver = new OAuthConnectionResolver(Context());

        var options = await Task.WhenAll(
            resolver.ResolveAsync("MCP_OAUTH_TEST"),
            resolver.ResolveAsync("MCP_OAUTH_TEST"));

        Assert.Equal(1, fetches);
        Assert.Equal(1, lists);
        Assert.Same(options[0].TokenCache, options[1].TokenCache);
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

    private static OAuthConnection Connection(
        string redirectUri = "https://studio.example.com/callback",
        string? clientSecret = "secret") => new()
    {
        ClientId = "client",
        ClientSecret = clientSecret,
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

    private static HttpResponseMessage EmptyResponse(HttpStatusCode statusCode) => new(statusCode);

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request);
    }
}
