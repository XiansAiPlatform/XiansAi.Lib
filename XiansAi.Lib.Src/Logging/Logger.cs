using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Workflows;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
namespace XiansAi.Logging;

public class Logger<T>
{
    private readonly Lazy<ILogger> _lazyLogger;
    private static readonly ConcurrentDictionary<Type, object> _loggers = new();
    private static bool _isInitialized = false;
    private static readonly object _initLock = new object();

    static Logger()
    {
        InitializeLoggingSystem();
    }

    private static void InitializeLoggingSystem()
    {
        lock (_initLock)
        {
            if (_isInitialized) return;

            // Initialize minimal logger for application startup
            var services = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider();

            // Initialize the logging system
            LoggingServices.Initialize(services);

            _isInitialized = true;
        }
    }

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

    private Logger()
    {
        _lazyLogger = new Lazy<ILogger>(() =>
        {

            var logFactory = LoggerFactory.Create(builder =>
            {
                // Configure API logger with Trace level
                builder.AddProvider(new ApiLoggerProvider());

                // Configure console logger with level from environment variable
                var consoleLogLevel = GetConsoleLogLevel();
                builder.AddConsole(options =>
                {
                    options.LogToStandardErrorThreshold = consoleLogLevel;
                });

                // Set the default minimum level to Trace to ensure API logger gets all logs
                builder.SetMinimumLevel(LogLevel.Trace);

                // Configure console logger to respect the environment variable level
                builder.AddFilter("Microsoft", consoleLogLevel)
                       .AddFilter("System", consoleLogLevel)
                       .AddFilter((category, level) => level >= consoleLogLevel);
            });
            return logFactory.CreateLogger(typeof(T).FullName ?? "Unknown");
        });
    }

    private ILogger _logger => _lazyLogger.Value;

    public static Logger<TLogger> For<TLogger>()
    {
        return (Logger<TLogger>)_loggers.GetOrAdd(typeof(TLogger), _ => new Logger<TLogger>());
    }

    public static Logger<T> For()
    {
        return (Logger<T>)_loggers.GetOrAdd(typeof(T), _ => new Logger<T>());
    }

    private bool IsInWorkflow()
    {
        return Workflow.InWorkflow;
    }

    private bool IsInActivity()
    {
        try
        {
            return ActivityExecutionContext.Current != null;
        }
        catch (InvalidOperationException)
        {
            // No current context, not in an activity
            return false;
        }
    }

    private Dictionary<string, object> GetContextData()
    {
        var contextData = new Dictionary<string, object>();

        try
        {
            contextData["WorkflowId"] = AgentContext.WorkflowId;
            contextData["WorkflowRunId"] = AgentContext.WorkflowRunId;
            contextData["WorkflowType"] = AgentContext.WorkflowType;
            contextData["Agent"] = AgentContext.Agent;
            contextData["ParticipantId"] = "TODO";
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Error getting context data: " + e.Message);
        }

        return contextData;
    }

    public void LogTrace(string message)
    {
        Log(LogLevel.Trace, message, null);
    }

    public void LogDebug(string message)
    {
        Log(LogLevel.Debug, message, null);
    }

    public void LogInformation(string message)
    {
        Log(LogLevel.Information, message, null);
    }

    public void LogWarning(string message)
    {
        Log(LogLevel.Warning, message, null);
    }

    public void LogError(string message, Exception? exception = null)
    {
        Log(LogLevel.Error, message, exception);
    }

    public void LogCritical(string message, Exception? exception = null)
    {
        Log(LogLevel.Critical, message, exception);
    }

    private void Log(LogLevel logLevel, string message, Exception? exception)
    {
        var contextData = GetContextData();
        // If in workflow context, use Workflow.Logger
        if (IsInWorkflow())
        {
            // Create a scope for Workflow.Logger with the appropriate context data
            using (Workflow.Logger.BeginScope(contextData))
            {
                // Map the log level to appropriate Workflow.Logger methods
                switch (logLevel)
                {
                    case LogLevel.Trace:
                        Workflow.Logger.LogTrace(message);
                        break;
                    case LogLevel.Debug:
                        Workflow.Logger.LogDebug(message);
                        break;
                    case LogLevel.Information:
                        Workflow.Logger.LogInformation(message);
                        break;
                    case LogLevel.Warning:
                        Workflow.Logger.LogWarning(message);
                        break;
                    case LogLevel.Error:
                        if (exception != null)
                            Workflow.Logger.LogError($"{message} Exception: {exception}");
                        else
                            Workflow.Logger.LogError(message);
                        break;
                    case LogLevel.Critical:
                        if (exception != null)
                            Workflow.Logger.LogError($"CRITICAL: {message} Exception: {exception}");
                        else
                            Workflow.Logger.LogError($"CRITICAL: {message}");
                        break;
                    default:
                        Workflow.Logger.LogInformation(message);
                        break;
                }
            }
            return;
        }

        // For non-workflow context, use the normal logger
        if (contextData.Count > 0)
        {
            using (_logger.BeginScope(contextData))
            {
                if (exception != null)
                {
                    _logger.Log(logLevel, exception, message);
                }
                else
                {
                    _logger.Log(logLevel, message);
                }
            }
        }
        else
        {
            if (exception != null)
            {
                _logger.Log(logLevel, exception, message);
            }
            else
            {
                _logger.Log(logLevel, message);
            }
        }
    }
}