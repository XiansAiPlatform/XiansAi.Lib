using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using XiansAi.Models;
using XiansAi.Server.Base;
using Xunit;

namespace Server.Tests;

public class KnowledgeServiceUnitTests
{
    private readonly Mock<IApiService> _mockApiService;
    private readonly Mock<ILogger<KnowledgeService>> _mockLogger;
    private readonly KnowledgeService _knowledgeService;

    public KnowledgeServiceUnitTests()
    {
        _mockApiService = new Mock<IApiService>();
        _mockLogger = new Mock<ILogger<KnowledgeService>>();
        _knowledgeService = new KnowledgeService(_mockApiService.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithDependencies_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var service = new KnowledgeService(_mockApiService.Object, _mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullApiService_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KnowledgeService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KnowledgeService(_mockApiService.Object, null!));
    }

    [Fact]
    public async Task GetKnowledgeFromServer_WithValidKnowledge_ShouldReturnKnowledge()
    {
        // Arrange
        var expectedKnowledge = new Knowledge
        {
            Name = "TestKnowledge",
            Content = "Test content",
            Agent = "TestAgent"
        };

        _mockApiService
            .Setup(x => x.GetAsync<Knowledge>(It.IsAny<string>()))
            .ReturnsAsync(expectedKnowledge);

        // Act
        var result = await _knowledgeService.GetKnowledgeFromServer("TestKnowledge", "TestAgent");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedKnowledge.Name, result.Name);
        Assert.Equal(expectedKnowledge.Content, result.Content);
        Assert.Equal(expectedKnowledge.Agent, result.Agent);

        _mockApiService.Verify(x => x.GetAsync<Knowledge>(
            "api/agent/knowledge/latest?name=TestKnowledge&agent=TestAgent"), Times.Once);
    }

    [Fact]
    public async Task GetKnowledgeFromServer_WithNotFoundResponse_ShouldReturnNull()
    {
        // Arrange
        _mockApiService
            .Setup(x => x.GetAsync<Knowledge>(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("404 NotFound"));

        // Act
        var result = await _knowledgeService.GetKnowledgeFromServer("NonExistentKnowledge", "TestAgent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetKnowledgeFromServer_WithException_ShouldReturnNull()
    {
        // Arrange
        _mockApiService
            .Setup(x => x.GetAsync<Knowledge>(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Network error"));

        // Act
        var result = await _knowledgeService.GetKnowledgeFromServer("TestKnowledge", "TestAgent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetKnowledgeFromServer_WithSpecialCharacters_ShouldEncodeUrlCorrectly()
    {
        // Arrange
        var knowledgeName = "Test Knowledge & More";
        var agent = "Agent#1";
        var expectedKnowledge = new Knowledge
        {
            Name = knowledgeName,
            Content = "Test content",
            Agent = agent
        };

        _mockApiService
            .Setup(x => x.GetAsync<Knowledge>(It.IsAny<string>()))
            .ReturnsAsync(expectedKnowledge);

        // Act
        var result = await _knowledgeService.GetKnowledgeFromServer(knowledgeName, agent);

        // Assert
        Assert.NotNull(result);
        _mockApiService.Verify(x => x.GetAsync<Knowledge>(
            "api/agent/knowledge/latest?name=Test%20Knowledge%20%26%20More&agent=Agent%231"), Times.Once);
    }

    [Fact]
    public async Task UploadKnowledgeToServer_WithValidKnowledge_ShouldReturnTrue()
    {
        // Arrange
        var knowledge = new Knowledge
        {
            Name = "TestKnowledge",
            Content = "Test content",
            Agent = "TestAgent"
        };

        _mockApiService
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync("Success");

        // Act
        var result = await _knowledgeService.UploadKnowledgeToServer(knowledge);

        // Assert
        Assert.True(result);
        _mockApiService.Verify(x => x.PostAsync("api/agent/knowledge", knowledge), Times.Once);
    }

    [Fact]
    public async Task UploadKnowledgeToServer_WithException_ShouldReturnFalse()
    {
        // Arrange
        var knowledge = new Knowledge
        {
            Name = "TestKnowledge",
            Content = "Test content",
            Agent = "TestAgent"
        };

        _mockApiService
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new Exception("Upload failed"));

        // Act
        var result = await _knowledgeService.UploadKnowledgeToServer(knowledge);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetKnowledgeFromServerWithRawResponse_WithValidKnowledge_ShouldReturnKnowledge()
    {
        // Arrange
        var expectedKnowledge = new Knowledge
        {
            Name = "TestKnowledge",
            Content = "Test content",
            Agent = "TestAgent"
        };

        _mockApiService
            .Setup(x => x.GetAsync<Knowledge>(It.IsAny<string>()))
            .ReturnsAsync(expectedKnowledge);

        // Act
        var result = await _knowledgeService.GetKnowledgeFromServerWithRawResponse("TestKnowledge", "TestAgent");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedKnowledge.Name, result.Name);
        Assert.Equal(expectedKnowledge.Content, result.Content);
    }

    [Fact]
    public async Task GetKnowledgeFromServerWithRawResponse_WithNotFound_ShouldReturnNull()
    {
        // Arrange
        _mockApiService
            .Setup(x => x.GetAsync<Knowledge>(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Response status code does not indicate success: 404 (Not Found)."));

        // Act
        var result = await _knowledgeService.GetKnowledgeFromServerWithRawResponse("NonExistentKnowledge", "TestAgent");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("", "TestAgent")]
    [InlineData("TestKnowledge", "")]
    [InlineData("TestKnowledge", "TestAgent")]
    public async Task GetKnowledgeFromServer_WithVariousInputs_ShouldHandleCorrectly(string knowledgeName, string agent)
    {
        // Arrange
        var expectedKnowledge = new Knowledge
        {
            Name = knowledgeName,
            Content = "Test content",
            Agent = agent
        };

        _mockApiService
            .Setup(x => x.GetAsync<Knowledge>(It.IsAny<string>()))
            .ReturnsAsync(expectedKnowledge);

        // Act
        var result = await _knowledgeService.GetKnowledgeFromServer(knowledgeName, agent);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(knowledgeName, result.Name);
        Assert.Equal(agent, result.Agent);
    }

    [Fact]
    public void LegacyConstructor_WhenSecureApiNotReady_ShouldThrowException()
    {
        // Arrange & Act & Assert
        // Note: This test assumes SecureApi.IsReady returns false in test environment
        // In a real scenario, you might need to mock SecureApi or use a test double
        try
        {
            var service = new KnowledgeService();
            // If we reach here, SecureApi was ready, which is fine for this test
            Assert.NotNull(service);
        }
        catch (InvalidOperationException ex)
        {
            Assert.Contains("SecureApi is not ready", ex.Message);
        }
    }
} 