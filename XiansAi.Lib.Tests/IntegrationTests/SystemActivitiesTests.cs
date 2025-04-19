using System.Reflection;
using Microsoft.Extensions.Logging;
using Server;
using DotNetEnv;
using XiansAi.Flow;
using XiansAi.Messaging;

namespace XiansAi.Lib.Tests.IntegrationTests;

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
        SecureApi.InitializeClient(_certificateBase64, _serverUrl);

        // Create the system activities instance
        _threadHistoryService = new ThreadHistoryService();
        _systemActivities = new SystemActivities();
    }

    /*
    dotnet test --filter "FullyQualifiedName~SystemActivitiesTests.SendMessage_And_GetMessageHistory_ShouldWorkTogether"
    */
    [Fact]
    public async Task SendMessage_And_GetMessageHistory_ShouldWorkTogether()
    {
        // Arrange - Create test messages
        var message1 = new OutgoingMessage 
        { 
            Content = "Test message 1",
            Metadata = new Dictionary<string, string>(),
            ParticipantId = "test-participant",
            WorkflowIds = ["test-workflow"]
        };
        
        var message2 = new OutgoingMessage 
        { 
            Content = "Test message 2",
            Metadata = new Dictionary<string, string>(),
            ParticipantId = "test-participant",
            WorkflowIds = ["test-workflow"]
        };

        // Act - Send messages
        var sendResult1 = await _systemActivities.SendMessage(message1);
        var sendResult2 = await _systemActivities.SendMessage(message2);

        Assert.NotNull(sendResult1?.MessageIds[0]);
        Assert.NotNull(sendResult2?.MessageIds[0]);

        // Give server time to process
        await Task.Delay(1000);
        
        // Get message history
        var messages = await _threadHistoryService.GetMessageHistory(message1.WorkflowIds[0], message1.ParticipantId);

        // Log what we received
        _logger.LogInformation($"Retrieved {messages.Count} messages for thread {message1.WorkflowIds[0]} {message1.ParticipantId}");
        foreach (var msg in messages)
        {
            _logger.LogInformation($"Message: {msg.Content}");
        }

        // Assert
        Assert.NotEmpty(messages);
        
        // Find our test messages (they might not be the only ones in the thread)
        var testMessages = messages.Where(m => m.Content == message1.Content || m.Content == message2.Content).ToList();
        Assert.NotEmpty(testMessages);
        
        // Verify proper message contents were saved
        Assert.Contains(testMessages, m => m.Content == message1.Content);
        Assert.Contains(testMessages, m => m.Content == message2.Content);
    }

} 