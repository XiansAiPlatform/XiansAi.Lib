using Microsoft.Extensions.Logging;
using Temporalio.Worker;
using Xians.Lib.Common;
using Xians.Lib.Workflows;

namespace Xians.Lib.Agents;

/// <summary>
/// Represents a workflow for handling agent interactions.
/// </summary>
public class XiansWorkflow
{
    private readonly XiansAgent _agent;
    private readonly bool _isDefault;
    private readonly ILogger<XiansWorkflow> _logger;

    internal XiansWorkflow(XiansAgent agent, string workflowType, string? name, int workers, bool isDefault)
    {
        _agent = agent;
        WorkflowType = workflowType;
        Name = name;
        Workers = workers;
        _isDefault = isDefault;
        _logger = Common.LoggerFactory.CreateLogger<XiansWorkflow>();
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
    /// Registers a handler for user messages.
    /// </summary>
    /// <param name="handler">The async handler to process user messages.</param>
    public void OnUserMessage(Func<UserMessageContext, Task> handler)
    {
        if (!_isDefault)
        {
            throw new InvalidOperationException(
                "OnUserMessage is only supported for default workflows. Use custom workflow classes for custom workflows.");
        }

        // Get tenant ID for non-system-scoped agents
        string? tenantId = null;
        if (!_agent.SystemScoped)
        {
            tenantId = _agent.Options?.TenantId ?? 
                throw new InvalidOperationException(
                    "XiansOptions is not configured properly. Cannot determine TenantId for non-system-scoped agent.");
        }

        // Register the handler with tenant isolation metadata
        // This allows multiple default workflows to each have their own isolated handler
        // and enforces tenant boundaries for security
        DefaultWorkflow.RegisterMessageHandler(
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

        // Determine task queue name based on system scoped setting
        string taskQueue;
        if (_agent.SystemScoped)
        {
            // System-scoped agents use workflow type as task queue
            taskQueue = WorkflowType;
        }
        else
        {
            // Non-system-scoped agents use TenantId:WorkflowType format
            // TenantId is automatically extracted from the API key certificate
            var tenantId = _agent.Options?.TenantId ?? 
                throw new InvalidOperationException(
                    "XiansOptions is not configured properly. Cannot determine TenantId.");
            taskQueue = $"{tenantId}:{WorkflowType}";
        }

        _logger.LogInformation("Task queue for workflow '{WorkflowType}': {TaskQueue}", WorkflowType, taskQueue);

        // Create worker options
        var workerOptions = new TemporalWorkerOptions(taskQueue: taskQueue)
        {
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
                builder
                    .AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss] ")
                    .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information))
        };

        // Register workflow based on type
        if (_isDefault)
        {
            // Register the DefaultWorkflow class for default workflows
            workerOptions.AddWorkflow<DefaultWorkflow>();
            
            // Register activities for message sending
            // Get HTTP client from the platform's HTTP service
            if (_agent.HttpService != null)
            {
                var messageActivities = new Workflows.MessageActivities(_agent.HttpService.Client);
                workerOptions.AddAllActivities(messageActivities);
            }
            else
            {
                _logger.LogWarning("HTTP service not available - message sending will not work");
            }
        }
        else
        {
            // TODO: Register custom workflow types when implemented
            throw new NotImplementedException("Custom workflows are not yet implemented");
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
}

