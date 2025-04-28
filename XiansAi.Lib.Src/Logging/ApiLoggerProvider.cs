using Microsoft.Extensions.Logging;
using XiansAi.Models;

namespace XiansAi.Logging;

public class ApiLoggerProvider : ILoggerProvider
{
    private bool _isDisposed = false;

    public ApiLoggerProvider(string logApiUrl)
    {
        // No need to create a LogQueue anymore
    }

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

        var workflowId = context?.GetValueOrDefault("WorkflowId")?.ToString() ?? "defaultWorkflowId";
        
        // Check for both "WorkflowRunId" and "RunId" keys
        var workflowRunId = context?.GetValueOrDefault("WorkflowRunId")?.ToString() 
            ?? context?.GetValueOrDefault("RunId")?.ToString()
            ?? "defaultWorkflowRunId";

        var log = new Log
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            Level = logLevel,
            Message = logMessage,
            WorkflowId = workflowId,
            WorkflowRunId = workflowRunId,
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
