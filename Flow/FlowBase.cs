using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Temporalio.Worker.Interceptors;
using Temporalio.Workflows;
using System.Collections.Concurrent;

namespace XiansAi.Flow;

/// <summary>
/// Base class for all workflow implementations providing common functionality.
/// </summary>
public abstract class FlowBase
{
    private readonly ILogger _logger;

    // Dictionary to track received signal values
    private readonly ConcurrentDictionary<string, object> _receivedSignals = new();


    /// <summary>
    /// Initializes a new instance of the FlowBase class.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when LogFactory is not initialized</exception>
    protected FlowBase()
    {
        _logger = Globals.LogFactory?.CreateLogger<FlowBase>()
            ?? throw new InvalidOperationException("LogFactory not initialized");
    }

    /// <summary>
    /// Executes an activity asynchronously with default timeout settings.
    /// </summary>
    /// <typeparam name="TActivityInstance">The type of the activity instance</typeparam>
    /// <typeparam name="TResult">The type of the result returned by the activity</typeparam>
    /// <param name="activityCall">Expression representing the activity method call</param>
    /// <param name="timeoutMinutes">Optional timeout in minutes (defaults to 5)</param>
    /// <returns>Task representing the result of the activity execution</returns>
    /// <exception cref="ArgumentNullException">Thrown when activityCall is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when timeoutMinutes is less than or equal to 0</exception>
    protected virtual async Task<TResult> RunActivityAsync<TActivityInstance, TResult>(
        Expression<Func<TActivityInstance, Task<TResult>>> activityCall,
        int timeoutMinutes = 5)
    {
        ArgumentNullException.ThrowIfNull(activityCall, nameof(activityCall));

        if (timeoutMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutMinutes),
                "Timeout must be greater than 0 minutes");
        }

        try
        {
            var options = new ActivityOptions
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(timeoutMinutes)
            };

            _logger.LogDebug(
                "Executing activity {ActivityType} with {TimeoutMinutes} minute timeout",
                typeof(TActivityInstance).Name,
                timeoutMinutes);

            var result = await Workflow.ExecuteActivityAsync(activityCall, options);

            _logger.LogDebug(
                "Successfully completed activity {ActivityType}",
                typeof(TActivityInstance).Name);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to execute activity {ActivityType}",
                typeof(TActivityInstance).Name);
            throw;
        }
    }

    /// <summary>
    /// Delays the workflow for a specified duration.
    /// </summary>
    /// <param name="timeSpan">The duration to delay for</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>A task that completes after the delay</returns>
    protected virtual async Task DelayAsync(TimeSpan timeSpan, CancellationToken cancellationToken = default)
    {
        await Workflow.DelayAsync(timeSpan, cancellationToken);
    }

    /// <summary>
    /// Executes an activity asynchronously with custom activity options.
    /// </summary>
    /// <typeparam name="TActivityInstance">The type of the activity instance</typeparam>
    /// <typeparam name="TResult">The type of the result returned by the activity</typeparam>
    /// <param name="activityCall">Expression representing the activity method call</param>
    /// <param name="options">Custom activity options</param>
    /// <returns>Task representing the result of the activity execution</returns>
    /// <exception cref="ArgumentNullException">Thrown when activityCall or options is null</exception>
    protected virtual async Task<TResult> RunActivityAsync<TActivityInstance, TResult>(
        Expression<Func<TActivityInstance, Task<TResult>>> activityCall,
        ActivityOptions options)
    {
        ArgumentNullException.ThrowIfNull(activityCall, nameof(activityCall));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        try
        {
            _logger.LogDebug(
                "Executing activity {ActivityType} with custom options",
                typeof(TActivityInstance).Name);

            var result = await Workflow.ExecuteActivityAsync(activityCall, options);

            _logger.LogDebug(
                "Successfully completed activity {ActivityType}",
                typeof(TActivityInstance).Name);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to execute activity {ActivityType}",
                typeof(TActivityInstance).Name);
            throw;
        }
    }

    /// <summary>
    /// Waits asynchronously for an external signal with the specified logical name.
    /// Uses Temporal's built-in wait mechanism.
    /// </summary>
    /// <typeparam name="T">The type of the signal payload</typeparam>
    /// <param name="signalName">The logical signal name to wait for</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>The signal payload value</returns>
    protected async Task<T> WaitForEvent<T>(string signalName, CancellationToken cancellationToken = default)
    {
        await Workflow.WaitConditionAsync(() => _receivedSignals.ContainsKey(signalName), cancellationToken);

        _receivedSignals.TryRemove(signalName, out var result);
        return (T)result!;
    }

    /// <summary>
    /// A dictionary to store received signals.
    /// </summary>
    /// <remarks>
    /// This is used to store signals received by the workflow.
    /// </remarks>
    [WorkflowSignal("HandleSignal")]
    public Task HandleSignal(SignalPayload payload)
    {
        _logger.LogDebug("Signal received for '{SignalName}', storing value.", payload.SignalName);
        _receivedSignals[payload.SignalName] = payload.Value;
        return Task.CompletedTask;
    }

}

/// <summary>
/// A simple record type representing the payload for a signal.
/// Contains a logical signal name and an associated value.
/// </summary>
public record SignalPayload(string SignalName, object Value);