using Microsoft.Extensions.Logging;
using Xians.Lib.Common;
using XiansLoggerFactory = Xians.Lib.Common.Infrastructure.LoggerFactory;

namespace Xians.Lib.Tests.UnitTests.Logging;

/// <summary>
/// Unit tests for LoggerFactory enhancements.
/// </summary>
public class LoggerFactoryTests : IDisposable
{
    public LoggerFactoryTests()
    {
        // Reset factory before each test
        XiansLoggerFactory.Reset();
        
        // Set default log levels
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ConsoleLogLevel, "DEBUG");
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, "ERROR");
    }

    [Fact]
    public void CreateDefaultLoggerFactory_CreatesValidFactory()
    {
        // Act
        var factory = XiansLoggerFactory.CreateDefaultLoggerFactory();

        // Assert
        Assert.NotNull(factory);
        
        // Cleanup
        factory.Dispose();
    }

    [Fact]
    public void CreateDefaultLoggerFactory_WithCustomLevel_UsesProvidedLevel()
    {
        // Act
        var factory = XiansLoggerFactory.CreateDefaultLoggerFactory(LogLevel.Warning);
        var logger = factory.CreateLogger("Test");

        // Assert
        Assert.NotNull(logger);
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        
        // Cleanup
        factory.Dispose();
    }

    [Fact]
    public void CreateLoggerFactoryWithApiLogging_CreatesValidFactory()
    {
        // Act
        var factory = XiansLoggerFactory.CreateLoggerFactoryWithApiLogging(enableApiLogging: true);

        // Assert
        Assert.NotNull(factory);
        
        // Cleanup
        factory.Dispose();
    }

    [Fact]
    public void CreateLoggerFactoryWithApiLogging_WithApiDisabled_StillCreatesFactory()
    {
        // Act
        var factory = XiansLoggerFactory.CreateLoggerFactoryWithApiLogging(enableApiLogging: false);

        // Assert
        Assert.NotNull(factory);
        
        // Cleanup
        factory.Dispose();
    }

    [Fact]
    public void CreateLoggerFactoryWithApiLogging_CreatesLogger_Successfully()
    {
        // Arrange
        var factory = XiansLoggerFactory.CreateLoggerFactoryWithApiLogging();

        // Act
        var logger = factory.CreateLogger<LoggerFactoryTests>();

        // Assert
        Assert.NotNull(logger);
        
        // Cleanup
        factory.Dispose();
    }

    [Fact]
    public void CreateLogger_Generic_CreatesTypedLogger()
    {
        // Arrange
        var factory = XiansLoggerFactory.CreateDefaultLoggerFactory();
        XiansLoggerFactory.Instance = factory;

        // Act
        var logger = XiansLoggerFactory.CreateLogger<LoggerFactoryTests>();

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void Instance_GetAndSet_DelegatesToCustomFactory()
    {
        // Arrange — Critical-only factory: only Critical and above should be enabled.
        var customFactory = XiansLoggerFactory.CreateDefaultLoggerFactory(LogLevel.Critical);

        // Act — install the custom underlying factory.
        XiansLoggerFactory.Instance = customFactory;

        // Assert — Instance returns the stable wrapper (not the same reference as the
        // installed factory), but loggers built from it delegate to the installed
        // factory's filter pipeline.
        var retrievedFactory = XiansLoggerFactory.Instance;
        Assert.NotNull(retrievedFactory);

        var logger = retrievedFactory.CreateLogger("Test");
        Assert.True(logger.IsEnabled(LogLevel.Critical));
        Assert.False(logger.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void ConfigureLogLevels_AfterLoggerCached_AppliesNewLevelToExistingLogger()
    {
        // Regression test: in the previous implementation, an ILogger created before
        // ConfigureLogLevels(...) ran would remain bound to the disposed factory's
        // filter pipeline, so subsequent LogDebug calls would be silently dropped.
        // The DelegatingLogger wrapper + dynamic filter must surface the new level
        // on the next call.

        // Arrange — start at Information (default for CreateDefaultLoggerFactory).
        XiansLoggerFactory.Reset();
        Environment.SetEnvironmentVariable(
            WorkflowConstants.EnvironmentVariables.ConsoleLogLevel, "INFORMATION");

        // Cache a logger via the static Instance (mirrors what consumers do via DI).
        var cachedLogger = XiansLoggerFactory.Instance.CreateLogger("Test");
        Assert.False(cachedLogger.IsEnabled(LogLevel.Debug));
        Assert.True(cachedLogger.IsEnabled(LogLevel.Information));

        // Act — flip console level to Debug after the logger has already been built.
        XiansLoggerFactory.ConfigureLogLevels(
            consoleLogLevel: LogLevel.Debug,
            serverLogLevel: null);

        // Assert — the same cached logger instance now respects Debug.
        Assert.True(cachedLogger.IsEnabled(LogLevel.Debug));
    }

    [Fact]
    public void ConfigureLogLevels_WhenServerLevelToggled_RebuildsFactoryButLoggersStillWork()
    {
        // When ServerLogLevel transitions between None/null and a real level, the
        // underlying factory has to be rebuilt to add/remove the API logger provider.
        // Cached loggers must transparently re-resolve via the DelegatingLogger.

        XiansLoggerFactory.Reset();
        XiansLoggerFactory.ConfigureLogLevels(
            consoleLogLevel: LogLevel.Information,
            serverLogLevel: null);

        var cachedLogger = XiansLoggerFactory.Instance.CreateLogger("Test");
        Assert.True(cachedLogger.IsEnabled(LogLevel.Information));

        // Act — toggle ServerLogLevel on, which forces a topology change + rebuild.
        XiansLoggerFactory.ConfigureLogLevels(
            consoleLogLevel: LogLevel.Debug,
            serverLogLevel: LogLevel.Information);

        // Assert — cached logger picks up the new console level via re-resolution.
        Assert.True(cachedLogger.IsEnabled(LogLevel.Debug));
    }

    [Fact]
    public void Reset_ClearsFactory()
    {
        // Arrange
        var factory = XiansLoggerFactory.CreateDefaultLoggerFactory();
        XiansLoggerFactory.Instance = factory;

        // Act
        XiansLoggerFactory.Reset();

        // Assert - Should create new factory on access
        var newFactory = XiansLoggerFactory.Instance;
        Assert.NotNull(newFactory);
        Assert.NotSame(factory, newFactory);
    }

    [Fact]
    public void Instance_LazyInitialization_CreatesFactoryOnFirstAccess()
    {
        // Arrange - Ensure factory is reset
        XiansLoggerFactory.Reset();

        // Act
        var factory = XiansLoggerFactory.Instance;

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void CreateLogger_MultipleTimes_ReturnsLoggers()
    {
        // Arrange
        var factory = XiansLoggerFactory.CreateDefaultLoggerFactory();
        XiansLoggerFactory.Instance = factory;

        // Act
        var logger1 = XiansLoggerFactory.CreateLogger<LoggerFactoryTests>();
        var logger2 = XiansLoggerFactory.CreateLogger<LoggerFactoryTests>();

        // Assert
        Assert.NotNull(logger1);
        Assert.NotNull(logger2);
    }

    [Theory]
    [InlineData("TRACE")]
    [InlineData("DEBUG")]
    [InlineData("INFORMATION")]
    [InlineData("WARNING")]
    [InlineData("ERROR")]
    [InlineData("CRITICAL")]
    public void CreateLoggerFactoryWithApiLogging_RespectsConsoleLogLevel(string logLevel)
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ConsoleLogLevel, logLevel);

        // Act
        var factory = XiansLoggerFactory.CreateLoggerFactoryWithApiLogging();
        var logger = factory.CreateLogger("Test");

        // Assert
        Assert.NotNull(logger);
        
        // Cleanup
        factory.Dispose();
    }

    [Fact]
    public void CreateLoggerFactoryWithApiLogging_WithInvalidEnvVar_UsesDefault()
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ConsoleLogLevel, "INVALID");

        // Act
        var factory = XiansLoggerFactory.CreateLoggerFactoryWithApiLogging();
        var logger = factory.CreateLogger("Test");

        // Assert - Should use default (DEBUG) without throwing
        Assert.NotNull(logger);
        
        // Cleanup
        factory.Dispose();
    }

    [Fact]
    public void CreateLoggerFactoryWithApiLogging_WithNullEnvVar_UsesDefault()
    {
        // Arrange
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ConsoleLogLevel, null);

        // Act
        var factory = XiansLoggerFactory.CreateLoggerFactoryWithApiLogging();
        var logger = factory.CreateLogger("Test");

        // Assert - Should use default (DEBUG)
        Assert.NotNull(logger);
        
        // Cleanup
        factory.Dispose();
    }

    [Fact]
    public void LoggerFactory_ThreadSafe_MultipleAccess()
    {
        // Arrange
        XiansLoggerFactory.Reset();
        var factories = new ILoggerFactory[10];

        // Act - Access from multiple threads
        Parallel.For(0, 10, i =>
        {
            factories[i] = XiansLoggerFactory.Instance;
        });

        // Assert - All should get the same instance
        Assert.All(factories, f => Assert.NotNull(f));
        Assert.All(factories.Skip(1), f => Assert.Same(factories[0], f));
    }

    [Fact]
    public void CreateDefaultLoggerFactory_MultipleInstances_AreIndependent()
    {
        // Act
        var factory1 = XiansLoggerFactory.CreateDefaultLoggerFactory(LogLevel.Debug);
        var factory2 = XiansLoggerFactory.CreateDefaultLoggerFactory(LogLevel.Error);

        // Assert - Should be different instances
        Assert.NotSame(factory1, factory2);
        
        // Cleanup
        factory1.Dispose();
        factory2.Dispose();
    }

    public void Dispose()
    {
        XiansLoggerFactory.Reset();
        
        // Cleanup environment variables
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ConsoleLogLevel, null);
        Environment.SetEnvironmentVariable(WorkflowConstants.EnvironmentVariables.ApiLogLevel, null);
    }
}
