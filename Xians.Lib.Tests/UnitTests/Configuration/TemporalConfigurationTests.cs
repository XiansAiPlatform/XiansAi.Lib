using Xians.Lib.Configuration.Models;

namespace Xians.Lib.Tests.UnitTests.Configuration;

public class TemporalConfigurationTests
{
    [Fact]
    public void Constructor_WithValidConfiguration_ShouldSetProperties()
    {
        // Arrange & Act
        var config = new TemporalConfiguration
        {
            ServerUrl = "localhost:7233",
            Namespace = "default"
        };

        // Assert
        Assert.Equal("localhost:7233", config.ServerUrl);
        Assert.Equal("default", config.Namespace);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithInvalidServerUrl_ShouldThrow(string? serverUrl)
    {
        // Arrange
        var config = new TemporalConfiguration
        {
            ServerUrl = serverUrl!,
            Namespace = "test"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithInvalidNamespace_ShouldThrow(string? namespace_)
    {
        // Arrange
        var config = new TemporalConfiguration
        {
            ServerUrl = "localhost:7233",
            Namespace = namespace_!
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithValidConfiguration_ShouldNotThrow()
    {
        // Arrange
        var config = new TemporalConfiguration
        {
            ServerUrl = "localhost:7233",
            Namespace = "default"
        };

        // Act & Assert
        var exception = Record.Exception(() => config.Validate());
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithMTlsPartialConfig_ShouldThrow()
    {
        // Arrange - only certificate without key
        var config = new TemporalConfiguration
        {
            ServerUrl = "localhost:7233",
            Namespace = "default",
            CertificateBase64 = "cert-data"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_WithCompleteMTlsConfig_ShouldNotThrow()
    {
        // Arrange
        var config = new TemporalConfiguration
        {
            ServerUrl = "localhost:7233",
            Namespace = "default",
            CertificateBase64 = "cert-data",
            PrivateKeyBase64 = "key-data"
        };

        // Act & Assert
        var exception = Record.Exception(() => config.Validate());
        Assert.Null(exception);
    }
}

