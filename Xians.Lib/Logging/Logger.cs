using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using System.Collections.Concurrent;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Logging;

/// <summary>
/// Helper for workflow logging. Workflow.Logger suppresses during replay; dual-logging makes workflow logs visible.
/// </summary>
internal static class WorkflowLoggingHelper
{
    public static bool ShouldLogWorkflowToConsole()
    {
        var val = Environment.GetEnvironmentVariable(Common.WorkflowConstants.EnvironmentVariables.WorkflowLogToConsole);
        if (string.IsNullOrEmpty(val)) return true;
        return val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1";
    }
}

/// <summary>
/// Interface for logger instances that provides logging methods.
/// </summary>
public interface IXiansLogger
{
    /// <summary>
    /// Logs a trace message.
    /// </summary>
    void LogTrace(string message);

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    void LogDebug(string message);

    /// <summary>
    /// Logs an information message.
    /// </summary>
    void LogInformation(string message);

    /// <summary>
    /// Logs an information message (shorthand for LogInformation).
    /// </summary>
    void LogInfo(string message);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    void LogWarning(string message);

    /// <summary>
    /// Logs an error message with optional exception.
    /// </summary>
    void LogError(string message, Exception? exception = null);

    /// <summary>
    /// Logs a critical message with optional exception.
    /// </summary>
    void LogCritical(string message, Exception? exception = null);
}

/// <summary>
/// Non-generic logger wrapper that works with runtime types.
/// Implements both IXiansLogger and ILogger for flexibility.
/// </summary>
internal class TypeBasedLoggerWrapper : IXiansLogger, ILogger
{
    private readonly Lazy<ILogger> _lazyLogger;
    private readonly Type _loggerType;

    public TypeBasedLoggerWrapper(Type loggerType)
    {
        _loggerType = loggerType;
        _lazyLogger = new Lazy<ILogger>(() =>
        {
            // Use the centralized LoggerFactory with API logging
            var logFactory = Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(enableApiLogging: true);
            return logFactory.CreateLogger(_loggerType.FullName ?? _loggerType.Name);
        });
    }

    private ILogger _logger => _lazyLogger.Value;

    /// <summary>
    /// Gets workflow context data for logging scope.
    /// </summary>
    private Dictionary<string, object>? GetContextData()
    {
        try
        {
            // Check if we're in workflow or activity context
            if (!XiansContext.InWorkflow && !XiansContext.InActivity)
            {
                return null;
            }

            var contextData = new Dictionary<string, object>();

            // Use XiansContext for consistent context extraction
            var workflowId = XiansContext.SafeWorkflowId;
            var workflowRunId = XiansContext.SafeWorkflowRunId;
            var workflowType = XiansContext.SafeWorkflowType;
            var agentName = XiansContext.SafeAgentName;
            
            // TODO: Add participant ID extraction once we have an accurate method
            string? participantId = null;

            if (workflowId != null) contextData["WorkflowId"] = workflowId;
            if (workflowRunId != null) contextData["WorkflowRunId"] = workflowRunId;
            if (workflowType != null) contextData["WorkflowType"] = workflowType;
            if (agentName != null) contextData["Agent"] = agentName;
            if (participantId != null) contextData["ParticipantId"] = participantId;

            return contextData.Count > 0 ? contextData : null;
        }
        catch
        {
            return null;
        }
    }

    public void LogTrace(string message) => Log(LogLevel.Trace, message, null);
    public void LogDebug(string message) => Log(LogLevel.Debug, message, null);
    public void LogInformation(string message) => Log(LogLevel.Information, message, null);
    public void LogInfo(string message) => Log(LogLevel.Information, message, null);
    public void LogWarning(string message) => Log(LogLevel.Warning, message, null);
    public void LogError(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
    public void LogCritical(string message, Exception? exception = null) => Log(LogLevel.Critical, message, exception);

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        Log(logLevel, message, exception);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);

    /// <summary>
    /// Core logging method that handles workflow context and routing.
    /// </summary>
    private void Log(LogLevel logLevel, string message, Exception? exception)
    {
        var contextData = GetContextData();

        // If in workflow context, use Workflow.Logger (required by Temporal).
        if (Workflow.InWorkflow)
        {
            LogToWorkflowLogger(logLevel, message, exception, contextData);
            // Workflow.Logger suppresses during replay. Dual-log so workflow logs appear in console and server.
            if (WorkflowLoggingHelper.ShouldLogWorkflowToConsole())
            {
                LogToStandardLogger(logLevel, message, exception, contextData);
            }
            return;
        }

        // For non-workflow context (activities, startup, etc.), use the standard logger
        LogToStandardLogger(logLevel, message, exception, contextData);
    }

