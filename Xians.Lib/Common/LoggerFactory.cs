using Microsoft.Extensions.Logging;

namespace Xians.Lib.Common;

/// <summary>
/// Provides a centralized logger factory for the library.
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

