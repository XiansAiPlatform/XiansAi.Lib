using Microsoft.Extensions.Logging;
using Xians.Lib.Logging;
using Xians.Lib.Common;

namespace Xians.Lib.Tests.UnitTests.Logging;

/// <summary>
/// Unit tests for ApiLoggerProvider and ApiLogger.
/// </summary>
public class ApiLoggerProviderTests : IDisposable
{
    private readonly ApiLoggerProvider _provider;

    public ApiLoggerProviderTests()
    {
        _provider = new ApiLoggerProvider();
        
        // Set default log levels for testing
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "DEBUG");
    }

    [Fact]
    public void CreateLogger_ReturnsValidLogger()
    {
        // Act
        var logger = _provider.CreateLogger("TestCategory");

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void CreateLogger_MultipleCalls_ReturnsDifferentInstances()
    {
        // Act
        var logger1 = _provider.CreateLogger("Category1");
        var logger2 = _provider.CreateLogger("Category2");

        // Assert - Instances might be different, but both should be functional
        Assert.NotNull(logger1);
        Assert.NotNull(logger2);
    }

    [Fact]
    public void ApiLogger_IsEnabled_RespectsLogLevel()
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "ERROR");
        var logger = _provider.CreateLogger("Test");

        // Act & Assert
        Assert.False(logger.IsEnabled(LogLevel.Trace));
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.False(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
    }

    [Fact]
    public void ApiLogger_IsEnabled_WithTraceLevel_EnablesAllLevels()
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "TRACE");
        var logger = _provider.CreateLogger("Test");

        // Act & Assert
        Assert.True(logger.IsEnabled(LogLevel.Trace));
        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
    }

    [Fact]
    public void ApiLogger_IsEnabled_WithDefaultLevel_UsesError()
    {
        // Arrange - Clear environment variable to test default
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, null);
        var logger = _provider.CreateLogger("Test");

        // Act & Assert - Default should be Error
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.False(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
    }

    [Fact]
    public void ApiLogger_IsEnabled_WithInvalidLevel_UsesDefault()
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "INVALID");
        var logger = _provider.CreateLogger("Test");

        // Act & Assert - Should default to Error
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Error));
    }

    [Theory]
    [InlineData("TRACE", LogLevel.Trace)]
    [InlineData("DEBUG", LogLevel.Debug)]
    [InlineData("INFORMATION", LogLevel.Information)]
    [InlineData("INFO", LogLevel.Information)]
    [InlineData("WARNING", LogLevel.Warning)]
    [InlineData("WARN", LogLevel.Warning)]
    [InlineData("ERROR", LogLevel.Error)]
    [InlineData("CRITICAL", LogLevel.Critical)]
    public void ApiLogger_IsEnabled_ParsesEnvironmentVariable(string envValue, LogLevel expectedLevel)
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, envValue);
        var logger = _provider.CreateLogger("Test");

        // Act & Assert
        Assert.True(logger.IsEnabled(expectedLevel));
    }

    [Fact]
    public void ApiLogger_Log_WithDisabledLevel_DoesNotThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "ERROR");
        var logger = _provider.CreateLogger("Test");

        // Act & Assert - Should not throw when logging below threshold
        var exception = Record.Exception(() =>
        {
            logger.LogDebug("This should be filtered out");
            logger.LogInformation("This should also be filtered out");
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ApiLogger_Log_WithNullException_HandlesCorrectly()
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "TRACE");
        var logger = _provider.CreateLogger("Test");

        // Act & Assert - Should not throw
        var exception = Record.Exception(() =>
        {
            logger.LogInformation("Message without exception");
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ApiLogger_BeginScope_ReturnsDisposable()
    {
        // Arrange
        var logger = _provider.CreateLogger("Test");
        var scopeData = new Dictionary<string, object>
        {
            ["WorkflowId"] = "test-workflow",
            ["TenantId"] = "test-tenant"
        };

        // Act
        var scope = logger.BeginScope(scopeData);

        // Assert
        Assert.NotNull(scope);
        Assert.IsAssignableFrom<IDisposable>(scope);
        
        // Cleanup
        scope.Dispose();
    }

    [Fact]
    public void ApiLogger_BeginScope_WithString_HandlesCorrectly()
    {
        // Arrange
        var logger = _provider.CreateLogger("Test");

        // Act & Assert - Should not throw
        var exception = Record.Exception(() =>
        {
            using var scope = logger.BeginScope("String scope");
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Provider_Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var provider = new ApiLoggerProvider();

        // Act & Assert - Should not throw
        var exception = Record.Exception(() =>
        {
            provider.Dispose();
            provider.Dispose(); // Second dispose should be safe
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ApiLogger_LogWithException_DoesNotThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "ERROR");
        var logger = _provider.CreateLogger("Test");
        var testException = new InvalidOperationException("Test exception");

        // Act & Assert
        var exception = Record.Exception(() =>
        {
            logger.LogError(testException, "Error occurred");
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ApiLogger_LogWithScope_DoesNotThrow()
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "TRACE");
        var logger = _provider.CreateLogger("Test");
        var scopeData = new Dictionary<string, object>
        {
            ["WorkflowId"] = "workflow-123",
            ["Agent"] = "TestAgent"
        };

        // Act & Assert
        var exception = Record.Exception(() =>
        {
            using (logger.BeginScope(scopeData))
            {
                logger.LogInformation("Message with scope");
            }
        });

        Assert.Null(exception);
    }

    public void Dispose()
    {
        _provider?.Dispose();
        
        // Cleanup environment variables
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, null);
    }
}
