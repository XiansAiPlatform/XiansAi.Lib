using Microsoft.Extensions.Logging;
using XiansAi.Models;
using XiansAi.Logging;
using Temporalio.Activities;

public class ApiLoggerProvider : ILoggerProvider
{
    private readonly LogQueue _logQueue;
    private bool _isDisposed = false;

    public ApiLoggerProvider(string logApiUrl)
    {
        _logQueue = LoggingServices.GetOrCreateLogQueue(logApiUrl);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ApiLogger(_logQueue);
    }

    public void Dispose() 
    {
        if (_isDisposed) return;
        _isDisposed = true;
    }
}

public class ApiLogger : ILogger
{
    private readonly LogQueue _logQueue;
    private static readonly AsyncLocal<IDisposable?> _currentScope = new AsyncLocal<IDisposable?>();
    private static readonly AsyncLocal<Dictionary<string, object>?> _currentContext = new AsyncLocal<Dictionary<string, object>?>();

    public ApiLogger(LogQueue logQueue)
    {
        _logQueue = logQueue ?? throw new ArgumentNullException(nameof(logQueue));
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

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (ActivityExecutionContext.Current == null)
        {
            return;
        }

        var logMessage = formatter(state, exception);
        var context = _currentContext.Value;

        var workflowId = context?.GetValueOrDefault("WorkflowId")?.ToString() ?? "defaultWorkflowId";
        var workflowRunId = context?.GetValueOrDefault("WorkflowRunId")?.ToString() ?? "defaultWorkflowRunId";

        var log = new Log
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            Level = (XiansAi.Models.LogLevel)logLevel,
            Message = logMessage,
            WorkflowId = workflowId,
            WorkflowRunId = workflowRunId,
            Properties = null,
            Exception = exception?.ToString(),
            UpdatedAt = null
        };

        _logQueue.EnqueueLog(log);
    }

    private class ScopeDisposable : IDisposable
    {
        private readonly Action _onDispose;
        public ScopeDisposable(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
