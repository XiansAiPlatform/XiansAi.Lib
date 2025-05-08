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

    private (LogLevel level, string? exceptionMessage) ProcessTemporalMessage(string message, LogLevel originalLevel)
    {
        if (originalLevel != LogLevel.Trace && originalLevel != LogLevel.Debug)
        {
            return (originalLevel, null);
        }

        if (!message.Contains("Sending activity completion"))
        {
            return (originalLevel, null);
        }

        try
        {
            // Extract the JSON part after "Sending activity completion: "
            var jsonStart = message.IndexOf("{");
            if (jsonStart == -1) return (LogLevel.Error, null);

            var jsonPart = message.Substring(jsonStart);
            
            // Look for the failure message
            var failureStart = jsonPart.IndexOf("\"failure\"");
            if (failureStart == -1) return (LogLevel.Error, null);

            // Get the main error message
            var messageStart = jsonPart.IndexOf("\"message\"", failureStart);
            if (messageStart == -1) return (LogLevel.Error, null);

            var messageValueStart = jsonPart.IndexOf("\"", messageStart + 8) + 1;
            var messageValueEnd = jsonPart.IndexOf("\"", messageValueStart);
            var mainError = jsonPart.Substring(messageValueStart, messageValueEnd - messageValueStart);

            // Look for the cause message
            var causeStart = jsonPart.IndexOf("\"cause\"", failureStart);
            if (causeStart != -1)
            {
                var causeMessageStart = jsonPart.IndexOf("\"message\"", causeStart);
                if (causeMessageStart != -1)
                {
                    var causeValueStart = jsonPart.IndexOf("\"", causeMessageStart + 8) + 1;
                    var causeValueEnd = jsonPart.IndexOf("\"", causeValueStart);
                    var causeMessage = jsonPart.Substring(causeValueStart, causeValueEnd - causeValueStart);
                    
                    // Combine both messages for better context
                    return (LogLevel.Error, $"Main Error: {mainError}\nCause: {causeMessage}");
                }
            }

            return (LogLevel.Error, mainError);
        }
        catch
        {
            // If we can't parse the message properly, just return Error level
            return (LogLevel.Error, null);
        }
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
        var (processedLevel, temporalException) = ProcessTemporalMessage(logMessage, logLevel);
        logLevel = processedLevel;

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
            Exception = temporalException ?? exception?.ToString(),
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
