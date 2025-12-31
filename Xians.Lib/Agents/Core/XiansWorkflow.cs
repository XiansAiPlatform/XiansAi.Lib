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

    internal XiansWorkflow(XiansAgent agent, string workflowType, string? name, int workers, bool isBuiltIn, Type? workflowClassType = null)
    {
        if (agent == null)
            throw new ArgumentNullException(nameof(agent));
        
        if (string.IsNullOrWhiteSpace(workflowType))
            throw new ArgumentNullException(nameof(workflowType));

        // Enforce that workflow type starts with agent name prefix
        var expectedPrefix = agent.Name + ":";
        if (!workflowType.StartsWith(expectedPrefix))
        {
            throw new ArgumentException(
                $"Workflow type '{workflowType}' must start with agent name prefix '{expectedPrefix}'. " +
                $"Expected format: '{expectedPrefix}WorkflowName'",
                nameof(workflowType));
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
    /// Registers a handler for user messages.
    /// </summary>
    /// <param name="handler">The async handler to process user messages.</param>
    public void OnUserMessage(Func<UserMessageContext, Task> handler)
    {
        if (!_isBuiltIn)
        {
            throw new InvalidOperationException(
                "OnUserMessage is only supported for built-in workflows. Use custom workflow classes for custom workflows.");
        }

        var tenantId = GetTenantIdOrNull();

        // Register the handler with tenant isolation metadata
        // This allows multiple default workflows to each have their own isolated handler
        // and enforces tenant boundaries for security
        BuiltinWorkflow.RegisterMessageHandler(
            workflowType: WorkflowType,
            handler: handler,
            agentName: _agent.Name,
            tenantId: tenantId,
            systemScoped: _agent.SystemScoped
        );
        
        _logger.LogDebug(
            "User message handler registered for workflow '{WorkflowType}', Agent='{AgentName}', SystemScoped={SystemScoped}, TenantId={TenantId}",
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

        // Register workflow based on type
        if (_isBuiltIn)
        {
            // Register the BuiltinWorkflow class for built-in workflows
            workerOptions.AddWorkflow<BuiltinWorkflow>();
            
            // Register activities for message sending, knowledge, and document operations
            // Get HTTP client from the platform's HTTP service
            if (_agent.HttpService != null)
            {
                var messageActivities = new Xians.Lib.Workflows.Messaging.MessageActivities(_agent.HttpService.Client);
                workerOptions.AddAllActivities(typeof(Xians.Lib.Workflows.Messaging.MessageActivities), messageActivities);
                
                var knowledgeActivities = new Xians.Lib.Workflows.Knowledge.KnowledgeActivities(
                    _agent.HttpService.Client, 
                    _agent.CacheService);
                workerOptions.AddAllActivities(typeof(Xians.Lib.Workflows.Knowledge.KnowledgeActivities), knowledgeActivities);
                
                var documentActivities = new Xians.Lib.Workflows.Documents.DocumentActivities(_agent.HttpService.Client);
                workerOptions.AddAllActivities(typeof(Xians.Lib.Workflows.Documents.DocumentActivities), documentActivities);
            }
            else
            {
                _logger.LogWarning("HTTP service not available - message sending, knowledge, and document operations will not work");
            }

            // Register system activities (always available for all built-in workflows)
            RegisterSystemActivities(workerOptions);
        }
        else
        {
            // Register custom workflow type
            if (_workflowClassType == null)
            {
                throw new InvalidOperationException($"Workflow class type not provided for custom workflow '{WorkflowType}'");
            }

            // Register the custom workflow using the stored type
            var addWorkflowMethod = typeof(TemporalWorkerOptions).GetMethod("AddWorkflow", Type.EmptyTypes);
            var genericAddWorkflowMethod = addWorkflowMethod?.MakeGenericMethod(_workflowClassType);
            genericAddWorkflowMethod?.Invoke(workerOptions, null);

            // Register user-provided activity instances
            foreach (var activityInstance in _activityInstances)
            {
                workerOptions.AddAllActivities(activityInstance.GetType(), activityInstance);
                _logger.LogInformation(
                    "Registered activity instance '{ActivityType}' for workflow '{WorkflowType}'",
                    activityInstance.GetType().Name, WorkflowType);
            }

            // Register user-provided activity types
            foreach (var activityType in _activityTypes)
            {
                var instance = Activator.CreateInstance(activityType);
                if (instance != null)
                {
                    workerOptions.AddAllActivities(activityType, instance);
                    _logger.LogInformation(
                        "Registered activity type '{ActivityType}' for workflow '{WorkflowType}'",
                        activityType.Name, WorkflowType);
                }
            }

            // Register system activities (always available for all custom workflows)
            RegisterSystemActivities(workerOptions);
            
            _logger.LogInformation("Custom workflow '{WorkflowType}' registered successfully with {ActivityCount} activities",
                WorkflowType, _activityInstances.Count + _activityTypes.Count + 4); // +4 for ScheduleActivities, MessageActivities, KnowledgeActivities, DocumentActivities
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

    /// <summary>
    /// Registers system activities for workflows.
    /// This is automatically done for all workflows, making these core operations available by default:
    /// - ScheduleActivities: For managing schedules from workflows
    /// - MessageActivities: For message operations
    /// - KnowledgeActivities: For knowledge operations
    /// - DocumentActivities: For document operations
    /// This ensures all workflows can use A2A communication and interact with workflows that need these activities.
    /// </summary>
    private void RegisterSystemActivities(TemporalWorkerOptions workerOptions)
    {
        // Register ScheduleActivities (always available)
        try
        {
            var scheduleActivities = new Xians.Lib.Workflows.Scheduling.ScheduleActivities();
            workerOptions.AddAllActivities(typeof(Xians.Lib.Workflows.Scheduling.ScheduleActivities), scheduleActivities);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not register ScheduleActivities for workflow '{WorkflowType}'", WorkflowType);
        }

        // Register core activities (Message, Knowledge, Document) if HTTP service is available
        if (_agent.HttpService != null)
        {
            try
            {
                var messageActivities = new Xians.Lib.Workflows.Messaging.MessageActivities(_agent.HttpService.Client);
                workerOptions.AddAllActivities(typeof(Xians.Lib.Workflows.Messaging.MessageActivities), messageActivities);
                
                var knowledgeActivities = new Xians.Lib.Workflows.Knowledge.KnowledgeActivities(
                    _agent.HttpService.Client, 
                    _agent.CacheService);
                workerOptions.AddAllActivities(typeof(Xians.Lib.Workflows.Knowledge.KnowledgeActivities), knowledgeActivities);
                
                var documentActivities = new Xians.Lib.Workflows.Documents.DocumentActivities(_agent.HttpService.Client);
                workerOptions.AddAllActivities(typeof(Xians.Lib.Workflows.Documents.DocumentActivities), documentActivities);

                _logger.LogInformation(
                    "Registered system activities (Schedule, Message, Knowledge, Document) for workflow '{WorkflowType}'",
                    WorkflowType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not register core activities for workflow '{WorkflowType}'", WorkflowType);
            }
        }
        else
        {
            _logger.LogInformation(
                "Registered system activities (Schedule only) for workflow '{WorkflowType}' - HTTP service not available",
                WorkflowType);
        }
    }
}

