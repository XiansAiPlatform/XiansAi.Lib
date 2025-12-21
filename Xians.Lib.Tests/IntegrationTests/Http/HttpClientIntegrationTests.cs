using System.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xians.Lib.Common;
using Xians.Lib.Configuration;
using Xians.Lib.Http;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.Http;

[Trait("Category", "Integration")]
public class HttpClientIntegrationTests : IAsyncLifetime
{
    private WireMockServer? _mockServer;
    private IHttpClientService? _httpService;

    public Task InitializeAsync()
    {
        // Setup mock HTTP server
        _mockServer = WireMockServer.Start();
        
        var config = new ServerConfiguration
        {
            ServerUrl = _mockServer.Url!,
            ApiKey = TestCertificateGenerator.GetTestCertificate()
        };
        
        _httpService = ServiceFactory.CreateHttpClientService(config);
        
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetWithRetryAsync_WithSuccessfulResponse_ShouldReturnData()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create().WithPath("/api/test").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"message\": \"success\"}")
                .WithHeader("Content-Type", "application/json"));

        // Act
        var response = await _httpService!.GetWithRetryAsync("/api/test");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("success", content);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_WithTransientFailure_ShouldRetryAndSucceed()
    {
        // Arrange - Set up endpoint that always succeeds
        _mockServer!
            .Given(Request.Create().WithPath("/api/data").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"result\": \"data\"}"));

        var attemptCount = 0;
        
        // Act - Test retry logic by wrapping an operation that fails first then succeeds
        var result = await _httpService!.ExecuteWithRetryAsync(async () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                // Simulate transient failure on first attempt
                throw new HttpRequestException("Simulated network error");
            }
            
            // Succeed on retry
            return await _httpService!.GetWithRetryAsync("/api/data");
        });

        // Assert
        Assert.True(result.IsSuccessStatusCode);
        Assert.Equal(2, attemptCount); // Should have attempted twice
    }

    [Fact]
    public async Task PostWithRetryAsync_WithJsonPayload_ShouldSucceed()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create().WithPath("/api/data").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(201)
                .WithBody("{\"id\": \"123\"}"));

        var payload = new { name = "test", value = 42 };
        var jsonContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(payload), 
            System.Text.Encoding.UTF8, 
            "application/json");

        // Act
        var response = await _httpService!.PostWithRetryAsync("/api/data", jsonContent);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("123", content);
    }

    [Fact]
    public async Task IsHealthyAsync_WithHealthyServer_ShouldReturnTrue()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create().WithStatusCode(200));

        // Act
        var isHealthy = await _httpService!.IsHealthyAsync();

        // Assert
        Assert.True(isHealthy);
    }

    [Fact]
    public async Task GetWithRetryAsync_WithCertificateAuth_ShouldIncludeAuthorizationHeader()
    {
        // Arrange - Certificate is sent as Bearer token (Base64-encoded)
        // The client will re-export the certificate and send it
        _mockServer!
            .Given(Request.Create()
                .WithPath("/api/secure")
                .WithHeader("Authorization", "Bearer *", matchBehaviour: WireMock.Matchers.MatchBehaviour.AcceptOnMatch)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"authorized\": true}"));

        // Act
        var response = await _httpService!.GetWithRetryAsync("/api/secure");

        // Assert - If the Authorization header wasn't sent, WireMock wouldn't match
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task GetHealthyClientAsync_ShouldReturnWorkingClient()
    {
        // Arrange
        _mockServer!
            .Given(Request.Create().WithPath("/api/health").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"status\": \"healthy\"}"));

        // Act
        var client = await _httpService!.GetHealthyClientAsync();
        var response = await client.GetAsync("/api/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("healthy", content);
    }

    public Task DisposeAsync()
    {
        _httpService?.Dispose();
        _mockServer?.Stop();
        _mockServer?.Dispose();
        return Task.CompletedTask;
    }
}
