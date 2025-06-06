// using System.Net;
// using System.Reflection;
// using Microsoft.Extensions.Logging;
// using Server;
// using DotNetEnv;
// using XiansAi.Knowledge;

// namespace XiansAi.Lib.Tests.IntegrationTests;

// [Collection("SecureApi Tests")]
// public class InstructionLoaderTests
// {
//     private readonly ILoggerFactory _loggerFactory;
//     private readonly KnowledgeLoaderImpl _knowledgeLoader;
//     private readonly string _certificateBase64;
//     private readonly string _serverUrl;
//     private readonly ILogger<InstructionLoaderTests> _logger;

//     /*
//     dotnet test --filter "FullyQualifiedName~InstructionLoaderTests"
//     */
//     public InstructionLoaderTests()
//     {
//         // Reset SecureApi to ensure clean state
//         SecureApi.Reset();

//         // Load environment variables
//         Env.Load();

//         // Get values from environment for SecureApi
//         _certificateBase64 = Environment.GetEnvironmentVariable("APP_SERVER_API_KEY") ?? 
//             throw new InvalidOperationException("APP_SERVER_API_KEY environment variable is not set");
//         _serverUrl = Environment.GetEnvironmentVariable("APP_SERVER_URL") ?? 
//             throw new InvalidOperationException("APP_SERVER_URL environment variable is not set");

//         // Set up logger
//         _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
//         _logger = _loggerFactory.CreateLogger<InstructionLoaderTests>();

//         // Initialize SecureApi with real credentials
//         SecureApi.InitializeClient(_certificateBase64, _serverUrl, forceReinitialize: true);
//         var secureApiClient = SecureApi.Instance;

//         // Create the instruction loader with real SecureApi
//         _knowledgeLoader = new KnowledgeLoaderImpl();

//         AgentContext.AgentName = "test-agent";
//     }

//     /*
//     dotnet test --filter "FullyQualifiedName~InstructionLoaderTests.Load_ShouldLoadFromServer_WhenApiClientIsReady"
//     */
//     [Fact]
//     public async Task Load_ShouldLoadFromServer_WhenApiClientIsReady()
//     {
//         // Arrange - use a known instruction name that exists on the server
//         string instructionName = "How To Collect Links"; // Assuming "system" instruction exists

//         // Act
//         var result = await _knowledgeLoader.Load(instructionName);

//         _logger.LogInformation($"Loaded instruction: {result?.Content}");

//         // Assert
//         Assert.NotNull(result);
//         Assert.Equal(instructionName, result.Name);
//         Assert.NotEmpty(result.Content);
//         _logger.LogInformation($"Successfully loaded instruction '{instructionName}' from server");
//     }

//     /*
//     dotnet test --filter "FullyQualifiedName~InstructionLoaderTests.Load_ShouldReturnNull_WhenInstructionNotFoundOnServer"
//     */
//     [Fact]
//     public async Task Load_ShouldReturnNull_WhenInstructionNotFoundOnServer()
//     {
//         // Arrange - use a non-existent instruction name
//         string instructionName = "non-existent-instruction-" + Guid.NewGuid();

//         // Act
//         var result = await _knowledgeLoader.Load(instructionName);

//         // Assert
//         Assert.Null(result);
//         _logger.LogInformation($"Correctly returned null for non-existent instruction '{instructionName}'");
//     }

//     /*
//     dotnet test --filter "FullyQualifiedName~InstructionLoaderTests.Load_ShouldThrowArgumentException_WhenInstructionNameIsEmpty"
//     */
//     [Fact]
//     public async Task Load_ShouldThrowArgumentException_WhenInstructionNameIsEmpty()
//     {
//         // Act & Assert
//         await Assert.ThrowsAsync<ArgumentException>(() => _knowledgeLoader.Load(string.Empty));
//         _logger.LogInformation("Correctly threw ArgumentException for empty instruction name");
//     }

// } 