using Xians.Lib.Configuration;

namespace Xians.Lib.Tests.UnitTests.Configuration;

public class ServerConfigurationTests
{
    [Fact]
    public void Constructor_WithValidUrl_ShouldSetProperties()
    {
        // Arrange & Act
        var config = new ServerConfiguration
        {
            ServerUrl = "https://api.example.com",
            ApiKey = "test-key"
        };

        // Assert
        Assert.Equal("https://api.example.com", config.ServerUrl);
        Assert.Equal("test-key", config.ApiKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithInvalidServerUrl_ShouldThrow(string? serverUrl)
    {
        // Arrange
        var config = new ServerConfiguration
        {
            ServerUrl = serverUrl!,
            ApiKey = "test-key"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithoutApiKeyOrCertificate_ShouldThrow(string? apiKey)
    {
        // Arrange
        var config = new ServerConfiguration
        {
            ServerUrl = "https://api.example.com",
            ApiKey = apiKey!
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithValidConfiguration_ShouldNotThrow()
    {
        // Arrange
        var config = new ServerConfiguration
        {
            ServerUrl = "https://api.example.com",
            ApiKey = "valid-key"
        };

        // Act & Assert
        var exception = Record.Exception(() => config.Validate());
        Assert.Null(exception);
    }
}

