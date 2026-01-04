using Microsoft.Extensions.Logging;
using Temporalio.Worker;
using Xians.Lib.Agents.Scheduling;
using Xians.Lib.Common;
using Xians.Lib.Workflows;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Common.Caching;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Workflows.Knowledge;
using Xians.Lib.Workflows.Messaging;
using Xians.Lib.Workflows.Documents;
using Xians.Lib.Agents.Knowledge.Models;
using Xians.Lib.Workflows.Scheduling;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Represents a workflow for handling agent interactions.
/// </summary>
public class XiansWorkflow
{
    private readonly XiansAgent _agent;
    private readonly bool _isBuiltIn;
    private readonly ILogger<XiansWorkflow> _logger;
    private readonly List<object> _activityInstances = new();
    private readonly List<Type> _activityTypes = new();
    private readonly Type? _workflowClassType;

    internal XiansWorkflow(XiansAgent agent, string workflowType, string? name, int workers, bool isBuiltIn, Type? workflowClassType = null, bool isPlatformWorkflow = false)
    {
        if (agent == null)
            throw new ArgumentNullException(nameof(agent));
        
        if (string.IsNullOrWhiteSpace(workflowType))
            throw new ArgumentNullException(nameof(workflowType));

        // Enforce that workflow type starts with agent name prefix (unless it's a platform workflow)
        if (!isPlatformWorkflow)
        {
            var expectedPrefix = agent.Name + ":";
            if (!workflowType.StartsWith(expectedPrefix))
            {
                throw new ArgumentException(
                    $"Workflow type '{workflowType}' must start with agent name prefix '{expectedPrefix}'. " +
                    $"Expected format: '{expectedPrefix}WorkflowName'",
                    nameof(workflowType));
            }
        }

        _agent = agent;
        WorkflowType = workflowType;
        Name = name;
        Workers = workers;
        _isBuiltIn = isBuiltIn;
        _workflowClassType = workflowClassType;
        _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<XiansWorkflow>();

        // Initialize schedule collection if temporal service is available
        if (agent.TemporalService != null)
        {
            Schedules = new ScheduleCollection(agent, workflowType, agent.TemporalService);
        }

        // Register this workflow in XiansContext so it can be accessed via CurrentWorkflow
        XiansContext.RegisterWorkflow(workflowType, this);
    }

    /// <summary>
    /// Gets the workflow type identifier.
    /// </summary>
    public string WorkflowType { get; }

    /// <summary>
    /// Gets the optional workflow name.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the number of workers for this workflow.
    /// </summary>
    public int Workers { get; }

    /// <summary>
    /// Gets the schedule collection for managing scheduled executions of this workflow.
    /// </summary>
    public ScheduleCollection? Schedules { get; }

    /// <summary>
    /// Gets the workflow class type for custom workflows, or null for built-in workflows.
    /// </summary>
    internal Type? GetWorkflowClassType() => _workflowClassType;

    /// <summary>
    /// Adds an activity instance to the workflow.
    /// The activity will be registered with all workers for this workflow.
    /// </summary>
    /// <param name="activityInstance">The activity instance to register.</param>
    /// <returns>This workflow instance for method chaining.</returns>
    public XiansWorkflow AddActivity(object activityInstance)
    {
        if (activityInstance == null)
            throw new ArgumentNullException(nameof(activityInstance));

        _activityInstances.Add(activityInstance);
        _logger.LogDebug("Activity instance of type '{ActivityType}' added to workflow '{WorkflowType}'",
            activityInstance.GetType().Name, WorkflowType);
        
        return this;
    }

    /// <summary>
    /// Adds an activity type to the workflow.
    /// An instance will be created for each worker.
    /// </summary>
    /// <typeparam name="T">The activity type to register.</typeparam>
    /// <returns>This workflow instance for method chaining.</returns>
    public XiansWorkflow AddActivity<T>() where T : class
    {
        _activityTypes.Add(typeof(T));
        _logger.LogDebug("Activity type '{ActivityType}' added to workflow '{WorkflowType}'",
            typeof(T).Name, WorkflowType);
        
        return this;
    }

