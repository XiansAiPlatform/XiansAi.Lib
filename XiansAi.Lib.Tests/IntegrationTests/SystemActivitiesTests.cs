using Microsoft.Extensions.Logging;
using Server;
using DotNetEnv;
using XiansAi.Server.Base;
using XiansAi.Models;
using XiansAi.Messaging;

namespace XiansAi.Lib.Tests.IntegrationTests;

[Collection("SecureApi Tests")]
public class SystemActivitiesTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly SystemActivities _systemActivities;
    private readonly ThreadHistoryService _threadHistoryService;
    private readonly string _certificateBase64;
    private readonly string _serverUrl;
    private readonly ILogger<SystemActivitiesTests> _logger;

    /*
    dotnet test --filter "FullyQualifiedName~SystemActivitiesTests"
    */
    public SystemActivitiesTests()
    {
        // Reset SecureApi to ensure clean state
        SecureApi.Reset();

        // Load environment variables
        Env.Load();

        // Get values from environment for SecureApi
        _certificateBase64 = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY") ?? 
            throw new InvalidOperationException("APP_SERVER_API_KEY environment variable is not set");
        _serverUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL") ?? 
            throw new InvalidOperationException("APP_SERVER_URL environment variable is not set");

        // Set up logger
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<SystemActivitiesTests>();

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

        // Create the correct logger type for SystemActivities
        var systemActivitiesLogger = _loggerFactory.CreateLogger<SystemActivities>();

        // Create the system activities instance with IApiService
        _systemActivities = new SystemActivities(apiService, systemActivitiesLogger);
        _threadHistoryService = new ThreadHistoryService();
    }

    [Fact]
    public async Task SystemActivities_GetMessageHistoryAsync_ShouldReturnMessages()
    {
        // Arrange
        var workflowType = "test-workflow";
        var participantId = "test-participant";

        // Act & Assert - Should not throw
        var messages = await _systemActivities.GetMessageHistoryAsync(workflowType, participantId, 1, 5);
        
        // Should return a list (may be empty)
        Assert.NotNull(messages);
        _logger.LogInformation("Retrieved {Count} messages from history", messages.Count);
    }

    [Fact]
    public void SystemActivities_LegacyConstructor_ShouldWork()
    {
        // Arrange
        var capabilities = new List<Type>();

        // Act & Assert - Should not throw
        var systemActivities = new SystemActivities(capabilities);
        Assert.NotNull(systemActivities);
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