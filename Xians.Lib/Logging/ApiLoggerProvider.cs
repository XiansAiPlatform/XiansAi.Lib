using Microsoft.Extensions.Logging;
using Xians.Lib.Logging.Models;
using Xians.Lib.Common;
using Xians.Lib.Agents.Core;
using Temporalio.Workflows;

namespace Xians.Lib.Logging;

/// <summary>
/// Provider for creating API loggers that send logs to the application server.
/// </summary>
public class ApiLoggerProvider : ILoggerProvider
{
    private bool _isDisposed = false;

    /// <summary>
    /// Creates a logger for the specified category.
    /// </summary>
    /// <param name="categoryName">The category name for the logger.</param>
    /// <returns>An ILogger instance.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return new ApiLogger();
    }

    /// <summary>
    /// Disposes the provider.
    /// </summary>
    public void Dispose() 
    {
        if (_isDisposed) return;
        _isDisposed = true;
    }
}

/// <summary>
/// Logger implementation that sends logs to the application server via API.
/// </summary>
public class ApiLogger : ILogger
{
    private static readonly AsyncLocal<IDisposable?> _currentScope = new AsyncLocal<IDisposable?>();
    private static readonly AsyncLocal<Dictionary<string, object>?> _currentContext = new AsyncLocal<Dictionary<string, object>?>();


    /// <summary>
    /// Processes Temporal-specific messages and adjusts log levels accordingly.
    /// </summary>
    private LogLevel ProcessTemporalMessage(string message, LogLevel originalLevel)
    {
        // Elevate ActivityFailureException to Critical
        if (originalLevel == LogLevel.Error && message.Contains("Temporalio.Exceptions.ActivityFailureException"))
        {
            return LogLevel.Critical;
        }

        // Elevate "Activity task failed" from Trace to Critical
        if (originalLevel == LogLevel.Trace && message.Contains("Activity task failed"))
        {
            return LogLevel.Critical;
        }

        // Only process Trace and Debug messages for activity completion
        if (originalLevel != LogLevel.Trace && originalLevel != LogLevel.Debug)
        {
            return originalLevel;
        }

        // Skip if not related to activity completion
        if (!message.Contains("Sending activity completion"))
        {
            return originalLevel;
        }
   
        // Elevate failed activity completion from Trace to Error
        if (originalLevel == LogLevel.Trace && message.Contains("\"failed\""))
        {
            return LogLevel.Error;
        }

        return originalLevel;
    }

    /// <summary>
    /// Begins a logical operation scope.
    /// </summary>
    IDisposable ILogger.BeginScope<TState>(TState state)
    {
        if (state is IEnumerable<KeyValuePair<string, object>> kvps)
        {
            var contextDict = kvps.ToDictionary(kv => kv.Key, kv => kv.Value);
            _currentContext.Value = contextDict;
        }

        var disposable = new ScopeDisposable(() => _currentContext.Value = null);
        _currentScope.Value = disposable;
        return disposable;
    }

    /// <summary>
    /// Checks if the given log level is enabled.
    /// </summary>
    public bool IsEnabled(LogLevel logLevel)
    {
        var serverLogLevel = Common.Infrastructure.LoggerFactory.GetServerLogLevel();
        return logLevel >= serverLogLevel;
    }

    /// <summary>
    /// Writes a log entry.
    /// </summary>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Check if this log level should be processed
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var logMessage = formatter(state, exception);

        logLevel = ProcessTemporalMessage(logMessage, logLevel);

        // Check again after processing temporal message in case level changed
        if (!IsEnabled(logLevel))
        {
            return;
        }

        // Extract context from Temporal workflow/activity using XiansContext
        var workflowId = XiansContext.SafeWorkflowId ?? "startup-context";
        var workflowRunId = XiansContext.SafeWorkflowRunId;
        var agent = XiansContext.SafeAgentName ?? string.Empty;
        var participantId = XiansContext.SafeParticipantId ?? XiansContext.SafeCertificateUser;
        var idPostfix = XiansContext.SafeIdPostfix;
        var workflowType = XiansContext.SafeWorkflowType;


        var log = new Log
        {
            Id = Workflow.InWorkflow ? Workflow.NewGuid().ToString() : Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            Level = logLevel,
            Message = logMessage,
            WorkflowId = workflowId,
            WorkflowRunId = workflowRunId,
            WorkflowType = workflowType,
            Agent = agent,
            Activation = idPostfix,
            ParticipantId = participantId,
            Exception = exception?.ToString()
        };

        LoggingServices.EnqueueLog(log);
    }

    /// <summary>
    /// Disposable scope helper.
    /// </summary>
    private class ScopeDisposable : IDisposable
    {
        private readonly Action _onDispose;
        public ScopeDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
