using Xunit;
using Xians.Lib.Agents.Knowledge;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Agents.Workflows.Models;
using Xians.Lib.Http;

namespace Xians.Lib.Tests.UnitTests.Agents;

[Collection("Sequential")]
public class KnowledgeCollectionTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<IHttpClientService> _mockHttpService;
    private readonly XiansAgent _agent;
    private readonly KnowledgeCollection _knowledgeCollection;

    public KnowledgeCollectionTests()
    {
        // Clean up static registries from previous tests
        XiansContext.CleanupForTests();
        
        // Create mock HTTP message handler
        _httpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        
        // Mock IHttpClientService to return our HTTP client
        _mockHttpService = new Mock<IHttpClientService>();
        _mockHttpService.Setup(x => x.Client).Returns(_httpClient);
        
        // Create options with test certificate
        var options = new XiansOptions
        {
            ApiKey = Xians.Lib.Tests.TestUtilities.TestCertificateGenerator.GenerateTestCertificateBase64("test-tenant", "test-user"),
            ServerUrl = "http://localhost"
        };
        
        // Create agent using internal constructor (now accessible via InternalsVisibleTo)
        _agent = new XiansAgent(
            "test-agent",
            false,
            null,
            null,
            _mockHttpService.Object,
            options,
            null); // No cache for unit tests
        
        _knowledgeCollection = _agent.Knowledge;
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
        XiansContext.CleanupForTests();
    }

    [Fact]
    public async Task GetAsync_WithValidKnowledge_ReturnsKnowledge()
    {
        // Arrange
        var expectedKnowledge = new Knowledge
        {
            Id = "123",
            Name = "test-knowledge",
            Content = "Test content",
            Type = "instruction",
            Agent = "test-agent",
            CreatedAt = DateTime.UtcNow
        };

        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedKnowledge)
            });

        // Act
        var result = await _knowledgeCollection.GetAsync("test-knowledge");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-knowledge", result.Name);
        Assert.Equal("Test content", result.Content);
    }

    [Fact]
    public async Task GetAsync_WithNotFound_ReturnsNull()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Act
        var result = await _knowledgeCollection.GetAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WithNullOrEmpty_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _knowledgeCollection.GetAsync(null!));
        
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _knowledgeCollection.GetAsync(""));
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ReturnsTrue()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var result = await _knowledgeCollection.UpdateAsync("config", "content", "json");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingKnowledge_ReturnsTrue()
    {
        // Arrange
        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Act
        var result = await _knowledgeCollection.DeleteAsync("old-knowledge");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ListAsync_WithKnowledge_ReturnsList()
    {
        // Arrange
        var knowledgeList = new List<Knowledge>
        {
            new Knowledge { Name = "k1", Content = "c1" },
            new Knowledge { Name = "k2", Content = "c2" }
        };

        _httpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(knowledgeList)
            });

        // Act
        var result = await _knowledgeCollection.ListAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }
}
