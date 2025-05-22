using Microsoft.Extensions.Logging;
using XiansAi.Models;

namespace XiansAi.Logging;

public class ApiLoggerProvider : ILoggerProvider
{
    private bool _isDisposed = false;


    public ILogger CreateLogger(string categoryName)
    {
        return new ApiLogger();
    }

    public void Dispose() 
    {
        if (_isDisposed) return;
        _isDisposed = true;
    }
}

public class ApiLogger : ILogger
{
    private static readonly AsyncLocal<IDisposable?> _currentScope = new AsyncLocal<IDisposable?>();
    private static readonly AsyncLocal<Dictionary<string, object>?> _currentContext = new AsyncLocal<Dictionary<string, object>?>();

    private LogLevel ProcessTemporalMessage(string message, LogLevel originalLevel)
    {
      
         // Check for Temporal exceptions
        if (originalLevel == LogLevel.Error && message.Contains("Temporalio.Exceptions.ActivityFailureException"))
        {
            return LogLevel.Critical;
        }

        if (originalLevel != LogLevel.Trace && originalLevel != LogLevel.Debug)
        {
            return originalLevel;
        }

       
        if (!message.Contains("Sending activity completion"))
        {
            return originalLevel;
        }

        // If we find a failure in the message, change the level to Error
        if (message.Contains("\"failed\""))
        {
            return LogLevel.Error;
        }

        return originalLevel;
    }

    public ApiLogger()
    {
        // No dependencies needed
    }

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

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var logMessage = formatter(state, exception);
        var context = _currentContext.Value;

        // Process the message to check for Temporal errors and extract exception
        logLevel = ProcessTemporalMessage(logMessage, logLevel);

        var workflowId = context?.GetValueOrDefault("WorkflowId")?.ToString() ?? "defaultWorkflowId";
        
        // Check for both "WorkflowRunId" and "RunId" keys
        var workflowRunId = context?.GetValueOrDefault("WorkflowRunId")?.ToString() 
            ?? context?.GetValueOrDefault("RunId")?.ToString()
            ?? "defaultWorkflowRunId";

        var workflowType = context?.GetValueOrDefault("WorkflowType")?.ToString() ?? "defaultWorkflowType";
        var agent = context?.GetValueOrDefault("Agent")?.ToString() ?? AgentContext.Agent ?? "defaultAgent"; // if we dont include AgentContext here for the agent, we're unable to get the agent for temporal logs
        var participantId = context?.GetValueOrDefault("ParticipantId")?.ToString() ?? "defaultParticipantId";

        var log = new Log
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            Level = logLevel,
            Message = logMessage,
            WorkflowId = workflowId,
            WorkflowRunId = workflowRunId,
            WorkflowType = workflowType,
            Agent = agent,
            ParticipantId = participantId,
            Properties = null,
            Exception = exception?.ToString(),
            UpdatedAt = null
        };

        // Use the static queue instead of a local queue
        LoggingServices.EnqueueLog(log);
    }

    private class ScopeDisposable : IDisposable
    {
        private readonly Action _onDispose;
        public ScopeDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