    /// <summary>
    /// Adds multiple activity instances to the workflow.
    /// </summary>
    /// <param name="activityInstances">The activity instances to register.</param>
    /// <returns>This workflow instance for method chaining.</returns>
    public XiansWorkflow AddActivities(params object[] activityInstances)
    {
        foreach (var activity in activityInstances)
        {
            AddActivity(activity);
        }
        return this;
    }

    /// <summary>
    /// Registers a handler for user chat messages.
    /// </summary>
    /// <param name="handler">The async handler to process user chat messages.</param>
    public void OnUserChatMessage(Func<UserMessageContext, Task> handler)
    {
        if (!_isBuiltIn)
        {
            throw new InvalidOperationException(
                "OnUserChatMessage is only supported for built-in workflows. Use custom workflow classes for custom workflows.");
        }

        var tenantId = GetTenantIdOrNull();

        BuiltinWorkflow.RegisterChatHandler(
            workflowType: WorkflowType,
            handler: handler,
            agentName: _agent.Name,
            tenantId: tenantId,
            systemScoped: _agent.SystemScoped
        );
        
        _logger.LogDebug(
            "Chat message handler registered for workflow '{WorkflowType}', Agent='{AgentName}', SystemScoped={SystemScoped}, TenantId={TenantId}",
            WorkflowType,
            _agent.Name,
            _agent.SystemScoped,
            tenantId ?? "(system)");
    }

    /// <summary>
    /// Registers a handler for user data messages.
    /// </summary>
    /// <param name="handler">The async handler to process user data messages.</param>
    public void OnUserDataMessage(Func<UserMessageContext, Task> handler)
    {
        if (!_isBuiltIn)
        {
            throw new InvalidOperationException(
                "OnUserDataMessage is only supported for built-in workflows. Use custom workflow classes for custom workflows.");
        }

        var tenantId = GetTenantIdOrNull();

        BuiltinWorkflow.RegisterDataHandler(
            workflowType: WorkflowType,
            handler: handler,
            agentName: _agent.Name,
            tenantId: tenantId,
            systemScoped: _agent.SystemScoped
        );
        
        _logger.LogDebug(
            "Data message handler registered for workflow '{WorkflowType}', Agent='{AgentName}', SystemScoped={SystemScoped}, TenantId={TenantId}",
            WorkflowType,
            _agent.Name,
            _agent.SystemScoped,
            tenantId ?? "(system)");
    }

    /// <summary>
    /// Registers a handler for webhook messages.
    /// </summary>
    /// <param name="handler">The async handler to process webhook messages.</param>
    public void OnWebhook(Func<WebhookContext, Task> handler)
    {
        if (!_isBuiltIn)
        {
            throw new InvalidOperationException(
                "OnWebhook is only supported for built-in workflows. Use custom workflow classes for custom workflows.");
        }

        var tenantId = GetTenantIdOrNull();

        BuiltinWorkflow.RegisterWebhookHandler(
            workflowType: WorkflowType,
            handler: handler,
            agentName: _agent.Name,
            tenantId: tenantId,
            systemScoped: _agent.SystemScoped
        );
        
        _logger.LogDebug(
            "Webhook handler registered for workflow '{WorkflowType}', Agent='{AgentName}', SystemScoped={SystemScoped}, TenantId={TenantId}",
            WorkflowType,
            _agent.Name,
            _agent.SystemScoped,
            tenantId ?? "(system)");
    }


    /// <summary>
    /// Runs the workflow asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for stopping the workflow.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_agent.TemporalService == null)
        {
            throw new InvalidOperationException("Temporal service is not configured. Cannot run workflows.");
        }

        _logger.LogInformation("Starting workflow '{WorkflowType}' for agent '{AgentName}' with {Workers} worker(s)", 
            WorkflowType, _agent.Name, Workers);

