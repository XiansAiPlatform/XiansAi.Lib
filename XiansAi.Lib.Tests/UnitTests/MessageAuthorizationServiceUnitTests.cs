using Microsoft.Extensions.Logging;
using Moq;
using XiansAi.Server.Base;
using Xunit;

namespace Server.Tests;

public class MessageAuthorizationServiceUnitTests
{
    private readonly Mock<IApiService> _mockApiService;
    private readonly Mock<ILogger<MessageAuthorizationService>> _mockLogger;
    private readonly MessageAuthorizationService _messageAuthorizationService;

    public MessageAuthorizationServiceUnitTests()
    {
        _mockApiService = new Mock<IApiService>();
        _mockLogger = new Mock<ILogger<MessageAuthorizationService>>();
        _messageAuthorizationService = new MessageAuthorizationService(_mockApiService.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithDependencies_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var service = new MessageAuthorizationService(_mockApiService.Object, _mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullApiService_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MessageAuthorizationService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MessageAuthorizationService(_mockApiService.Object, null!));
    }

    [Fact]
    public async Task GetAuthorization_WithValidAuthorization_ShouldReturnToken()
    {
        // Arrange
        var authorization = "test-auth-guid";
        var expectedToken = "\"test-token-value\"";
        var expectedUrl = "api/agent/conversation/authorization/test-auth-guid";

        _mockApiService
            .Setup(x => x.GetAsync<string>(expectedUrl))
            .ReturnsAsync(expectedToken)
            .Verifiable();

        // Act
        var result = await _messageAuthorizationService.GetAuthorization(authorization);

        // Assert
        Assert.Equal("test-token-value", result);
        _mockApiService.Verify(x => x.GetAsync<string>(expectedUrl), Times.Once);
    }

    [Fact]
    public async Task GetAuthorization_WithNullAuthorization_ShouldReturnToken()
    {
        // Arrange
        string? authorization = null;
        var expectedToken = "\"test-token-value\"";
        var expectedUrl = "api/agent/conversation/authorization/";

        _mockApiService
            .Setup(x => x.GetAsync<string>(expectedUrl))
            .ReturnsAsync(expectedToken)
            .Verifiable();

        // Act
        var result = await _messageAuthorizationService.GetAuthorization(authorization);

        // Assert
        Assert.Equal("test-token-value", result);
        _mockApiService.Verify(x => x.GetAsync<string>(expectedUrl), Times.Once);
    }

    [Fact]
    public async Task GetAuthorization_WithEmptyResponse_ShouldReturnNull()
    {
        // Arrange
        var authorization = "test-auth-guid";
        var expectedUrl = "api/agent/conversation/authorization/test-auth-guid";

        _mockApiService
            .Setup(x => x.GetAsync<string>(expectedUrl))
            .ReturnsAsync("")
            .Verifiable();

        // Act
        var result = await _messageAuthorizationService.GetAuthorization(authorization);

        // Assert
        Assert.Null(result);
        _mockApiService.Verify(x => x.GetAsync<string>(expectedUrl), Times.Once);
    }

    [Fact]
    public async Task GetAuthorization_WithWhitespaceResponse_ShouldReturnNull()
    {
        // Arrange
        var authorization = "test-auth-guid";
        var expectedUrl = "api/agent/conversation/authorization/test-auth-guid";

        _mockApiService
            .Setup(x => x.GetAsync<string>(expectedUrl))
            .ReturnsAsync("   ")
            .Verifiable();

        // Act
        var result = await _messageAuthorizationService.GetAuthorization(authorization);

        // Assert
        Assert.Null(result);
        _mockApiService.Verify(x => x.GetAsync<string>(expectedUrl), Times.Once);
    }

    [Fact]
    public async Task GetAuthorization_WithApiException_ShouldReturnNull()
    {
        // Arrange
        var authorization = "test-auth-guid";
        var expectedUrl = "api/agent/conversation/authorization/test-auth-guid";

        _mockApiService
            .Setup(x => x.GetAsync<string>(expectedUrl))
            .ThrowsAsync(new HttpRequestException("API Error"))
            .Verifiable();

        // Act
        var result = await _messageAuthorizationService.GetAuthorization(authorization);

        // Assert
        Assert.Null(result);
        _mockApiService.Verify(x => x.GetAsync<string>(expectedUrl), Times.Once);
    }

    [Fact]
    public async Task GetAuthorization_WithTokenWithoutQuotes_ShouldReturnTokenAsIs()
    {
        // Arrange
        var authorization = "test-auth-guid";
        var expectedToken = "test-token-value";
        var expectedUrl = "api/agent/conversation/authorization/test-auth-guid";

        _mockApiService
            .Setup(x => x.GetAsync<string>(expectedUrl))
            .ReturnsAsync(expectedToken)
            .Verifiable();

        // Act
        var result = await _messageAuthorizationService.GetAuthorization(authorization);

        // Assert
        Assert.Equal("test-token-value", result);
        _mockApiService.Verify(x => x.GetAsync<string>(expectedUrl), Times.Once);
    }

    [Fact]
    public async Task GetAuthorization_WithTokenWithSingleQuotes_ShouldReturnTokenWithoutQuotes()
    {
        // Arrange
        var authorization = "test-auth-guid";
        var expectedToken = "'test-token-value'";
        var expectedUrl = "api/agent/conversation/authorization/test-auth-guid";

        _mockApiService
            .Setup(x => x.GetAsync<string>(expectedUrl))
            .ReturnsAsync(expectedToken)
            .Verifiable();

        // Act
        var result = await _messageAuthorizationService.GetAuthorization(authorization);

        // Assert
        Assert.Equal("test-token-value", result);
        _mockApiService.Verify(x => x.GetAsync<string>(expectedUrl), Times.Once);
    }
} 