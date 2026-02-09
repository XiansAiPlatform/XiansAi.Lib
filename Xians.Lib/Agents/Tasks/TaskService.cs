using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Temporal.Workflows;

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
        
        _logger.LogDebug("Task info queried: TaskId={TaskId}, IsCompleted={IsCompleted}", taskId, taskInfo.IsCompleted);
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
        
        _logger.LogDebug("Draft updated: TaskId={TaskId}", taskId);
    }

    /// <summary>
    /// Performs an action on a task with an optional comment.
    /// </summary>
    public async Task PerformActionAsync(string taskId, string action, string? comment = null)
    {
        _logger.LogDebug("Performing action: TaskId={TaskId}, Action={Action}, TenantId={TenantId}", 
            taskId, action, _tenantId);
        
        var handle = GetTaskHandle(taskId);
        var actionRequest = new TaskActionRequest { Action = action, Comment = comment };
        await handle.SignalAsync(wf => wf.PerformAction(actionRequest));
        
        _logger.LogDebug("Action performed: TaskId={TaskId}, Action={Action}", taskId, action);
    }
}