    /// <summary>
    /// Logs to Workflow.Logger (required when inside Temporal workflows).
    /// </summary>
    private void LogToWorkflowLogger(LogLevel logLevel, string message, Exception? exception, Dictionary<string, object>? contextData)
    {
        // Create scope with context data if available
        IDisposable? scope = null;
        if (contextData != null && contextData.Count > 0)
        {
            scope = Workflow.Logger.BeginScope(contextData);
        }

        try
        {
            // Format message with exception if present
            var fullMessage = exception != null ? $"{message} Exception: {exception}" : message;

            // Use appropriate Workflow.Logger method
            switch (logLevel)
            {
                case LogLevel.Trace:
                    Workflow.Logger.LogTrace(fullMessage);
                    break;
                case LogLevel.Debug:
                    Workflow.Logger.LogDebug(fullMessage);
                    break;
                case LogLevel.Information:
                    Workflow.Logger.LogInformation(fullMessage);
                    break;
                case LogLevel.Warning:
                    Workflow.Logger.LogWarning(fullMessage);
                    break;
                case LogLevel.Error:
                    Workflow.Logger.LogError(fullMessage);
                    break;
                case LogLevel.Critical:
                    Workflow.Logger.LogCritical(fullMessage);
                    break;
                default:
                    Workflow.Logger.LogDebug(fullMessage);
                    break;
            }
        }
        finally
        {
            scope?.Dispose();
        }
    }

    /// <summary>
    /// Logs to the standard logger (activities, startup, non-workflow context).
    /// </summary>
    private void LogToStandardLogger(LogLevel logLevel, string message, Exception? exception, Dictionary<string, object>? contextData)
    {
        // Create scope with context data if available
        IDisposable? scope = null;
        if (contextData != null && contextData.Count > 0)
        {
            scope = _logger.BeginScope(contextData);
        }

        try
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
        finally
        {
            scope?.Dispose();
        }
    }
}

/// <summary>
/// Non-generic Logger class that provides factory methods for creating loggers.
/// </summary>
public static class Logger
{
    private static readonly ConcurrentDictionary<Type, TypeBasedLoggerWrapper> _typeBasedLoggers = new();

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    /// <param name="type">The type to create a logger for.</param>
    /// <returns>A logger instance (IXiansLogger) for the specified type.</returns>
    public static IXiansLogger For(Type type)
    {
        return _typeBasedLoggers.GetOrAdd(type, t => new TypeBasedLoggerWrapper(t));
    }

    /// <summary>
    /// Creates a logger for the specified type as ILogger.
    /// </summary>
    /// <param name="type">The type to create a logger for.</param>
    /// <returns>A logger instance (ILogger) for the specified type.</returns>
    public static ILogger ForILogger(Type type)
    {
        return _typeBasedLoggers.GetOrAdd(type, t => new TypeBasedLoggerWrapper(t));
    }
}

/// <summary>
/// Unified Xians logging API. Use instead of Microsoft.Extensions.Logging types to avoid confusion.
/// Provides type-based loggers with workflow context, dual-logging, and IXiansLogger/ILogger support.
/// </summary>
public static class XiansLogger
{
    /// <summary>
    /// Creates a logger for the specified type (returns IXiansLogger).
    /// </summary>
    public static IXiansLogger For(Type type) => Logger.For(type);

    /// <summary>
    /// Creates a logger for the specified type (returns ILogger).
    /// </summary>
    public static ILogger ForILogger(Type type) => Logger.ForILogger(type);

    /// <summary>
    /// Creates a context-aware logger for the specified type (returns ILogger).
    /// Equivalent to ForILogger(typeof(T)) - use for ILogger&lt;T&gt;-style APIs.
    /// </summary>
    public static ILogger GetLogger<T>() => Logger.ForILogger(typeof(T));
}

