using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Common;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Temporal.Workflows;
using Xians.Lib.Temporal.Workflows.Tasks;

namespace Xians.Lib.Agents.Tasks;

/// <summary>
/// Core service for task operations via Temporal client.
/// Shared by both TaskActivities and TaskCollection to avoid code duplication.
/// Handles direct Temporal client operations for querying and signaling tasks.
/// </summary>
internal class TaskService
{
    private readonly ITemporalClient _client;
    private readonly ILogger _logger;
    private readonly string _tenantId;
    private readonly string _agentName;

    public TaskService(ITemporalClient client, string agentName, string tenantId, ILogger logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _agentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
        _tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a typed workflow handle for a task.
    /// </summary>
    public WorkflowHandle<TaskWorkflow> GetTaskHandle(string taskId)
    {
        // Build workflow ID manually to avoid requiring workflow context
        // Format: {tenantId}:{agentName}:Task Workflow:{taskId}
        var workflowId = $"{_tenantId}:{_agentName}:Task Workflow:{taskId}";
        return _client.GetWorkflowHandle<TaskWorkflow>(workflowId);
    }

    /// <summary>
    /// Queries the current status of a task workflow.
    /// </summary>
    public async Task<TaskInfo> QueryTaskInfoAsync(string taskId)
    {
        _logger.LogDebug("Querying task info: TaskId={TaskId}, TenantId={TenantId}", taskId, _tenantId);
        
        var handle = GetTaskHandle(taskId);
        var taskInfo = await handle.QueryAsync(wf => wf.GetTaskInfo());
        
        _logger.LogInformation("Task info queried: TaskId={TaskId}, IsCompleted={IsCompleted}", taskId, taskInfo.IsCompleted);
        return taskInfo;
    }

    /// <summary>
    /// Sends a signal to update the draft work.
    /// </summary>
    public async Task UpdateDraftAsync(string taskId, string updatedDraft)
    {
        _logger.LogDebug("Updating draft: TaskId={TaskId}, TenantId={TenantId}", taskId, _tenantId);
        
        var handle = GetTaskHandle(taskId);
        await handle.SignalAsync(wf => wf.UpdateDraft(updatedDraft));
        
        _logger.LogInformation("Draft updated: TaskId={TaskId}", taskId);
    }

    /// <summary>
    /// Sends a signal to complete the task.
    /// </summary>
    public async Task CompleteTaskAsync(string taskId)
    {
        _logger.LogDebug("Completing task: TaskId={TaskId}, TenantId={TenantId}", taskId, _tenantId);
        
        var handle = GetTaskHandle(taskId);
        await handle.SignalAsync(wf => wf.CompleteTask());
        
        _logger.LogInformation("Task completed: TaskId={TaskId}", taskId);
    }

    /// <summary>
    /// Sends a signal to reject the task.
    /// </summary>
    public async Task RejectTaskAsync(string taskId, string rejectionMessage)
    {
        _logger.LogDebug("Rejecting task: TaskId={TaskId}, TenantId={TenantId}, Reason={Reason}", 
            taskId, _tenantId, rejectionMessage);
        
        var handle = GetTaskHandle(taskId);
        await handle.SignalAsync(wf => wf.RejectTask(rejectionMessage));
        
        _logger.LogWarning("Task rejected: TaskId={TaskId}, Reason={Reason}", taskId, rejectionMessage);
    }
}

