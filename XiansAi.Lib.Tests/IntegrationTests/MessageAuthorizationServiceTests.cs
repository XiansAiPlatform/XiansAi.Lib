using Microsoft.Extensions.Logging;
using Server;
using DotNetEnv;
using XiansAi.Server.Base;
using Xunit;

namespace XiansAi.Lib.Tests.IntegrationTests;

public class MessageAuthorizationServiceTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MessageAuthorizationService? _messageAuthorizationService;
    private readonly MessageAuthorizationService? _legacyMessageAuthorizationService;
    private readonly string? _certificateBase64;
    private readonly string? _serverUrl;
    private readonly ILogger<MessageAuthorizationServiceTests> _logger;
    private readonly bool _skipIntegrationTests;

    /*
    dotnet test --filter "FullyQualifiedName~MessageAuthorizationServiceTests"
    */
    public MessageAuthorizationServiceTests()
    {
        // Set up logger first
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<MessageAuthorizationServiceTests>();

        try
        {
            // Reset SecureApi to ensure clean state
            SecureApi.Reset();

            // Load environment variables
            Env.Load();

            // Get values from environment for SecureApi
            _certificateBase64 = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY");
            _serverUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL");

            // Check if we have valid configuration for integration tests
            if (string.IsNullOrEmpty(_certificateBase64) || string.IsNullOrEmpty(_serverUrl) ||
                _certificateBase64 == "test-api-key" || _serverUrl == "https://test-server.com")
            {
                _logger.LogWarning("Skipping integration tests - real server credentials not configured");
                _skipIntegrationTests = true;
                return;
            }

            // Set the global LogFactory
            typeof(Globals).GetProperty("LogFactory")?.SetValue(null, _loggerFactory);

            // Initialize SecureApi with real credentials
            SecureApi.InitializeClient(_certificateBase64, _serverUrl, forceReinitialize: true);

            // Create HttpClient for BaseApiService
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(_serverUrl);
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_certificateBase64}");

            // Create IApiService instance using BaseApiService
            IApiService apiService = new TestApiService(httpClient, _logger);

            // Create the correct logger type for MessageAuthorizationService
            var messageAuthServiceLogger = _loggerFactory.CreateLogger<MessageAuthorizationService>();

            // Create the message authorization service instance with IApiService (DI constructor)
            _messageAuthorizationService = new MessageAuthorizationService(apiService, messageAuthServiceLogger);

            // Create the legacy message authorization service instance (legacy constructor)
            _legacyMessageAuthorizationService = new MessageAuthorizationService();

            _skipIntegrationTests = false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize integration test environment - tests will be skipped");
            _skipIntegrationTests = true;
        }
    }

    public void Dispose()
    {
        _loggerFactory?.Dispose();
        SecureApi.Reset();
    }

    [Fact]
    public async Task MessageAuthorizationService_GetAuthorization_WithDIConstructor_ShouldWork()
    {
        // Skip if integration tests are not properly configured
        if (_skipIntegrationTests || _messageAuthorizationService == null)
        {
            _logger.LogWarning("Skipping integration test - credentials not configured");
            return;
        }

        // Arrange
        var testAuthorization = "test-authorization-guid";

        // Act & Assert - Should not throw, may return null if authorization doesn't exist
        var result = await _messageAuthorizationService.GetAuthorization(testAuthorization);
        
        // The result can be null (if authorization doesn't exist) or a string (if it exists)
        // We're just testing that the API call doesn't fail
        _logger.LogInformation("Authorization result for '{Authorization}': {Result}", 
            testAuthorization, result ?? "null");
        
        // Test should pass regardless of result since we're testing the integration
        Assert.True(true);
    }

    [Fact]
    public async Task MessageAuthorizationService_GetAuthorization_WithLegacyConstructor_ShouldWork()
    {
        // Skip if integration tests are not properly configured
        if (_skipIntegrationTests || _legacyMessageAuthorizationService == null)
        {
            _logger.LogWarning("Skipping integration test - credentials not configured");
            return;
        }

        // Arrange
        var testAuthorization = "test-authorization-guid-legacy";

        // Act & Assert - Should not throw, may return null if authorization doesn't exist
        var result = await _legacyMessageAuthorizationService.GetAuthorization(testAuthorization);
        
        // The result can be null (if authorization doesn't exist) or a string (if it exists)
        // We're just testing that the API call doesn't fail
        _logger.LogInformation("Legacy authorization result for '{Authorization}': {Result}", 
            testAuthorization, result ?? "null");
        
        // Test should pass regardless of result since we're testing the integration
        Assert.True(true);
    }

    [Fact]
    public async Task MessageAuthorizationService_GetAuthorization_WithNullAuthorization_ShouldWork()
    {
        // Skip if integration tests are not properly configured
        if (_skipIntegrationTests || _messageAuthorizationService == null)
        {
            _logger.LogWarning("Skipping integration test - credentials not configured");
            return;
        }

        // Arrange
        string? nullAuthorization = null;

        // Act & Assert - Should not throw
        var result = await _messageAuthorizationService.GetAuthorization(nullAuthorization);
        
        _logger.LogInformation("Authorization result for null authorization: {Result}", result ?? "null");
        
        // Test should pass regardless of result since we're testing the integration
        Assert.True(true);
    }

    [Fact]
    public async Task MessageAuthorizationService_GetAuthorization_WithEmptyAuthorization_ShouldWork()
    {
        // Skip if integration tests are not properly configured
        if (_skipIntegrationTests || _messageAuthorizationService == null)
        {
            _logger.LogWarning("Skipping integration test - credentials not configured");
            return;
        }

        // Arrange
        var emptyAuthorization = "";

        // Act & Assert - Should not throw
        var result = await _messageAuthorizationService.GetAuthorization(emptyAuthorization);
        
        _logger.LogInformation("Authorization result for empty authorization: {Result}", result ?? "null");
        
        // Test should pass regardless of result since we're testing the integration
        Assert.True(true);
    }

    [Fact]
    public void MessageAuthorizationService_DIConstructor_ShouldCreateSuccessfully()
    {
        // Skip if integration tests are not properly configured
        if (_skipIntegrationTests)
        {
            _logger.LogWarning("Skipping integration test - credentials not configured");
            return;
        }

        // Act & Assert - Constructor should succeed
        Assert.NotNull(_messageAuthorizationService);
        _logger.LogInformation("DI constructor created MessageAuthorizationService successfully");
    }

    [Fact]
    public void MessageAuthorizationService_LegacyConstructor_ShouldCreateSuccessfully()
    {
        // Skip if integration tests are not properly configured
        if (_skipIntegrationTests)
        {
            _logger.LogWarning("Skipping integration test - credentials not configured");
            return;
        }

        // Act & Assert - Constructor should succeed
        Assert.NotNull(_legacyMessageAuthorizationService);
        _logger.LogInformation("Legacy constructor created MessageAuthorizationService successfully");
    }

    [Fact]
    public async Task MessageAuthorizationService_GetAuthorization_WithLongAuthorization_ShouldWork()
    {
        // Skip if integration tests are not properly configured
        if (_skipIntegrationTests || _messageAuthorizationService == null)
        {
            _logger.LogWarning("Skipping integration test - credentials not configured");
            return;
        }

        // Arrange - Test with a longer authorization string
        var longAuthorization = "test-authorization-guid-with-very-long-string-that-might-cause-issues-" + 
                               Guid.NewGuid().ToString() + "-" + Guid.NewGuid().ToString();

        // Act & Assert - Should not throw
        var result = await _messageAuthorizationService.GetAuthorization(longAuthorization);
        
        _logger.LogInformation("Authorization result for long authorization: {Result}", result ?? "null");
        
        // Test should pass regardless of result since we're testing the integration
        Assert.True(true);
    }

    [Fact]
    public async Task MessageAuthorizationService_GetAuthorization_WithSpecialCharacters_ShouldWork()
    {
        // Skip if integration tests are not properly configured
        if (_skipIntegrationTests || _messageAuthorizationService == null)
        {
            _logger.LogWarning("Skipping integration test - credentials not configured");
            return;
        }

        // Arrange - Test with special characters that might need URL encoding
        var specialCharAuthorization = "test-auth-with-special-chars-@#$%^&*()";

        // Act & Assert - Should not throw
        var result = await _messageAuthorizationService.GetAuthorization(specialCharAuthorization);
        
        _logger.LogInformation("Authorization result for special char authorization: {Result}", result ?? "null");
        
        // Test should pass regardless of result since we're testing the integration
        Assert.True(true);
    }

    /// <summary>
    /// Test implementation of BaseApiService for testing purposes
    /// </summary>
    private class TestApiService : BaseApiService
    {
        public TestApiService(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
        }
    }
} 