using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Server;
using DotNetEnv;
using XiansAi.Knowledge;

namespace XiansAi.Lib.Tests.IntegrationTests;

[Collection("SecureApi Tests")]
public class InstructionLoaderTests
{
    private readonly bool _runRealServerTests;
    private readonly ILoggerFactory _loggerFactory;
    private readonly KnowledgeLoaderImpl? _knowledgeLoader;
    private readonly string? _certificateBase64;
    private readonly string? _serverUrl;
    private readonly ILogger<InstructionLoaderTests> _logger;

    /*
    dotnet test --filter "FullyQualifiedName~InstructionLoaderTests"
    */
    public InstructionLoaderTests()
    {
        // Load environment variables
        try
        {
            Env.Load();
        }
        catch
        {
            // .env file may not exist
        }

        // Get values from environment for SecureApi
        _certificateBase64 = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY");
        _serverUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL");

        // Only run if we have valid credentials
        _runRealServerTests = !string.IsNullOrEmpty(_certificateBase64) && 
                              !string.IsNullOrEmpty(_serverUrl);

        // Set up logger
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<InstructionLoaderTests>();

        if (_runRealServerTests)
        {
            // Reset SecureApi to ensure clean state
            SecureApi.Reset();

            // Initialize SecureApi with real credentials
            SecureApi.InitializeClient(_certificateBase64!, _serverUrl!, forceReinitialize: true);
            var secureApiClient = SecureApi.Instance;

            // Create the instruction loader with real SecureApi
            _knowledgeLoader = new KnowledgeLoaderImpl();

            // Set up test context with proper workflow ID format: tenantId:agentName:flowName
            AgentContext.SetLocalContext("test-user", "test-tenant:test-agent:test-flow");
        }
    }

    /*
    dotnet test --filter "FullyQualifiedName~InstructionLoaderTests.Load_ShouldLoadFromServer_WhenApiClientIsReady"
    Set APP_SERVER_API_KEY and APP_SERVER_URL environment variables to run this test.
    Note: This test requires a specific instruction to exist on the server.
    */
    [Fact]
    [Trait("Category", "RealServer")]
    public async Task Load_ShouldLoadFromServer_WhenApiClientIsReady()
    {
        if (!_runRealServerTests)
        {
            _logger.LogInformation("Skipping test - APP_SERVER_API_KEY and APP_SERVER_URL not set");
            return;
        }

        // Arrange - use a known instruction name that exists on the server
        string instructionName = "How To Collect Links"; // Known instruction on the test server

        // Act
        var result = await _knowledgeLoader!.Load(instructionName);

        // Assert - skip if instruction doesn't exist on this server instance
        if (result == null)
        {
            _logger.LogWarning($"Skipping test - instruction '{instructionName}' not found on server. This test requires specific server data.");
            return;
        }

        Assert.Equal(instructionName, result.Name);
        Assert.NotEmpty(result.Content);
        _logger.LogInformation($"Successfully loaded instruction '{instructionName}' from server");
    }

    /*
    dotnet test --filter "FullyQualifiedName~InstructionLoaderTests.Load_ShouldReturnNull_WhenInstructionNotFoundOnServer"
    Set APP_SERVER_API_KEY and APP_SERVER_URL environment variables to run this test.
    */
    [Fact]
    [Trait("Category", "RealServer")]
    public async Task Load_ShouldReturnNull_WhenInstructionNotFoundOnServer()
    {
        if (!_runRealServerTests)
        {
            _logger.LogInformation("Skipping test - APP_SERVER_API_KEY and APP_SERVER_URL not set");
            return;
        }

        // Arrange - use a non-existent instruction name
        string instructionName = "non-existent-instruction-" + Guid.NewGuid();

        // Act
        var result = await _knowledgeLoader!.Load(instructionName);

        // Assert
        Assert.Null(result);
        _logger.LogInformation($"Correctly returned null for non-existent instruction '{instructionName}'");
    }

    /*
    dotnet test --filter "FullyQualifiedName~InstructionLoaderTests.Load_ShouldThrowArgumentException_WhenInstructionNameIsEmpty"
    Set APP_SERVER_API_KEY and APP_SERVER_URL environment variables to run this test.
    */
    [Fact]
    [Trait("Category", "RealServer")]
    public async Task Load_ShouldThrowArgumentException_WhenInstructionNameIsEmpty()
    {
        if (!_runRealServerTests)
        {
            _logger.LogInformation("Skipping test - APP_SERVER_API_KEY and APP_SERVER_URL not set");
            return;
        }

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _knowledgeLoader!.Load(string.Empty));
        _logger.LogInformation("Correctly threw ArgumentException for empty instruction name");
    }

} 