/// <summary>
/// Context-aware logger that automatically captures workflow context and routes logs appropriately.
/// Use this instead of Logger&lt;T&gt; to avoid confusion with Microsoft.Extensions.Logging types.
/// Equivalent to Logger&lt;T&gt; - use: var logger = XiansLogger&lt;MyService&gt;.For();
/// </summary>
/// <typeparam name="T">The type to create a logger for (used for logger category).</typeparam>
public class XiansLogger<T> : IXiansLogger, ILogger
{
    private static readonly ConcurrentDictionary<Type, object> _xiansLoggers = new();
    private readonly Logger<T> _inner;

    private XiansLogger()
    {
        _inner = Logger<T>.For();
    }

    /// <summary>
    /// Gets or creates a cached logger instance for the specified type.
    /// </summary>
    public static XiansLogger<TLogger> For<TLogger>() =>
        (XiansLogger<TLogger>)_xiansLoggers.GetOrAdd(typeof(TLogger), _ => new XiansLogger<TLogger>());

    /// <summary>
    /// Gets or creates a cached logger instance for the current type T.
    /// </summary>
    public static XiansLogger<T> For() =>
        (XiansLogger<T>)_xiansLoggers.GetOrAdd(typeof(T), _ => new XiansLogger<T>());

    // IXiansLogger / ILogger - delegate to inner
    public void LogTrace(string message) => _inner.LogTrace(message);
    public void LogDebug(string message) => _inner.LogDebug(message);
    public void LogInformation(string message) => _inner.LogInformation(message);
    public void LogInfo(string message) => _inner.LogInfo(message);
    public void LogWarning(string message) => _inner.LogWarning(message);
    public void LogError(string message, Exception? exception = null) => _inner.LogError(message, exception);
    public void LogCritical(string message, Exception? exception = null) => _inner.LogCritical(message, exception);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _inner.Log(logLevel, eventId, state, exception, formatter);
    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
}

/// <summary>
/// Context-aware logger wrapper that automatically captures workflow context and uses appropriate logger.
/// Prefer XiansLogger&lt;T&gt; to avoid confusion with Microsoft.Extensions.Logging.
/// Implements both IXiansLogger and ILogger for flexibility.
/// </summary>
/// <typeparam name="T">The type to create a logger for (used for logger category).</typeparam>
public class Logger<T> : IXiansLogger, ILogger
{
    private readonly Lazy<ILogger> _lazyLogger;
    private static readonly ConcurrentDictionary<Type, object> _loggers = new();

    private Logger()
    {
        _lazyLogger = new Lazy<ILogger>(() =>
        {
            // Use the centralized LoggerFactory with API logging
            var logFactory = Common.Infrastructure.LoggerFactory.CreateLoggerFactoryWithApiLogging(enableApiLogging: true);
            return logFactory.CreateLogger(typeof(T).FullName ?? typeof(T).Name);
        });
    }

    private ILogger _logger => _lazyLogger.Value;

    /// <summary>
    /// Gets or creates a cached logger instance for the specified type.
    /// </summary>
    /// <typeparam name="TLogger">The type to create a logger for.</typeparam>
    /// <returns>A cached Logger instance for the specified type.</returns>
    public static Logger<TLogger> For<TLogger>()
    {
        return (Logger<TLogger>)_loggers.GetOrAdd(typeof(TLogger), _ => new Logger<TLogger>());
    }

    /// <summary>
    /// Gets or creates a cached logger instance for the current type T.
    /// </summary>
    /// <returns>A cached Logger instance for type T.</returns>
    public static Logger<T> For()
    {
        return (Logger<T>)_loggers.GetOrAdd(typeof(T), _ => new Logger<T>());
    }

