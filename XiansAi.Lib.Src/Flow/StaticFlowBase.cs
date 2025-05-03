using System.Linq.Expressions;
using Temporalio.Workflows;
using Server;
using XiansAi.Router;
using XiansAi.Knowledge;
using XiansAi.Messaging;
using XiansAi.Events;
using XiansAi.Logging;
using XiansAi.Memory;

namespace XiansAi.Flow;

/// <summary>
/// Base class for all workflow implementations providing common functionality.
/// </summary>
public abstract class StaticFlowBase
{
    private const int MaxLogLines = 100;
    private readonly Logger<StaticFlowBase> _logger = Logger<StaticFlowBase>.For();
    private readonly Dictionary<Type, Type> _typeMappings = new();

    private readonly IMemoryHub _memoryHub;
    protected readonly IMessageHub _messageHub;
    protected readonly IRouteHub _routeHub;
    protected readonly IEventHub _eventHub;
    protected readonly IKnowledgeHub _knowledgeHub;

    // Signal method to receive events
    [WorkflowSignal("ReceiveEvent")]
    public async Task ReceiveEvent(object eventDto)
    {
        await _eventHub.EventListener(eventDto);
    }

    [WorkflowSignal("HandleInboundMessage")]
    public async Task HandleInboundMessage(MessageSignal messageSignal)
    {
        _logger.LogInformation($"Received inbound message in base class: {messageSignal.Content}");
        await _messageHub.ReceiveMessage(messageSignal);
    }

    /// <summary>
    /// Initializes a new instance of the FlowBase class.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when LogFactory is not initialized</exception>
    protected StaticFlowBase()
    {
        _messageHub = new MessageHub();
        _memoryHub = new MemoryHub();
        _routeHub = new RouteHub();
        _knowledgeHub = new KnowledgeHub();
        _eventHub = new EventHub();
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
                $"Failed to execute activity {typeof(TActivityInstance).Name}",
                ex);
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
        _logger.LogInformation($"Delaying for {timeSpan}");
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
            _logger.LogInformation($"Executing activity '{methodName}' at '{typeof(TActivityInstance).Name}'");

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
                $"Successfully completed activity '{methodName}' at '{typeof(TActivityInstance).Name}' with result: {result?.ToString()}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"Failed to execute activity {typeof(TActivityInstance).Name}",
                ex);
            throw;
        }
    }

    public bool IsInWorkflow()
    {
        var isInWorkflow = Workflow.InWorkflow;
        _logger.LogDebug($"IsInWorkflow: {isInWorkflow}");
        return isInWorkflow;
    }

    /// <summary>
    /// Get the latest error logs from the activity error log file.
    /// </summary>
    /// <returns>The latest error logs as a string, or null if the file does not exist.</returns>
    [WorkflowQuery]
    public string? GetLogsFromFile()
    {
        var currentDateFileName = DateTime.Now.ToString("yyyyMMdd");
        var errorFilePath = Path.Combine(Directory.GetCurrentDirectory(), $"logs/app{currentDateFileName}.log");

        if (!File.Exists(errorFilePath))
            return null;

        try
        {
            // Open the file with shared read access
            using var fs = new FileStream(errorFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);

            var lastLines = new LinkedList<string>();
            string? line;

            while ((line = sr.ReadLine()) != null)
            {
                lastLines.AddLast(line);
                if (lastLines.Count > MaxLogLines)
                    lastLines.RemoveFirst();
            }

            return string.Join(Environment.NewLine, lastLines);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to read logs: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get the latest error logs from an Mongo DB collection.
    /// </summary>
    /// <returns>The latest error logs as a string, or null if the collection does not exist.</returns>
    [WorkflowQuery]
    public string? GetLogsFromMongo(string workflowId, string runId)
    {
        // Call the activity asynchronously using 'await'
        var mongoDbActivity = new MongoDbActivity();
        var logs = mongoDbActivity.GetLogsFromMongo(workflowId, runId);
       
        return string.Join(Environment.NewLine, logs.Select(log =>
            $"Level: {log.GetValue("Level", "N/A")}\n" +
            $"Timestamp: {log.GetValue("UtcTimeStamp", "N/A")}\n" +
            $"Message: {log.GetValue("RenderedMessage", "N/A")}\n" +
            "--------------------------------------------------"));
    }
}
