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
        // Initialize logging services - will only happen once 
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

    private Logger()
    {
        // Use lazy initialization to delay logger creation until needed
        _lazyLogger = new Lazy<ILogger>(() => {
            // Create appropriate logger with ApiLoggerProvider
            var logFactory = LoggerFactory.Create(builder =>
            {
                builder.AddProvider(new ApiLoggerProvider("/api/client/logs"));
                builder.SetMinimumLevel(LogLevel.Trace); // Ensure Trace level is enabled
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
            if (IsInActivity())
            {
                var info = ActivityExecutionContext.Current!.Info;
                contextData["WorkflowId"] = info.WorkflowId;
                contextData["WorkflowRunId"] = info.WorkflowRunId;
            }
            else if (IsInWorkflow())
            {
                var info = Workflow.Info;
                contextData["WorkflowId"] = info.WorkflowId;
                contextData["WorkflowRunId"] = info.RunId;
            }
        }
        catch (Exception)
        {
            Console.WriteLine("Error getting context data");
            // If we can't get context data, return empty dictionary
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