    /// <summary>
    /// Gets workflow context data for logging scope.
    /// </summary>
    private Dictionary<string, object>? GetContextData()
    {
        try
        {
            // Check if we're in workflow or activity context
            if (!XiansContext.InWorkflow && !XiansContext.InActivity)
            {
                return null;
            }

            var contextData = new Dictionary<string, object>();

            // Use XiansContext for consistent context extraction
            var workflowId = XiansContext.SafeWorkflowId;
            var workflowRunId = XiansContext.SafeWorkflowRunId;
            var workflowType = XiansContext.SafeWorkflowType;
            var agentName = XiansContext.SafeAgentName;
            
            // TODO: Add participant ID extraction once we have an accurate method
            string? participantId = null;

            if (workflowId != null) contextData["WorkflowId"] = workflowId;
            if (workflowRunId != null) contextData["WorkflowRunId"] = workflowRunId;
            if (workflowType != null) contextData["WorkflowType"] = workflowType;
            if (agentName != null) contextData["Agent"] = agentName;
            if (participantId != null) contextData["ParticipantId"] = participantId;

            return contextData.Count > 0 ? contextData : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Logs a trace message.
    /// </summary>
    public void LogTrace(string message)
    {
        Log(LogLevel.Trace, message, null);
    }

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    public void LogDebug(string message)
    {
        Log(LogLevel.Debug, message, null);
    }

    /// <summary>
    /// Logs an information message.
    /// </summary>
    public void LogInformation(string message)
    {
        Log(LogLevel.Information, message, null);
    }

    /// <summary>
    /// Logs an information message (shorthand for LogInformation).
    /// </summary>
    public void LogInfo(string message)
    {
        Log(LogLevel.Information, message, null);
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public void LogWarning(string message)
    {
        Log(LogLevel.Warning, message, null);
    }

    /// <summary>
    /// Logs an error message with optional exception.
    /// </summary>
    public void LogError(string message, Exception? exception = null)
    {
        Log(LogLevel.Error, message, exception);
    }

    /// <summary>
    /// Logs a critical message with optional exception.
    /// </summary>
    public void LogCritical(string message, Exception? exception = null)
    {
        Log(LogLevel.Critical, message, exception);
    }

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        Log(logLevel, message, exception);
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _logger.BeginScope(state);

    /// <summary>
    /// Core logging method that handles workflow context and routing.
    /// </summary>
    private void Log(LogLevel logLevel, string message, Exception? exception)
    {
        var contextData = GetContextData();

        // If in workflow context, use Workflow.Logger (required by Temporal).
        if (Workflow.InWorkflow)
        {
            LogToWorkflowLogger(logLevel, message, exception, contextData);
            // Workflow.Logger suppresses during replay. Dual-log so workflow logs appear in console and server.
            if (WorkflowLoggingHelper.ShouldLogWorkflowToConsole())
            {
                LogToStandardLogger(logLevel, message, exception, contextData);
            }
            return;
        }

        // For non-workflow context (activities, startup, etc.), use the standard logger
        LogToStandardLogger(logLevel, message, exception, contextData);
    }

    /// <summary>
    /// Logs to Workflow.Logger (required when inside Temporal workflows).
    /// </summary>
    private void LogToWorkflowLogger(LogLevel logLevel, string message, Exception? exception, Dictionary<string, object>? contextData)
    {
        // Create scope with context data if available
        IDisposable? scope = null;
        if (contextData != null && contextData.Count > 0)
        {
            scope = Workflow.Logger.BeginScope(contextData);
        }

        try
        {
            // Format message with exception if present
            var fullMessage = exception != null ? $"{message} Exception: {exception}" : message;

            // Use appropriate Workflow.Logger method
            switch (logLevel)
            {
                case LogLevel.Trace:
                    Workflow.Logger.LogTrace(fullMessage);
                    break;
                case LogLevel.Debug:
                    Workflow.Logger.LogDebug(fullMessage);
                    break;
                case LogLevel.Information:
                    Workflow.Logger.LogInformation(fullMessage);
                    break;
                case LogLevel.Warning:
                    Workflow.Logger.LogWarning(fullMessage);
                    break;
                case LogLevel.Error:
                    Workflow.Logger.LogError(fullMessage);
                    break;
                case LogLevel.Critical:
                    Workflow.Logger.LogCritical(fullMessage);
                    break;
                default:
                    Workflow.Logger.LogDebug(fullMessage);
                    break;
            }
        }
        finally
        {
            scope?.Dispose();
        }
    }

    /// <summary>
    /// Logs to the standard logger (activities, startup, non-workflow context).
    /// </summary>
    private void LogToStandardLogger(LogLevel logLevel, string message, Exception? exception, Dictionary<string, object>? contextData)
    {
        // Create scope with context data if available
        IDisposable? scope = null;
        if (contextData != null && contextData.Count > 0)
        {
            scope = _logger.BeginScope(contextData);
        }

        try
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
        finally
        {
            scope?.Dispose();
        }
    }
}
