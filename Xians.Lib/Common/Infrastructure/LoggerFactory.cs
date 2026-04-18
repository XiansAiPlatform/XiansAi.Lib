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
    private static LogLevel? _consoleLogLevelOverride;
    private static LogLevel? _serverLogLevelOverride;

    // Bumped whenever the underlying factory is replaced, so DelegatingLogger
    // instances know to re-resolve their cached ILogger from the new factory.
    private static int _generation;

    // Stable wrapper returned from Instance. Its loggers transparently follow the
    // underlying factory across replacement so consumers can cache them safely.
    private static readonly DelegatingLoggerFactory _stable = new();

    /// <summary>
    /// Gets or sets the logger factory instance.
    /// The getter returns a stable wrapper whose loggers transparently follow the
    /// underlying factory across replacement (e.g. when <see cref="ConfigureLogLevels"/>
    /// rebuilds it). Consumers can therefore cache <see cref="ILogger"/> references
    /// captured at construction time without going stale.
    /// The setter installs a custom underlying factory; loggers handed out previously
    /// will switch to the new factory on their next call.
    /// </summary>
    public static ILoggerFactory Instance
    {
        get => _stable;
        set
        {
            lock (_lock)
            {
                if (!ReferenceEquals(_loggerFactory, value))
                {
                    _loggerFactory?.Dispose();
                    _loggerFactory = value;
                    Interlocked.Increment(ref _generation);
                }
            }
        }
    }

    /// <summary>
    /// Returns the current underlying <see cref="ILoggerFactory"/>, creating it lazily.
    /// Used by <see cref="DelegatingLoggerFactory"/>/<see cref="DelegatingLogger"/> to
    /// delegate operations.
    /// </summary>
    private static ILoggerFactory GetUnderlying()
    {
        if (_loggerFactory == null)
        {
            lock (_lock)
            {
                _loggerFactory ??= CreateDefaultLoggerFactory();
            }
        }
        return _loggerFactory;
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to create a logger for.</typeparam>
    /// <returns>A logger instance.</returns>
    public static ILogger<T> CreateLogger<T>()
    {
        try
        {
            return Instance.CreateLogger<T>();
        }
        catch (ObjectDisposedException)
        {
            // Factory was disposed (likely by a test), recreate it
            lock (_lock)
            {
                _loggerFactory?.Dispose();
                _loggerFactory = CreateDefaultLoggerFactory();
                // Use the newly created factory directly to avoid race conditions
                return _loggerFactory.CreateLogger<T>();
            }
        }
    }

    /// <summary>
    /// Creates a default logger factory with console logging and optionally API logging.
    /// API logging is enabled if ServerLogLevel has been configured via ConfigureLogLevels.
    /// </summary>
    /// <param name="minLevel">Minimum log level. Defaults to Information.</param>
    /// <returns>A configured logger factory.</returns>
    public static ILoggerFactory CreateDefaultLoggerFactory(LogLevel minLevel = LogLevel.Information)
    {
        // If ServerLogLevel was explicitly configured via ConfigureLogLevels, enable API logging
        // This ensures all loggers (including HttpClientService, CacheService, etc.) send logs to server
        if (_serverLogLevelOverride.HasValue && _serverLogLevelOverride.Value != LogLevel.None)
        {
            Console.WriteLine($"[LoggerFactory] Creating logger factory with API logging enabled (ServerLogLevel: {_serverLogLevelOverride.Value})");
            return CreateLoggerFactoryWithApiLogging(enableApiLogging: true);
        }
        
        // Otherwise, just console logging.
        // The level filter is a callback that consults GetConsoleLogLevel() at log time,
        // so console-level changes via ConfigureLogLevels take effect without rebuilding
        // the factory. The supplied minLevel is honoured as a fallback when no override
        // (XiansOptions.ConsoleLogLevel or env var) has been configured.
        var fallbackLevel = minLevel;
        return Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .AddSimpleConsole(options =>
                {
                    options.TimestampFormat = "[HH:mm:ss] ";
                    options.SingleLine = true;
                })
                .SetMinimumLevel(LogLevel.Trace)
                .AddFilter((_, level) => level >= (_consoleLogLevelOverride ?? fallbackLevel));
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
        return Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            // Add API logger if enabled (sends logs to server)
            if (enableApiLogging)
            {
                builder.AddProvider(new ApiLoggerProvider());
            }

            // Add console logger; LogToStandardErrorThreshold is captured at build time
            // (changing it dynamically would require IOptionsMonitor plumbing).
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = GetConsoleLogLevel();
            });

            // Set the default minimum level to Trace to ensure API logger gets all logs.
            // The filters below are evaluated at log time, so console-level changes via
            // ConfigureLogLevels take effect without rebuilding the factory.
            builder.SetMinimumLevel(LogLevel.Trace);

            builder.AddFilter("Microsoft", level => level >= GetConsoleLogLevel())
                   .AddFilter("System", level => level >= GetConsoleLogLevel())
                   .AddFilter((_, level) => level >= GetConsoleLogLevel());
        });
    }

    /// <summary>
    /// Configures the logger factory with log level overrides from XiansOptions.
    /// Should be called during platform initialization.
    /// </summary>
    /// <param name="consoleLogLevel">Console log level override (null to use environment variable).</param>
    /// <param name="serverLogLevel">Server log level override (null to use environment variable).</param>
    public static void ConfigureLogLevels(LogLevel? consoleLogLevel, LogLevel? serverLogLevel)
    {
        lock (_lock)
        {
            // Only the API-logger provider topology depends on whether ServerLogLevel is set.
            // Console-level changes are picked up dynamically by the callback filter at log
            // time, so we can leave the existing factory in place — preserving any ILogger
            // instances consumers may have already cached.
            var hadApiLogger =
                _serverLogLevelOverride.HasValue && _serverLogLevelOverride.Value != LogLevel.None;
            var willHaveApiLogger =
                serverLogLevel.HasValue && serverLogLevel.Value != LogLevel.None;

            _consoleLogLevelOverride = consoleLogLevel;
            _serverLogLevelOverride = serverLogLevel;

            if (hadApiLogger != willHaveApiLogger && _loggerFactory != null)
            {
                _loggerFactory.Dispose();
                _loggerFactory = null;
            }

            // Bump the generation so any DelegatingLogger instances re-resolve their
            // underlying logger (in case the factory was rebuilt above) and so the new
            // override is picked up immediately even by loggers that cached IsEnabled
            // results from the previous filter.
            Interlocked.Increment(ref _generation);
        }
    }

    /// <summary>
    /// Gets the console log level from XiansOptions override or environment variables.
    /// </summary>
    private static LogLevel GetConsoleLogLevel()
    {
        // Check override first (from XiansOptions)
        if (_consoleLogLevelOverride.HasValue)
        {
            return _consoleLogLevelOverride.Value;
        }

        // Fall back to environment variable
        return ParseLogLevel(
            Environment.GetEnvironmentVariable(Common.WorkflowConstants.EnvironmentVariables.ConsoleLogLevel),
            defaultLevel: LogLevel.Debug
        );
    }

    /// <summary>
    /// Gets the server log level from XiansOptions override or environment variables.
    /// Checks SERVER_LOG_LEVEL first, then falls back to legacy API_LOG_LEVEL for backward compatibility.
    /// </summary>
    public static LogLevel GetServerLogLevel()
    {
        // Check override first (from XiansOptions)
        if (_serverLogLevelOverride.HasValue)
        {
            return _serverLogLevelOverride.Value;
        }

        // Check new environment variable name first
        var serverLogLevel = Environment.GetEnvironmentVariable(Common.WorkflowConstants.EnvironmentVariables.ServerLogLevel);
        if (!string.IsNullOrEmpty(serverLogLevel))
        {
            return ParseLogLevel(serverLogLevel, defaultLevel: LogLevel.Error);
        }

        // Fall back to legacy API_LOG_LEVEL for backward compatibility
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
            Interlocked.Increment(ref _generation);
        }
    }

    /// <summary>
    /// Stable <see cref="ILoggerFactory"/> wrapper. <see cref="CreateLogger"/> returns
    /// <see cref="DelegatingLogger"/> instances that transparently re-resolve their
    /// underlying <see cref="ILogger"/> whenever the static generation counter advances
    /// (which happens whenever the underlying factory is replaced).
    /// </summary>
    private sealed class DelegatingLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new DelegatingLogger(categoryName);

        public void AddProvider(ILoggerProvider provider) => GetUnderlying().AddProvider(provider);

        public void Dispose()
        {
            // The stable wrapper is process-lifetime; disposing it would orphan all
            // delegating loggers handed out to consumers. The underlying factory is
            // owned and disposed through ConfigureLogLevels/Reset.
        }
    }

    /// <summary>
    /// <see cref="ILogger"/> that re-resolves its underlying logger when the generation
    /// counter advances, so it survives factory replacement without becoming stale.
    /// </summary>
    private sealed class DelegatingLogger : ILogger
    {
        private readonly string _category;
        private ILogger? _cached;
        private int _cachedGen = -1;

        public DelegatingLogger(string category)
        {
            _category = category;
        }

        private ILogger Current
        {
            get
            {
                var gen = Volatile.Read(ref _generation);
                if (_cached is null || _cachedGen != gen)
                {
                    _cached = GetUnderlying().CreateLogger(_category);
                    _cachedGen = gen;
                }
                return _cached;
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => Current.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => Current.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Current.Log(logLevel, eventId, state, exception, formatter);
    }
}



