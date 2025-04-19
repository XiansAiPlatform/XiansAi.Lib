using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Server;
using XiansAi.Router;
using XiansAi.Knowledge;
using XiansAi.Messaging;

namespace XiansAi.Flow;

/// <summary>
/// Base class for all workflow implementations providing common functionality.
/// </summary>
public abstract class FlowBase
{
    private readonly ILogger _logger;
    private readonly ObjectCacheManager _cacheManager;
    private readonly Dictionary<Type, Type> _typeMappings = new();

    public IMessenger Messenger { get; }

    public ISemanticRouter Router { get; }

    public IKnowledgeManager KnowledgeManager { get; }

    [WorkflowSignal]
    public async Task HandleInboundMessage(MessageSignal messageSignal)
    {
        _logger.LogInformation($"Received inbound message in base class: {messageSignal.Content}");
        await Messenger.ReceiveMessage(messageSignal);
    }

    /// <summary>
    /// Initializes a new instance of the FlowBase class.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when LogFactory is not initialized</exception>
    protected FlowBase()
    {
        Messenger = Messaging.Messenger.Instance;
        _logger = Globals.LogFactory?.CreateLogger<FlowBase>()
            ?? throw new InvalidOperationException("LogFactory not initialized");
        _cacheManager = new ObjectCacheManager();
        Router = new SemanticRouter();
        KnowledgeManager = new KnowledgeManager();
    }
    public ILogger GetLogger()
    {
        if (IsInWorkflow())
        {
            return Workflow.Logger;
        }
        return _logger;
    }

    public void SetActivityTypeMapping<TInterface, TImplementation>()
    {
        _typeMappings[typeof(TInterface)] = typeof(TImplementation);
    }


    public void SetActivityTypeMappings(Dictionary<Type, Type> typeMappings)
    {
        foreach (var mapping in typeMappings)
        {
            _typeMappings[mapping.Key] = mapping.Value;
        }
    }

    private Task<TResult> RunActivityAsyncLocal<TActivityInstance, TResult>(Expression<Func<TActivityInstance, Task<TResult>>> activityCall, int timeoutMinutes = 5)
    {
        // Create an instance of TActivityInstance
        TActivityInstance activityInstance;
        if (typeof(TActivityInstance).IsInterface)
        {
            if (_typeMappings.ContainsKey(typeof(TActivityInstance)))
            {
                Type concreteType = _typeMappings[typeof(TActivityInstance)];
                activityInstance = (TActivityInstance)Activator.CreateInstance(concreteType)!;
            }
            else
            {
                throw new InvalidOperationException("No concrete type provided for interface " + typeof(TActivityInstance).Name);
            }
        }
        else
        {
            activityInstance = Activator.CreateInstance<TActivityInstance>(); // Directly create an instance if not an interface
        }

        return activityCall.Compile()(activityInstance);
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

            var result = await RunActivityAsync(activityCall, options);

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
        _logger.LogInformation("Delaying for {TimeSpan}", timeSpan);
        if (IsInWorkflow())
        {
            await Workflow.DelayAsync(timeSpan, cancellationToken);
        }
        else
        {
            await Task.Delay(timeSpan, cancellationToken);
        }
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

        // Get method name from expression without compiling
        var methodName = ((MethodCallExpression)activityCall.Body).Method.Name;

        try
        {
            _logger.LogInformation("Executing activity '{ActivityMethod}' at '{ActivityType}'",
                methodName,
                typeof(TActivityInstance).Name);

            TResult result;
            if (IsInWorkflow())
            {
                result = await Workflow.ExecuteActivityAsync(activityCall, options);
            }
            else
            {
                result = await RunActivityAsyncLocal(activityCall);
            }

            _logger.LogInformation(
                "Successfully completed activity '{ActivityMethod}' at '{ActivityType}' with result: {Result}",
                methodName,
                typeof(TActivityInstance).Name,
                result?.ToString());

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

    public bool IsInWorkflow()
    {
        var isInWorkflow = Workflow.InWorkflow;
        _logger.LogDebug("IsInWorkflow: {IsInWorkflow}", isInWorkflow);
        return isInWorkflow;
    }

    /// <summary>
    /// Gets a value from the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve</typeparam>
    /// <param name="key">The key to look up</param>
    /// <returns>The cached value if found, otherwise null</returns>
    protected async Task<T?> GetCacheValueAsync<T>(string key)
    {
        _logger.LogInformation("Getting value from cache for key: {Key}", key);
        return await _cacheManager.GetValueAsync<T>(key);
    }

    /// <summary>
    /// Sets a value in the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of the value to store</typeparam>
    /// <param name="key">The key to store the value under</param>
    /// <param name="value">The value to store</param>
    /// <returns>True if the operation was successful, false otherwise</returns>
    protected async Task<bool> SetCacheValueAsync<T>(string key, T value)
    {
        _logger.LogInformation("Setting value in cache for key: {Key}", key);
        return await _cacheManager.SetValueAsync(key, value);
    }

    /// <summary>
    /// Deletes a value from the cache for the specified key.
    /// </summary>
    /// <param name="key">The key to delete</param>
    /// <returns>True if the operation was successful, false otherwise</returns>
    protected async Task<bool> DeleteCacheValueAsync(string key)
    {
        _logger.LogInformation("Deleting value from cache for key: {Key}", key);
        return await _cacheManager.DeleteValueAsync(key);
    }
}
