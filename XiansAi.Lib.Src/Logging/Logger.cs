using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Workflows;
using System.Collections.Concurrent;

namespace XiansAi.Logging;

public class Logger<T>
{
    private readonly ILogger _logger;
    private static readonly ConcurrentDictionary<Type, object> _loggers = new();

    private Logger()
    {
        // Get the appropriate logger based on context
        if (IsInActivity())
        {
            // In activity context, use ApiLoggerProvider
            var logFactory = LoggerFactory.Create(builder =>
            {
                builder.AddProvider(new ApiLoggerProvider("/api/client/logs"));
            });
            _logger = logFactory.CreateLogger(typeof(T).FullName ?? "Unknown");
        }
        else
        {
            // In normal context, use the global logger factory
            _logger = Globals.LogFactory.CreateLogger(typeof(T).FullName ?? "Unknown");
        }
    }

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
        return ActivityExecutionContext.Current != null;
    }

    private Dictionary<string, object> GetContextData()
    {
        var contextData = new Dictionary<string, object>();

        if (IsInActivity())
        {
            var info = ActivityExecutionContext.Current!.Info;
            contextData["TenantId"] = info.WorkflowNamespace;
            contextData["WorkflowId"] = info.WorkflowId;
            contextData["WorkflowRunId"] = info.WorkflowRunId;
        }
        else if (IsInWorkflow())
        {
            var info = Workflow.Info;
            contextData["TenantId"] = info.Namespace;
            contextData["WorkflowId"] = info.WorkflowId;
            contextData["WorkflowRunId"] = info.RunId;
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