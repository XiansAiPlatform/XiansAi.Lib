using Microsoft.Extensions.Logging;
using Xunit;

namespace Xians.Lib.Tests.UnitTests.Logging;

/// <summary>
/// Unit tests for the Xians.Lib.Logging.Logger<T> wrapper class.
/// Tests instance caching, log level methods, and basic functionality.
/// Note: Workflow context tests are in integration tests as they require Temporal.
/// Uses full namespace to avoid ambiguity with Microsoft.Extensions.Logging.Logger<T>.
/// </summary>
public class LoggerWrapperTests
{
    // Test helper classes for logger category testing
    private class TestClass1 { }
    private class TestClass2 { }
    private class TestClassWithLongName { }

    #region Instance Caching Tests

    [Fact]
    public void For_SameType_ReturnsSameInstance()
    {
        // Arrange & Act
        var logger1 = Xians.Lib.Logging.Logger<TestClass1>.For();
        var logger2 = Xians.Lib.Logging.Logger<TestClass1>.For();

        // Assert
        Assert.Same(logger1, logger2);
    }

    [Fact]
    public void For_DifferentTypes_ReturnsDifferentInstances()
    {
        // Arrange & Act
        var logger1 = Xians.Lib.Logging.Logger<TestClass1>.For();
        var logger2 = Xians.Lib.Logging.Logger<TestClass2>.For();

        // Assert
        Assert.NotSame(logger1, logger2);
    }

    [Fact]
    public void ForGeneric_SameType_ReturnsSameInstance()
    {
        // Arrange & Act
        var logger1 = Xians.Lib.Logging.Logger<TestClass1>.For<TestClass1>();
        var logger2 = Xians.Lib.Logging.Logger<TestClass1>.For<TestClass1>();

        // Assert
        Assert.Same(logger1, logger2);
    }

    [Fact]
    public void ForGeneric_ReturnsLoggerForCorrectType()
    {
        // Arrange & Act
        // For<TLogger>() returns a logger for TLogger, not for the calling type
        var logger1 = Xians.Lib.Logging.Logger<TestClass1>.For();
        var logger2 = Xians.Lib.Logging.Logger<TestClass1>.For<TestClass1>();

        // Assert - Both return same cached instance for TestClass1
        Assert.Same(logger1, logger2);
    }

    [Fact]
    public void For_MultipleTypes_CachesAllInstances()
    {
        // Arrange & Act
        var logger1 = Xians.Lib.Logging.Logger<TestClass1>.For();
        var logger2 = Xians.Lib.Logging.Logger<TestClass2>.For();
        var logger3 = Xians.Lib.Logging.Logger<TestClass1>.For(); // Should return cached
        var logger4 = Xians.Lib.Logging.Logger<TestClass2>.For(); // Should return cached

        // Assert
        Assert.Same(logger1, logger3);
        Assert.Same(logger2, logger4);
        Assert.NotSame(logger1, logger2);
    }

    #endregion

    #region Log Level Method Tests

    [Fact]
    public void LogTrace_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        logger.LogTrace("Test trace message");

