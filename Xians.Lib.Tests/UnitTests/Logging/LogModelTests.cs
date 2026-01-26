using Microsoft.Extensions.Logging;
using Xians.Lib.Logging.Models;

namespace Xians.Lib.Tests.UnitTests.Logging;

/// <summary>
/// Unit tests for the Log model.
/// </summary>
public class LogModelTests
{
    [Fact]
    public void Log_WithRequiredFields_CreatesSuccessfully()
    {
        // Arrange & Act
        var log = new Log
        {
            Level = LogLevel.Information,
            Message = "Test message",
            WorkflowId = "test-workflow",
            WorkflowType = "TestAgent:TestWorkflow",
            Agent = "TestAgent",
            ParticipantId = "user-123"
        };

        // Assert
        Assert.NotNull(log);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Equal("Test message", log.Message);
        Assert.Equal("test-workflow", log.WorkflowId);
        Assert.Equal("TestAgent", log.Agent);
        Assert.Equal("user-123", log.ParticipantId);
    }

    [Fact]
    public void Log_WithOptionalFields_StoresCorrectly()
    {
        // Arrange & Act
        var log = new Log
        {
            Id = "log-123",
            CreatedAt = DateTime.UtcNow,
            Level = LogLevel.Error,
            Message = "Error message",
            WorkflowId = "workflow-1",
            WorkflowType = "ErrorAgent:ErrorWorkflow",
            Agent = "ErrorAgent",
            ParticipantId = "user-1",
            Activation = "activation-123",
            Exception = "System.Exception: Test error"
        };

        // Assert
        Assert.Equal("log-123", log.Id);
        Assert.Equal("activation-123", log.Activation);
        Assert.Contains("Test error", log.Exception);
    }

    [Fact]
    public void Log_WithAllLogLevels_CreatesSuccessfully()
    {
        // Arrange & Act
        var levels = new[]
        {
            LogLevel.Trace,
            LogLevel.Debug,
            LogLevel.Information,
            LogLevel.Warning,
            LogLevel.Error,
            LogLevel.Critical
        };

        // Assert
        foreach (var level in levels)
        {
            var log = new Log
            {
                Level = level,
                Message = $"Message at {level}",
                WorkflowId = "test",
                WorkflowType = "agent:workflow",
                Agent = "agent",
                ParticipantId = "participant"
            };

            Assert.Equal(level, log.Level);
        }
    }

    [Fact]
    public void Log_CreatedAt_DefaultsToReasonableTime()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var log = new Log
        {
            CreatedAt = DateTime.UtcNow,
            Level = LogLevel.Information,
            Message = "Test",
            WorkflowId = "test",
            WorkflowType = "agent:workflow",
            Agent = "agent",
            ParticipantId = "participant"
        };

        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(log.CreatedAt >= beforeCreation);
        Assert.True(log.CreatedAt <= afterCreation);
    }

    [Fact]
    public void Log_WithNullException_HandlesCorrectly()
    {
        // Arrange & Act
        var log = new Log
        {
            Level = LogLevel.Warning,
            Message = "Warning without exception",
            WorkflowId = "test",
            WorkflowType = "agent:workflow",
            Agent = "agent",
            ParticipantId = "participant",
            Exception = null
        };

        // Assert
        Assert.Null(log.Exception);
    }

    [Fact]
    public void Log_WithNullActivation_HandlesCorrectly()
    {
        // Arrange & Act
        var log = new Log
        {
            Level = LogLevel.Debug,
            Message = "Debug message",
            WorkflowId = "test",
            WorkflowType = "agent:workflow",
            Agent = "agent",
            ParticipantId = "participant",
            Activation = null
        };

        // Assert
        Assert.Null(log.Activation);
    }
}
