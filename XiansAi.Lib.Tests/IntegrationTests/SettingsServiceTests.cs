using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Server;
using XiansAi.Server.Interfaces;
using XiansAi.Server.Extensions;
using DotNetEnv;
using System.Net;
using System.Text.Json;
using System.Reflection;
using Moq;
using Moq.Protected;
using XiansAi;

namespace XiansAi.Lib.Tests.IntegrationTests;

[Collection("SettingsService Tests")]
public class SettingsServiceTests : IDisposable
{
    private readonly string _serverUrl;
    private readonly string _apiKey;
    private readonly ILogger<SettingsServiceTests> _logger;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SettingsService> _serviceLogger;
    private readonly FlowServerSettings _testSettings;

    public SettingsServiceTests()
    {
        // Load environment variables
        Env.Load();
        
        var envServerUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL");
        var envApiKey = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY");
        
        // Use test defaults if environment variables are not set or empty
        _serverUrl = !string.IsNullOrWhiteSpace(envServerUrl) ? envServerUrl : "https://test-server.com";
        _apiKey = !string.IsNullOrWhiteSpace(envApiKey) ? envApiKey : "test-api-key";

        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<SettingsServiceTests>();
        
        _serviceLogger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<SettingsService>();

        // Create test settings
        _testSettings = new FlowServerSettings
        {
            FlowServerUrl = "https://test-flow-server.com",
            FlowServerNamespace = "test-namespace",
            FlowServerCertBase64 = "dGVzdC1jZXJ0aWZpY2F0ZQ==", // base64 encoded "test-certificate"
            FlowServerPrivateKeyBase64 = "dGVzdC1wcml2YXRlLWtleQ==", // base64 encoded "test-private-key"
            OpenAIApiKey = "test-openai-key",
            ModelName = "gpt-4"
        };

        // Setup mock HTTP handler
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object)
        {
            BaseAddress = new Uri(_serverUrl)
        };

