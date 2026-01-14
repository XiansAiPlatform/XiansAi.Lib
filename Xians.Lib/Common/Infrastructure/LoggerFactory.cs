using Microsoft.Extensions.Logging;
using Xians.Lib.Logging;

namespace Xians.Lib.Common.Infrastructure;

/// <summary>
/// Provides a centralized logger factory for the library.
/// Supports both console logging and API logging (sending logs to the application server).
/// </summary>
public static class LoggerFactory
{
    private static ILoggerFactory? _loggerFactory;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets or sets the logger factory instance.
    /// </summary>
    public static ILoggerFactory Instance
    {
        get
        {
            if (_loggerFactory == null)
            {
                lock (_lock)
                {
                    if (_loggerFactory == null)
                    {
                        _loggerFactory = CreateDefaultLoggerFactory();
                    }
                }
            }
            return _loggerFactory;
        }
        set
        {
            lock (_lock)
            {
                _loggerFactory = value;
            }
        }
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to create a logger for.</typeparam>
    /// <returns>A logger instance.</returns>
    public static ILogger<T> CreateLogger<T>()
    {
        return Instance.CreateLogger<T>();
    }

    /// <summary>
    /// Creates a default logger factory with console logging.
    /// </summary>
    /// <param name="minLevel">Minimum log level. Defaults to Information.</param>
    /// <returns>A configured logger factory.</returns>
    public static ILoggerFactory CreateDefaultLoggerFactory(LogLevel minLevel = LogLevel.Information)
    {
        return Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "[HH:mm:ss] ";
                    options.SingleLine = true;
                })
                .SetMinimumLevel(minLevel);
        });
    }

    /// <summary>
    /// Creates a logger factory with both console and API logging.
    /// API logging sends logs to the application server for centralized storage.
    /// </summary>
    /// <param name="enableApiLogging">Whether to enable API logging (default: true).</param>
    /// <returns>A configured logger factory with console and optionally API logging.</returns>
    public static ILoggerFactory CreateLoggerFactoryWithApiLogging(bool enableApiLogging = true)
    {
        var consoleLogLevel = GetConsoleLogLevel();
        
        return Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            // Add API logger if enabled (sends logs to server)
            if (enableApiLogging)
            {
                builder.AddProvider(new ApiLoggerProvider());
            }

            // Add console logger with level from environment variable
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = consoleLogLevel;
            });

            // Set the default minimum level to Trace to ensure API logger gets all logs
            // Individual providers filter based on their own settings
            builder.SetMinimumLevel(LogLevel.Trace);

            // Configure console logger to respect the environment variable level
            builder.AddFilter("Microsoft", consoleLogLevel)
                   .AddFilter("System", consoleLogLevel)
                   .AddFilter((category, level) => level >= consoleLogLevel);
        });
    }

    /// <summary>
    /// Gets the console log level from environment variables.
    /// </summary>
    private static LogLevel GetConsoleLogLevel()
    {
        return ParseLogLevel(
            Environment.GetEnvironmentVariable(Common.WorkflowConstants.EnvironmentVariables.ConsoleLogLevel),
            defaultLevel: LogLevel.Debug
        );
    }

    /// <summary>
    /// Gets the API log level from environment variables.
    /// </summary>
    public static LogLevel GetApiLogLevel()
    {
        return ParseLogLevel(
            Environment.GetEnvironmentVariable(Common.WorkflowConstants.EnvironmentVariables.ApiLogLevel),
            defaultLevel: LogLevel.Error
        );
    }

    /// <summary>
    /// Parses a log level string from environment variables to a LogLevel enum.
    /// </summary>
    /// <param name="logLevelString">The log level string (e.g., "DEBUG", "INFO", "ERROR").</param>
    /// <param name="defaultLevel">The default log level to return if parsing fails.</param>
    /// <returns>The parsed LogLevel or the default if invalid.</returns>
    public static LogLevel ParseLogLevel(string? logLevelString, LogLevel defaultLevel = LogLevel.Information)
    {
        var level = logLevelString?.ToUpper();
        return level switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFORMATION" or "INFO" => LogLevel.Information,
            "WARNING" or "WARN" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            "CRITICAL" => LogLevel.Critical,
            _ => defaultLevel
        };
    }

    /// <summary>
    /// Resets the logger factory to its default state.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _loggerFactory?.Dispose();
            _loggerFactory = null;
        }
    }
}



