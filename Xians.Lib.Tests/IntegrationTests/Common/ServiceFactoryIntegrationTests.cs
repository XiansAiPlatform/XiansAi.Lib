using DotNetEnv;
using Xians.Lib.Common;
using Xians.Lib.Configuration;
using Xians.Lib.Tests.TestUtilities;

namespace Xians.Lib.Tests.IntegrationTests.Common;

[Trait("Category", "Integration")]
public class ServiceFactoryIntegrationTests
{

    [Fact]
    public void CreateHttpClientServiceFromEnvironment_WithValidEnvVars_ShouldCreateService()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SERVER_URL", "https://test.example.com");
        Environment.SetEnvironmentVariable("API_KEY", TestCertificateGenerator.GetTestCertificate());

        try
        {
            // Act
            using var service = ServiceFactory.CreateHttpClientServiceFromEnvironment();

            // Assert
            Assert.NotNull(service);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("SERVER_URL", null);
            Environment.SetEnvironmentVariable("API_KEY", null);
        }
    }

    [Fact]
    public void CreateHttpClientService_WithValidConfig_ShouldCreateService()
    {
        // Arrange
        var config = new ServerConfiguration
        {
            ServerUrl = "https://test.example.com",
            ApiKey = TestCertificateGenerator.GetTestCertificate()
        };

        // Act
        using var service = ServiceFactory.CreateHttpClientService(config);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void CreateTemporalClientService_WithValidConfig_ShouldCreateService()
    {
        // Arrange
        var config = new TemporalConfiguration
        {
            ServerUrl = "localhost:7233",
            Namespace = "test"
        };

        // Act
        using var service = ServiceFactory.CreateTemporalClientService(config);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void CreateHttpClientService_WithInvalidConfig_ShouldThrow()
    {
        // Arrange
        var config = new ServerConfiguration
        {
            ServerUrl = "",
            ApiKey = "test-key"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            ServiceFactory.CreateHttpClientService(config));
    }
}

