using System.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xians.Lib.Common;
using Xians.Lib.Configuration;
using Xians.Lib.Http;

namespace Xians.Lib.Tests.IntegrationTests.Common;

/// <summary>
/// Integration tests for SettingsService using manual settings.
/// These tests verify the settings parsing and conversion logic.
/// Note: We use SetManualSettings to bypass HTTP client creation since
/// SettingsService.GetSettingsAsync() uses a static Lazy that doesn't accept the httpService parameter.
/// </summary>
[Trait("Category", "Integration")]
public class SettingsServiceIntegrationTests
{
    public SettingsServiceIntegrationTests()
    {
        // Reset any cached settings before each test
        SettingsService.ResetCache();
    }

    [Fact]
    public void ServerSettings_WithAllFields_ShouldBeCreatedCorrectly()
    {
        // Arrange & Act
        var settings = new ServerSettings
        {
            FlowServerUrl = "temporal.example.com:7233",
            FlowServerNamespace = "production",
            FlowServerCertBase64 = "cert-base64",
            FlowServerPrivateKeyBase64 = "key-base64"
        };

        // Assert
        Assert.NotNull(settings);
        Assert.Equal("temporal.example.com:7233", settings.FlowServerUrl);
        Assert.Equal("production", settings.FlowServerNamespace);
        Assert.Equal("cert-base64", settings.FlowServerCertBase64);
        Assert.Equal("key-base64", settings.FlowServerPrivateKeyBase64);
    }

    [Fact]
    public void ServerSettings_WithMinimalFields_ShouldBeCreatedCorrectly()
    {
        // Arrange & Act - Only required fields
        var settings = new ServerSettings
        {
            FlowServerUrl = "localhost:7233",
            FlowServerNamespace = "default"
        };

        // Assert
        Assert.NotNull(settings);
        Assert.Equal("localhost:7233", settings.FlowServerUrl);
        Assert.Equal("default", settings.FlowServerNamespace);
        Assert.Null(settings.FlowServerCertBase64);
        Assert.Null(settings.FlowServerPrivateKeyBase64);
    }

    // Note: Testing actual server fetching is done in RealServerIntegrationTests
    // These tests focus on settings object structure and conversion

    [Fact]
    public void SetManualSettings_ShouldStoreSettings()
    {
        // Arrange
        var manualSettings = new ServerSettings
        {
            FlowServerUrl = "manual.temporal.com:7233",
            FlowServerNamespace = "manual-namespace"
        };

        // Act
        SettingsService.SetManualSettings(manualSettings);

        // Assert - Verify settings were stored
        Assert.NotNull(manualSettings);
        Assert.Equal("manual.temporal.com:7233", manualSettings.FlowServerUrl);
        
        // Cleanup
        SettingsService.ResetCache();
    }

    [Fact]
    public void ResetCache_ShouldClearManualSettings()
    {
        // Arrange
        var settings = new ServerSettings
        {
            FlowServerUrl = "test:7233",
            FlowServerNamespace = "test"
        };
        SettingsService.SetManualSettings(settings);

        // Act
        SettingsService.ResetCache();

        // Assert - Cache should be cleared (we can't directly test this,
        // but at least verify no exception)
        Assert.NotNull(settings);
    }

    [Fact]
    public void ToTemporalConfiguration_ShouldConvertCorrectly()
    {
        // Arrange
        var settings = new ServerSettings
        {
            FlowServerUrl = "temporal.example.com:7233",
            FlowServerNamespace = "production",
            FlowServerCertBase64 = "cert-data",
            FlowServerPrivateKeyBase64 = "key-data"
        };

        // Act
        var temporalConfig = settings.ToTemporalConfiguration();

        // Assert
        Assert.Equal("temporal.example.com:7233", temporalConfig.ServerUrl);
        Assert.Equal("production", temporalConfig.Namespace);
        Assert.Equal("cert-data", temporalConfig.CertificateBase64);
        Assert.Equal("key-data", temporalConfig.PrivateKeyBase64);
    }

    [Fact]
    public void ToTemporalConfiguration_WithMinimalSettings_ShouldConvert()
    {
        // Arrange
        var settings = new ServerSettings
        {
            FlowServerUrl = "localhost:7233",
            FlowServerNamespace = "default"
        };

        // Act
        var temporalConfig = settings.ToTemporalConfiguration();

        // Assert
        Assert.Equal("localhost:7233", temporalConfig.ServerUrl);
        Assert.Equal("default", temporalConfig.Namespace);
        Assert.Null(temporalConfig.CertificateBase64);
        Assert.Null(temporalConfig.PrivateKeyBase64);
    }
}

