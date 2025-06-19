using Microsoft.Extensions.Logging;
using Moq;
using XiansAi.Server.Base;
using XiansAi.Models;
using XiansAi.Messaging;

namespace XiansAi.Lib.Tests.UnitTests;

public class SystemActivitiesUnitTests
{
    private readonly Mock<IApiService> _mockApiService;
    private readonly Mock<ILogger<SystemActivities>> _mockLogger;
    private readonly SystemActivities _systemActivities;

    public SystemActivitiesUnitTests()
    {
        _mockApiService = new Mock<IApiService>();
        _mockLogger = new Mock<ILogger<SystemActivities>>();
        _systemActivities = new SystemActivities(_mockApiService.Object, _mockLogger.Object);
    }

    [Fact]
    public void SystemActivities_Constructor_WithIApiService_ShouldSucceed()
    {
        // Act & Assert
        Assert.NotNull(_systemActivities);
    }

    [Fact]
    public async Task SendEventAsync_ShouldCallApiServicePostAsync()
    {
        // Arrange
        var eventSignal = new EventSignal
        {
            SourceWorkflowId = "test-workflow",
            SourceWorkflowType = "test-type",
            SourceAgent = "test-agent",
            TargetWorkflowType = "target-type",
            Payload = new { Message = "test" }
        };

        _mockApiService.Setup(x => x.PostAsync("api/agent/events/with-start", eventSignal))
                      .Returns(Task.FromResult("success"))
                      .Verifiable();

        // Act
        await _systemActivities.SendEventAsync(eventSignal);

        // Assert
        _mockApiService.Verify(x => x.PostAsync("api/agent/events/with-start", eventSignal), Times.Once);
    }

    [Fact]
    public async Task SendHandoffAsync_ShouldCallApiServicePostAsync()
    {
        // Arrange
        var handoffRequest = new HandoffRequest
        {
            SourceWorkflowId = "test-workflow",
            SourceWorkflowType = "test-type",
            SourceAgent = "test-agent",
            ThreadId = "test-thread",
            ParticipantId = "test-participant",
            Text = "test message"
        };

        _mockApiService.Setup(x => x.PostAsync("api/agent/conversation/outbound/handoff", handoffRequest))
                      .Returns(Task.FromResult("handoff-id"))
                      .Verifiable();

        // Act
        var result = await _systemActivities.SendHandoffAsync(handoffRequest);

        // Assert
        Assert.Equal("handoff-id", result);
        _mockApiService.Verify(x => x.PostAsync("api/agent/conversation/outbound/handoff", handoffRequest), Times.Once);
    }

    [Fact]
    public async Task SendChatOrDataAsync_ShouldCallApiServicePostAsync()
    {
        // Arrange
        var chatRequest = new ChatOrDataRequest
        {
            WorkflowId = "test-workflow",
            WorkflowType = "test-type",
            Agent = "test-agent",
            ParticipantId = "test-participant",
            Text = "Hello world"
        };

        _mockApiService.Setup(x => x.PostAsync("api/agent/conversation/outbound/chat", chatRequest))
                      .Returns(Task.FromResult("message-id"))
                      .Verifiable();

        // Act
        var result = await _systemActivities.SendChatOrDataAsync(chatRequest, MessageType.Chat);

        // Assert
        Assert.Equal("message-id", result);
        _mockApiService.Verify(x => x.PostAsync("api/agent/conversation/outbound/chat", chatRequest), Times.Once);
    }

    [Fact]
    public async Task GetMessageHistoryAsync_ShouldCallApiServiceGetAsync()
    {
        // Arrange
        var expectedMessages = new List<DbMessage>
        {
            new DbMessage 
            { 
                Id = "1", 
                Text = "Test message", 
                ParticipantId = "test-participant",
                ThreadId = "test-thread",
                CreatedAt = DateTime.UtcNow,
                Direction = "inbound",
                WorkflowId = "test-workflow",
                WorkflowType = "test-type"
            }
        };

        _mockApiService.Setup(x => x.GetAsync<List<DbMessage>>(It.IsAny<string>()))
                      .Returns(Task.FromResult(expectedMessages))
                      .Verifiable();

        // Act
        var result = await _systemActivities.GetMessageHistoryAsync("test-workflow", "test-participant", 1, 10);

        // Assert
        Assert.Equal(expectedMessages, result);
        _mockApiService.Verify(x => x.GetAsync<List<DbMessage>>(It.Is<string>(url => 
            url.Contains("api/agent/conversation/history") && 
            url.Contains("workflowType=test-workflow") && 
            url.Contains("participantId=test-participant"))), Times.Once);
    }

    [Fact]
    public async Task SendChatOrDataAsync_WithEmptyText_ShouldThrowException()
    {
        // Arrange
        var chatRequest = new ChatOrDataRequest
        {
            WorkflowId = "test-workflow",
            WorkflowType = "test-type",
            Agent = "test-agent",
            ParticipantId = "test-participant",
            Text = "" // Empty text
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => 
            _systemActivities.SendChatOrDataAsync(chatRequest, MessageType.Chat));
        
        Assert.Equal("Text is required for chat message", exception.Message);
    }

    [Fact]
    public async Task SendChatOrDataAsync_WithNullData_ShouldThrowException()
    {
        // Arrange
        var dataRequest = new ChatOrDataRequest
        {
            WorkflowId = "test-workflow",
            WorkflowType = "test-type",
            Agent = "test-agent",
            ParticipantId = "test-participant",
            Data = null // Null data
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => 
            _systemActivities.SendChatOrDataAsync(dataRequest, MessageType.Data));
        
        Assert.Equal("Data is required for data message", exception.Message);
    }
} 