        // Get Temporal client
        var client = await _agent.TemporalService.GetClientAsync();

        // Determine task queue name using centralized utility
        var tenantId = GetTenantIdOrNull();
        var taskQueue = TenantContext.GetTaskQueueName(
            WorkflowType, 
            _agent.SystemScoped, 
            tenantId);

        _logger.LogInformation(
            "Task queue for workflow '{WorkflowType}': {TaskQueue}, SystemScoped={SystemScoped}", 
            WorkflowType, 
            taskQueue,
            _agent.SystemScoped);

        // Create worker options
        var workerOptions = new TemporalWorkerOptions(taskQueue: taskQueue)
        {
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                builder
                    .AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ")
                    .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information))
        };

        // Initialize registrars
        var workflowRegistrar = new WorkflowRegistrar(_logger);
        var activityRegistrar = new ActivityRegistrar(_agent, _logger);

        // Register workflow and activities
        int totalActivityCount = 0;
        
        if (_isBuiltIn)
        {
            workflowRegistrar.RegisterBuiltInWorkflow(workerOptions, WorkflowType);
            totalActivityCount = activityRegistrar.RegisterSystemActivities(workerOptions, WorkflowType);
        }
        else
        {
            workflowRegistrar.RegisterCustomWorkflow(workerOptions, WorkflowType, _workflowClassType!);
            
            // Register user activities first
            totalActivityCount += activityRegistrar.RegisterUserActivityInstances(
                workerOptions, WorkflowType, _activityInstances);
            totalActivityCount += activityRegistrar.RegisterUserActivityTypes(
                workerOptions, WorkflowType, _activityTypes);
            
            // Then register system activities (always available)
            totalActivityCount += activityRegistrar.RegisterSystemActivities(workerOptions, WorkflowType);
            
            _logger.LogInformation(
                "Custom workflow '{WorkflowType}' registered with {ActivityCount} activities",
                WorkflowType, totalActivityCount);
        }

        // Create and start workers
        var workers = new List<TemporalWorker>();
        var workerTasks = new List<Task>();

        try
        {
            for (int i = 0; i < Workers; i++)
            {
                var worker = new TemporalWorker(client, workerOptions);
                workers.Add(worker);
                
                var workerIndex = i + 1;
                _logger.LogInformation("Worker {WorkerIndex}/{TotalWorkers} for '{WorkflowType}' on queue '{TaskQueue}' created and ready to run", 
                    workerIndex, Workers, WorkflowType, taskQueue);

                // Create task for this worker
                var workerTask = Task.Run(async () =>
                {
                    try
                    {
                        await worker.ExecuteAsync(cancellationToken);
                        _logger.LogInformation("Worker {WorkerIndex} execution completed on queue '{TaskQueue}'", 
                            workerIndex, taskQueue);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Worker {WorkerIndex} execution cancelled", workerIndex);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Worker {WorkerIndex} encountered an error", workerIndex);
                        throw;
                    }
                }, cancellationToken);

                workerTasks.Add(workerTask);
            }

            // Wait for all workers to complete
            await Task.WhenAll(workerTasks);
        }
        finally
        {
            // Dispose all workers
            foreach (var worker in workers)
            {
                try
                {
                    worker?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing worker");
                }
            }

            _logger.LogInformation("All workers for '{WorkflowType}' on queue '{TaskQueue}' have been shut down", 
                WorkflowType, taskQueue);
        }
    }

    /// <summary>
    /// Gets the tenant ID for non-system-scoped agents, or null for system-scoped agents.
    /// </summary>
    private string? GetTenantIdOrNull()
    {
        if (_agent.SystemScoped)
        {
            return null;
        }

        return _agent.Options?.CertificateTenantId ?? 
            throw new InvalidOperationException(
                "XiansOptions is not configured properly. Cannot determine TenantId for non-system-scoped agent.");
    }

}

