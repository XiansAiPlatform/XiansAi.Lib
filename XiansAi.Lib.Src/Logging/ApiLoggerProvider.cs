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
    
        if (originalLevel == LogLevel.Error && message.Contains("Temporalio.Exceptions.ActivityFailureException"))
        {
            return LogLevel.Critical;
        }

        if (originalLevel == LogLevel.Trace && message.Contains("Activity task failed"))
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
   
        if (originalLevel == LogLevel.Trace && message.Contains("\"failed\""))
        {
            return LogLevel.Error;
        }

        return originalLevel;
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

        logLevel = ProcessTemporalMessage(logMessage, logLevel);

        var workflowId = context?.GetValueOrDefault("WorkflowId")?.ToString() ?? "defaultWorkflowId";
        var workflowRunId = context?.GetValueOrDefault("WorkflowRunId")?.ToString() 
            ?? context?.GetValueOrDefault("RunId")?.ToString()
            ?? "defaultWorkflowRunId";
        var workflowType = context?.GetValueOrDefault("WorkflowType")?.ToString()  ?? "defaultWorkflowType";
        var agent = context?.GetValueOrDefault("Agent")?.ToString() ?? AgentContext.AgentName ?? "defaultAgent";
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

        LoggingServices.EnqueueLog(log);
    }

    private class ScopeDisposable : IDisposable
    {
        private readonly Action _onDispose;
        public ScopeDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