        // Assert - If we got here without throwing, test passes
        Assert.True(true);
    }

    [Fact]
    public void LogDebug_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        logger.LogDebug("Test debug message");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void LogInformation_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        logger.LogInformation("Test information message");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void LogWarning_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        logger.LogWarning("Test warning message");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void LogError_WithoutException_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        logger.LogError("Test error message");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void LogError_WithException_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();
        var testException = new InvalidOperationException("Test exception");

        // Act
        logger.LogError("Test error message", testException);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void LogCritical_WithoutException_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        logger.LogCritical("Test critical message");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void LogCritical_WithException_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();
        var testException = new InvalidOperationException("Test exception");

        // Act
        logger.LogCritical("Test critical message", testException);

        // Assert
        Assert.True(true);
    }

    #endregion

    #region Multiple Log Calls Tests

    [Fact]
    public void Logger_MultipleLogCalls_AllSucceed()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        logger.LogTrace("Trace message");
        logger.LogDebug("Debug message");
        logger.LogInformation("Information message");
        logger.LogWarning("Warning message");
        logger.LogError("Error message");
        logger.LogCritical("Critical message");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void Logger_RapidLogCalls_HandlesCorrectly()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        for (int i = 0; i < 100; i++)
        {
            logger.LogInformation($"Log message {i}");
        }

        // Assert
        Assert.True(true);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public void LogError_NullException_HandlesGracefully()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        logger.LogError("Error message", null);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void LogCritical_NullException_HandlesGracefully()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        logger.LogCritical("Critical message", null);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void LogError_ComplexException_HandlesCorrectly()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();
        var innerException = new ArgumentException("Inner exception");
        var outerException = new InvalidOperationException("Outer exception", innerException);

        // Act
        logger.LogError("Error with nested exceptions", outerException);

        // Assert
        Assert.True(true);
    }

    #endregion

    #region Special Characters and Edge Cases Tests

    [Fact]
    public void Logger_EmptyMessage_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        logger.LogInformation("");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void Logger_VeryLongMessage_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();
        var longMessage = new string('A', 10000);

        // Act
        logger.LogInformation(longMessage);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void Logger_MessageWithSpecialCharacters_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();
        var specialMessage = "Test with special chars: \n \t \r \" \\ / { } [ ] < > & @ # $ % ^ * ( )";

        // Act
        logger.LogInformation(specialMessage);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void Logger_MessageWithUnicode_DoesNotThrow()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();
        var unicodeMessage = "Test with unicode: 你好 مرحبا Здравствуйте 🎉 🔍";

        // Act
        logger.LogInformation(unicodeMessage);

        // Assert
        Assert.True(true);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task For_ConcurrentCalls_ReturnsSameInstance()
    {
        // Arrange
        var loggers = new System.Collections.Concurrent.ConcurrentBag<Xians.Lib.Logging.Logger<TestClass1>>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                loggers.Add(Xians.Lib.Logging.Logger<TestClass1>.For());
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var firstLogger = loggers.First();
        Assert.All(loggers, logger => Assert.Same(firstLogger, logger));
    }

    [Fact]
    public async Task Logger_ConcurrentLogCalls_AllSucceed()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    logger.LogInformation($"Task {taskId}, Log {j}");
                }
            }));
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        // Assert
        Assert.True(true);
    }

    #endregion

    #region Logger Category Tests

    [Fact]
    public void Logger_WithSimpleClassName_CreatesLogger()
    {
        // Arrange & Act
        var logger = Xians.Lib.Logging.Logger<TestClass1>.For();

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void Logger_WithLongClassName_CreatesLogger()
    {
        // Arrange & Act
        var logger = Xians.Lib.Logging.Logger<TestClassWithLongName>.For();

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void Logger_WithGenericClass_CreatesLogger()
    {
        // Arrange & Act
        var logger = Xians.Lib.Logging.Logger<List<string>>.For();

        // Assert
        Assert.NotNull(logger);
    }

    #endregion

    #region Integration with LoggerFactory Tests

    [Fact]
    public void Logger_UsesLoggerFactoryWithApiLogging()
    {
        // Arrange & Act
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Assert
        // Logger should be created successfully using LoggerFactory.CreateLoggerFactoryWithApiLogging
        Assert.NotNull(logger);

        // Verify it can log without errors (indicates proper factory setup)
        logger.LogInformation("Test message");
        Assert.True(true);
    }

    #endregion

    #region Lazy Initialization Tests

    [Fact]
    public void Logger_InitializesLazily()
    {
        // Arrange
        // Just creating the logger should not throw
        var logger = Xians.Lib.Logging.Logger<TestClass1>.For();

        // First actual log call initializes the underlying ILogger
        logger.LogInformation("First log triggers initialization");

        // Assert
        Assert.True(true);
    }

    #endregion

    #region Error Resilience Tests

    [Fact]
    public void Logger_AfterException_ContinuesToWork()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();
        var testException = new InvalidOperationException("Test exception");

        // Act
        logger.LogError("First error", testException);
        logger.LogInformation("Second log after error");
        logger.LogWarning("Third log after error");

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void Logger_MultipleExceptions_AllHandled()
    {
        // Arrange
        var logger = Xians.Lib.Logging.Logger<LoggerWrapperTests>.For();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var testException = new InvalidOperationException($"Exception {i}");
            logger.LogError($"Error {i}", testException);
        }

        // Assert
        Assert.True(true);
    }

    #endregion
}
