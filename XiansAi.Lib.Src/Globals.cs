using Microsoft.Extensions.Logging;

public static class Globals
{
    //public static XiansAIConfig? XiansAIConfig;

    private static LogLevel GetConsoleLogLevel()
    {
        var consoleLogLevel = Environment.GetEnvironmentVariable("CONSOLE_LOG_LEVEL")?.ToUpper();
        return consoleLogLevel switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFORMATION" or "INFO" => LogLevel.Information,
            "WARNING" or "WARN" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            "CRITICAL" => LogLevel.Critical,
            _ => LogLevel.Debug // Default to Debug if not set or invalid
        };
    }

    public static ILoggerFactory LogFactory = 
        LoggerFactory.Create(builder => 
        {
            var consoleLogLevel = GetConsoleLogLevel();
            
            builder.AddConsole(options =>
            {
                options.LogToStandardErrorThreshold = consoleLogLevel;
            });
            
            // Set the minimum level to allow all logs through, then filter at console level
            builder.SetMinimumLevel(LogLevel.Trace);
            
            // Configure filters to respect the environment variable level
            builder.AddFilter("Microsoft", consoleLogLevel)
                   .AddFilter("System", consoleLogLevel)
                   .AddFilter((category, level) => level >= consoleLogLevel);
        });

    public static ILoggerProvider[] AdditionalLoggerProviders = Array.Empty<ILoggerProvider>();
}
