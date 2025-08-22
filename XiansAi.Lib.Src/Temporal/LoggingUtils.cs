using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using XiansAi.Logging;

namespace Temporal;

public static class LoggingUtils
{
    private static readonly Lazy<ILoggerFactory> _loggerFactory = new Lazy<ILoggerFactory>(() =>
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new ApiLoggerProvider());
            var consoleLogLevel = GetConsoleLogLevel();
            
            // Log the console log level using a simple logger
            using var loggerFactory = LoggerFactory.Create(b => b.AddConsole(
                options =>
                {
                    options.LogToStandardErrorThreshold = consoleLogLevel;
                }
            ));
            var tempLogger = loggerFactory.CreateLogger("LoggingUtils");
            tempLogger.LogInformation($"Agentri Logging: Console log level: {consoleLogLevel}");
            
            // Set global minimum level to capture everything
            builder.SetMinimumLevel(LogLevel.Trace);
            
            // Configure console with specific filtering for Temporalio
            builder.AddConsole(options => 
            {
                options.LogToStandardErrorThreshold = consoleLogLevel;
            });
            
            // Explicitly filter Temporalio category to Information level for the console
            builder.AddFilter<ConsoleLoggerProvider>("Temporalio", consoleLogLevel);
        });
    });

    public static LogLevel GetConsoleLogLevel()
    {
        var consoleLogLevel = Environment.GetEnvironmentVariable("CONSOLE_LOG_LEVEL")?.ToUpper();
        return consoleLogLevel switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFORMATION" => LogLevel.Information,
            "WARNING" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            "CRITICAL" => LogLevel.Critical,
            _ => LogLevel.Information // Default to Information if not set or invalid
        };
    }

    public static ILoggerFactory CreateTemporalLoggerFactory()
    {
        return _loggerFactory.Value;
    }
}