        // Create real memory cache for testing
        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100
        });

        // Reset factory state
        XiansAiServiceFactory.Reset();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _memoryCache?.Dispose();
        XiansAiServiceFactory.Reset();
    }

    #region Service Creation Tests

    [Fact]
    public void CreateSettingsService_ShouldSucceed_WithValidParameters()
    {
        // Act
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void CreateSettingsService_ShouldThrowArgumentNullException_WhenHttpClientIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SettingsService(null!, _memoryCache, _serviceLogger));
    }

    [Fact]
    public void CreateSettingsService_ShouldThrowArgumentNullException_WhenCacheIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SettingsService(_httpClient, null!, _serviceLogger));
    }

    #endregion

    #region Factory Pattern Tests

    [Fact]
    public void XiansAiServiceFactory_GetSettingsService_ShouldReturnSameInstance()
    {
        // Arrange - Set environment variables for the factory
        Environment.SetEnvironmentVariable("APP_SERVER_URL", _serverUrl);
        Environment.SetEnvironmentVariable("APP_SERVER_API_KEY", _apiKey);
        
        try
        {
            // Act
            var service1 = XiansAiServiceFactory.GetSettingsService();
            var service2 = XiansAiServiceFactory.GetSettingsService();

            // Assert
            Assert.Same(service1, service2);
        }
        finally
        {
            // Clean up
            XiansAiServiceFactory.Reset();
        }
    }

    [Fact]
    public void XiansAiServiceFactory_GetSettingsService_ShouldThrowException_WhenAppServerUrlNotSet()
    {
        // Arrange
        var originalValue = typeof(PlatformConfig)
            .GetField("APP_SERVER_URL", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null);

        try
        {
            // Act & Assert - Set APP_SERVER_URL to null using reflection
            typeof(PlatformConfig)
                .GetField("APP_SERVER_URL", BindingFlags.Public | BindingFlags.Static)
                ?.SetValue(null, null);

            var exception = Assert.Throws<InvalidOperationException>(() => 
                XiansAiServiceFactory.GetSettingsService());
            
            Assert.Contains("Server URL is required", exception.Message);
        }
        finally
        {
            // Restore original value
            typeof(PlatformConfig)
                .GetField("APP_SERVER_URL", BindingFlags.Public | BindingFlags.Static)
                ?.SetValue(null, originalValue);
        }
    }

    [Fact]
    public void XiansAiServiceFactory_Reset_ShouldClearInstance()
    {
        // Arrange - Set environment variables for the factory
        Environment.SetEnvironmentVariable("APP_SERVER_URL", _serverUrl);
        Environment.SetEnvironmentVariable("APP_SERVER_API_KEY", _apiKey);
        
        try
        {
            var service1 = XiansAiServiceFactory.GetSettingsService();

            // Act
            XiansAiServiceFactory.Reset();
            var service2 = XiansAiServiceFactory.GetSettingsService();

            // Assert
            Assert.NotSame(service1, service2);
        }
        finally
        {
            // Clean up
            XiansAiServiceFactory.Reset();
        }
    }

    #endregion

    #region Settings Retrieval Tests

    [Fact]
    public async Task GetFlowServerSettingsAsync_ShouldReturnSettings_WhenServerRespondsSuccessfully()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);
        SetupSuccessfulHttpResponse();

        // Act
        var result = await service.GetFlowServerSettingsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_testSettings.FlowServerUrl, result.FlowServerUrl);
        Assert.Equal(_testSettings.FlowServerNamespace, result.FlowServerNamespace);
        Assert.Equal(_testSettings.OpenAIApiKey, result.OpenAIApiKey);
        Assert.Equal(_testSettings.ModelName, result.ModelName);
    }

    [Fact]
    public async Task GetFlowServerSettingsAsync_ShouldThrowHttpRequestException_WhenServerReturnsError()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);
        SetupErrorHttpResponse(HttpStatusCode.InternalServerError);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => 
            service.GetFlowServerSettingsAsync());
        
        Assert.Contains("Failed to get data from", exception.Message);
    }

    [Fact]
    public async Task GetFlowServerSettingsAsync_ShouldThrowJsonException_WhenResponseIsInvalidJson()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);
        SetupInvalidJsonResponse();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<JsonException>(() => 
            service.GetFlowServerSettingsAsync());
        
        Assert.Contains("'I' is an invalid start of a value", exception.Message);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GetFlowServerSettingsAsync_ShouldUseCache_OnSecondCall()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);
        SetupSuccessfulHttpResponse();

        // Act
        var result1 = await service.GetFlowServerSettingsAsync();
        var result2 = await service.GetFlowServerSettingsAsync();

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(result1.FlowServerUrl, result2.FlowServerUrl);
        
        // Verify HTTP was called only once
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetCachedSettings_ShouldReturnNull_WhenNothingCached()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);

        // Act
        var result = service.GetCachedSettings();

        // Assert
        Assert.Null(result);
        await Task.CompletedTask; // Make method properly async
    }

    [Fact]
    public async Task GetCachedSettings_ShouldReturnSettings_WhenCached()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);
        SetupSuccessfulHttpResponse();

        // Act
        await service.GetFlowServerSettingsAsync(); // This should cache the settings
        var cachedResult = service.GetCachedSettings();

        // Assert
        Assert.NotNull(cachedResult);
        Assert.Equal(_testSettings.FlowServerUrl, cachedResult.FlowServerUrl);
    }

    [Fact]
    public async Task RefreshSettingsAsync_ShouldInvalidateCache_AndReloadFromServer()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);
        SetupSuccessfulHttpResponse();

        // Act
        await service.GetFlowServerSettingsAsync(); // Initial load and cache
        var cachedBefore = service.GetCachedSettings();
        
        await service.RefreshSettingsAsync(); // Should clear cache and reload
        var cachedAfter = service.GetCachedSettings();

        // Assert
        Assert.NotNull(cachedBefore);
        Assert.NotNull(cachedAfter);
        
        // Verify HTTP was called twice (initial + refresh)
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task GetFlowServerSettingsAsync_ShouldHandleConcurrentRequests_WithoutMultipleServerCalls()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);
        SetupDelayedSuccessfulHttpResponse(TimeSpan.FromMilliseconds(100));

        // Act - Make multiple concurrent requests
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => service.GetFlowServerSettingsAsync())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result => 
        {
            Assert.NotNull(result);
            Assert.Equal(_testSettings.FlowServerUrl, result.FlowServerUrl);
        });

        // Verify HTTP was called only once despite concurrent requests
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetFlowServerSettingsAsync_ShouldHandleConcurrentRefresh_Safely()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);
        SetupSuccessfulHttpResponse();

        // Act - Initial load
        await service.GetFlowServerSettingsAsync();

        // Act - Concurrent refresh and get operations
        var refreshTask = service.RefreshSettingsAsync();
        var getTask1 = service.GetFlowServerSettingsAsync();
        var getTask2 = service.GetFlowServerSettingsAsync();

        await Task.WhenAll(refreshTask, getTask1, getTask2);

        // Assert - Should not throw exceptions
        Assert.True(refreshTask.IsCompletedSuccessfully);
        Assert.True(getTask1.IsCompletedSuccessfully);
        Assert.True(getTask2.IsCompletedSuccessfully);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetFlowServerSettingsAsync_ShouldThrowException_WhenHttpClientThrows()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);
        SetupHttpClientException();

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => 
            service.GetFlowServerSettingsAsync());
    }

    [Fact]
    public async Task GetFlowServerSettingsAsync_ShouldThrowException_WhenTimeoutOccurs()
    {
        // Arrange
        var service = new SettingsService(_httpClient, _memoryCache, _serviceLogger);
        SetupTimeoutResponse();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => 
            service.GetFlowServerSettingsAsync());
    }

    #endregion

    #region Integration Tests with Real Server

    [Fact]
    public async Task GetFlowServerSettingsAsync_IntegrationTest_WithRealServer()
    {
        // Arrange - Skip if no real server configured
        var realServerUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL");
        var realApiKey = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY");
        
        if (string.IsNullOrEmpty(realServerUrl) || string.IsNullOrEmpty(realApiKey) || 
            realServerUrl == "https://test-server.com" || realApiKey == "test-api-key")
        {
            _logger.LogWarning("Skipping integration test - real server credentials not configured");
            return;
        }

        // Initialize SecureApi for real server communication
        SecureApi.InitializeClient(realApiKey, realServerUrl, forceReinitialize: true);
        
        var realService = XiansAiServiceFactory.GetSettingsService();

        // Act
        var settings = await realService.GetFlowServerSettingsAsync();

        // Assert
        Assert.NotNull(settings);
        Assert.NotNull(settings.FlowServerUrl);
        Assert.NotNull(settings.FlowServerNamespace);
        Assert.NotNull(settings.OpenAIApiKey);
        Assert.NotNull(settings.ModelName);
        
        _logger.LogInformation("Integration test successful. Server: {ServerUrl}", settings.FlowServerUrl);
    }

    [Fact]
    public async Task RefreshSettingsAsync_IntegrationTest_WithRealServer()
    {
        // Arrange - Skip if no real server configured
        var realServerUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL");
        var realApiKey = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY");
        
        if (string.IsNullOrEmpty(realServerUrl) || string.IsNullOrEmpty(realApiKey) || 
            realServerUrl == "https://test-server.com" || realApiKey == "test-api-key")
        {
            _logger.LogWarning("Skipping integration test - real server credentials not configured");
            return;
        }

        // Initialize SecureApi for real server communication
        SecureApi.InitializeClient(realApiKey, realServerUrl, forceReinitialize: true);
        
        var realService = XiansAiServiceFactory.GetSettingsService();

        // Act
        var initialSettings = await realService.GetFlowServerSettingsAsync();
        await realService.RefreshSettingsAsync();
        var refreshedSettings = await realService.GetFlowServerSettingsAsync();

        // Assert
        Assert.NotNull(initialSettings);
        Assert.NotNull(refreshedSettings);
        Assert.Equal(initialSettings.FlowServerUrl, refreshedSettings.FlowServerUrl);
        
        _logger.LogInformation("Refresh integration test successful");
    }

    #endregion

    #region Helper Methods

    private void SetupSuccessfulHttpResponse()
    {
        var jsonResponse = JsonSerializer.Serialize(_testSettings, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void SetupDelayedSuccessfulHttpResponse(TimeSpan delay)
    {
        var jsonResponse = JsonSerializer.Serialize(_testSettings, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async () =>
            {
                await Task.Delay(delay);
                return response;
            });
    }

    private void SetupErrorHttpResponse(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("Server Error", System.Text.Encoding.UTF8, "text/plain")
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void SetupInvalidJsonResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Invalid JSON {", System.Text.Encoding.UTF8, "application/json")
        };

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void SetupHttpClientException()
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));
    }

    private void SetupTimeoutResponse()
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));
    }

    #endregion
}

[CollectionDefinition("SettingsService Tests")]
public class SettingsServiceTestCollection : ICollectionFixture<SettingsServiceTestFixture>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

public class SettingsServiceTestFixture : IDisposable
{
    public SettingsServiceTestFixture()
    {
        // Load environment variables
        Env.Load();
        
        // Reset factory state
        XiansAiServiceFactory.Reset();
        
        // Initialize SecureApi if credentials are available
        var serverUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL");
        var apiKey = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY");
        
        if (!string.IsNullOrEmpty(serverUrl) && !string.IsNullOrEmpty(apiKey))
        {
            SecureApi.InitializeClient(apiKey, serverUrl, forceReinitialize: true);
        }
    }

    public void Dispose()
    {
        XiansAiServiceFactory.Reset();
        SecureApi.Reset();
    }
} 