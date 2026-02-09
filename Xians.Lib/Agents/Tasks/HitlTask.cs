using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Temporal.Workflows;

namespace Xians.Lib.Agents.Tasks;

/// <summary>
/// Represents a human-in-the-loop task with convenient methods for all task operations.
/// This class provides a clean interface for interacting with task workflows from outside workflow context.
/// </summary>
public class HitlTask
{
    private static readonly ILogger _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<HitlTask>();
    
    private readonly string _taskId;
    private readonly string _tenantId;
    private readonly string _agentName;
    private readonly ITemporalClient _client;

    public string TaskId => _taskId;
    public string TenantId => _tenantId;
    public string AgentName => _agentName;

    public HitlTask(string taskId, string tenantId, string agentName, ITemporalClient client)
    {
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        _tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        _agentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Gets a HitlTask instance for an existing task workflow from its workflow ID.
    /// Workflow ID format: "{tenantId}:{agentName}:Task Workflow:{taskId}"
    /// </summary>
    public static async Task<HitlTask> FromWorkflowIdAsync(string workflowId)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            throw new ArgumentException("Workflow ID cannot be null or empty.", nameof(workflowId));
        }

        var parts = workflowId.Split(':');
        
        if (parts.Length != 4 || parts[2] != "Task Workflow")
        {
            throw new ArgumentException(
                $"Invalid task workflow ID format. Expected '{{tenantId}}:{{agentName}}:Task Workflow:{{taskId}}', got '{workflowId}'",
                nameof(workflowId));
        }

        var tenantId = parts[0];
        var agentName = parts[1];
        var taskId = parts[3];

        _logger.LogDebug("Parsed workflow ID: TenantId={TenantId}, AgentName={AgentName}, TaskId={TaskId}", 
            tenantId, agentName, taskId);

        if (!XiansContext.TryGetAgent(agentName, out var agent) || agent == null)
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' not found in registry. Cannot access task workflow.");
        }
        
        if (agent.TemporalService == null)
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' does not have a Temporal service configured. Cannot access task workflow.");
        }

        var client = await agent.TemporalService.GetClientAsync();
        return new HitlTask(taskId, tenantId, agentName, client);
    }

    /// <summary>
    /// Gets the current task information.
    /// </summary>
    public async Task<TaskInfo> GetInfoAsync()
    {
        _logger.LogDebug("Getting info for task: TaskId={TaskId}", _taskId);
        return await TaskWorkflowService.QueryTaskInfoAsync(_client, _agentName, _tenantId, _taskId);
    }

    /// <summary>
    /// Updates the draft work for the task.
    /// </summary>
    public async Task UpdateDraftAsync(string updatedDraft)
    {
        _logger.LogDebug("Updating draft for task: TaskId={TaskId}", _taskId);
        await TaskWorkflowService.SignalUpdateDraftAsync(_client, _agentName, _tenantId, _taskId, updatedDraft);
    }

    /// <summary>
    /// Performs an action on the task with an optional comment.
    /// </summary>
    public async Task PerformActionAsync(string action, string? comment = null)
    {
        _logger.LogDebug("Performing action on task: TaskId={TaskId}, Action={Action}", _taskId, action);
        await TaskWorkflowService.SignalPerformActionAsync(_client, _agentName, _tenantId, _taskId, action, comment);
    }

    /// <summary>
    /// Approves the task with an optional comment.
    /// </summary>
    public async Task ApproveAsync(string? comment = null)
    {
        await PerformActionAsync("approve", comment);
    }

    /// <summary>
    /// Rejects the task with an optional comment.
    /// </summary>
    public async Task RejectAsync(string? comment = null)
    {
        await PerformActionAsync("reject", comment);
    }

    /// <summary>
    /// Gets the current draft work content.
    /// </summary>
    public async Task<string?> GetInitialWorkAsync()
    {
        var info = await GetInfoAsync();
        return info.InitialWork;
    }

    /// <summary>
    /// Gets the final work content.
    /// </summary>
    public async Task<string?> GetFinalWorkAsync()
    {
        var info = await GetInfoAsync();
        return info.FinalWork;
    }

    /// <summary>
    /// Gets the action that was performed on the task.
    /// </summary>
    public async Task<string?> GetPerformedActionAsync()
    {
        var info = await GetInfoAsync();
        return info.PerformedAction;
    }

    /// <summary>
    /// Gets the comment associated with the performed action.
    /// </summary>
    public async Task<string?> GetCommentAsync()
    {
        var info = await GetInfoAsync();
        return info.Comment;
    }

    /// <summary>
    /// Gets the available actions for this task.
    /// </summary>
    public async Task<string[]?> GetAvailableActionsAsync()
    {
        var info = await GetInfoAsync();
        return info.AvailableActions;
    }

    /// <summary>
    /// Checks if the task is completed.
    /// </summary>
    public async Task<bool> IsCompletedAsync()
    {
        var info = await GetInfoAsync();
        return info.IsCompleted;
    }

    /// <summary>
    /// Checks if the task is pending.
    /// </summary>
    public async Task<bool> IsPendingAsync()
    {
        var info = await GetInfoAsync();
        return !info.IsCompleted;
    }

    /// <summary>
    /// Gets the task title.
    /// </summary>
    public async Task<string> GetTitleAsync()
    {
        var info = await GetInfoAsync();
        return info.Title;
    }

    /// <summary>
    /// Gets the task description.
    /// </summary>
    public async Task<string> GetDescriptionAsync()
    {
        var info = await GetInfoAsync();
        return info.Description;
    }

    /// <summary>
    /// Gets the task metadata.
    /// </summary>
    public async Task<Dictionary<string, object>?> GetMetadataAsync()
    {
        var info = await GetInfoAsync();
        return info.Metadata;
    }

    /// <summary>
    /// Gets the typed workflow handle for advanced operations.
    /// </summary>
    public WorkflowHandle GetWorkflowHandle()
    {
        return TaskWorkflowService.GetTaskHandleForClient(_client, _agentName, _tenantId, _taskId);
    }

    public override string ToString()
    {
        return $"HitlTask(TaskId={_taskId}, TenantId={_tenantId}, AgentName={_agentName})";
    }
}
