using Microsoft.Extensions.Logging;
using Server;
using DotNetEnv;
using System.Reflection;

namespace XiansAi.Lib.Tests.IntegrationTests;

[Collection("SecureApi Tests")]
public class SecureApiTests 
{
    private readonly string _certificateBase64;
    private readonly string _serverUrl;
    private readonly ILogger<SecureApiTests> _logger;

    /*
    dotnet test --filter "FullyQualifiedName~SecureApiTests"
    */
    public SecureApiTests()
    {
        // Reset SecureApi to ensure clean state
        SecureApi.Reset();

        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<SecureApiTests>();
            
        // Load environment variables from .env file
        Env.Load();
        
        // Get values from environment
        _certificateBase64 = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY") ?? 
            throw new InvalidOperationException("CERTIFICATE_BASE64 environment variable is not set");
        _serverUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL") ?? 
            throw new InvalidOperationException("SERVER_URL environment variable is not set");
    }

    /*
    dotnet test --filter "FullyQualifiedName~SecureApiTests.IntegrationTests.InitializeClient_ShouldReturnHttpClient_WhenValidParametersProvided"
    */
    [Fact]
    public void InitializeClient_ShouldReturnHttpClient_WhenValidParametersProvided()
    {
        // Act
        var client = SecureApi.InitializeClient(_certificateBase64, _serverUrl, forceReinitialize: true);

        // Assert
        Assert.NotNull(client);
        Assert.Equal(new Uri(_serverUrl), client.BaseAddress);
        Assert.True(client.DefaultRequestHeaders.Contains("Authorization"));
    }

    /*
    dotnet test --filter "FullyQualifiedName~SecureApiTests.IntegrationTests.Instance_ShouldReturnInitializedClient_AfterInitialization"
    */
    [Fact]
    public void Instance_ShouldReturnInitializedClient_AfterInitialization()
    {
        // Arrange
        SecureApi.InitializeClient(_certificateBase64, _serverUrl);

        // Act
        var instance = SecureApi.Instance;

        // Assert
        Assert.NotNull(instance);
        Assert.NotNull(instance.Client);
    }

    /*
    dotnet test --filter "FullyQualifiedName~SecureApiTests.IntegrationTests.InitializeClient_ShouldThrowException_WhenCertificateIsEmpty"
    */
    [Fact]
    public void InitializeClient_ShouldThrowException_WhenCertificateIsEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SecureApi.InitializeClient("", _serverUrl));
    }

    /*
    dotnet test --filter "FullyQualifiedName~SecureApiTests.IntegrationTests.InitializeClient_ShouldThrowException_WhenServerUrlIsEmpty"
    */
    [Fact]
    public void InitializeClient_ShouldThrowException_WhenServerUrlIsEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SecureApi.InitializeClient(_certificateBase64, ""));
    }


}

[CollectionDefinition("SecureApi Tests")]
public class SecureApiTestCollection : ICollectionFixture<SecureApiTestFixture>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

public class SecureApiTestFixture : IDisposable
{
    private readonly string _certificateBase64;
    private readonly string _serverUrl;

    public SecureApiTestFixture()
    {
        // Reset SecureApi to ensure clean state
        SecureApi.Reset();

        // Load environment variables
        Env.Load();

        // Get values from environment or use test defaults
        _certificateBase64 = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY") ?? "test-api-key";
        _serverUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL") ?? "https://test-server.com";

        // Initialize SecureApi with credentials (real or test)
        SecureApi.InitializeClient(_certificateBase64, _serverUrl, forceReinitialize: true);
    }

    public void Dispose()
    {
        SecureApi.Reset();
    }
} 