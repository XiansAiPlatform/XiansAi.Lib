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
    private readonly ITemporalClient _client;

    /// <summary>
    /// Gets the task ID.
    /// </summary>
    public string TaskId => _taskId;

    /// <summary>
    /// Gets the tenant ID.
    /// </summary>
    public string TenantId => _tenantId;

    /// <summary>
    /// Initializes a new instance of the HitlTask class.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="client">The Temporal client instance.</param>
    public HitlTask(string taskId, string tenantId, ITemporalClient client)
    {
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        _tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Creates a HitlTask instance using the Platform agent's Temporal client.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <returns>A new HitlTask instance.</returns>
    // public static async Task<HitlTask> CreateAsync(string taskId, string tenantId)
    // {
    //     var agent = GetPlatformAgent();
    //     var client = await agent.TemporalService!.GetClientAsync();
    //     return new HitlTask(taskId, tenantId, client);
    // }

    /// <summary>
    /// Gets a HitlTask instance for an existing task workflow from its workflow ID.
    /// Workflow ID format: "{tenantId}:{agentName}:Task Workflow:{taskId}"
    /// </summary>
    /// <param name="workflowId">The full workflow ID.</param>
    /// <returns>A HitlTask instance for the existing workflow.</returns>
    /// <exception cref="ArgumentException">Thrown when the workflow ID format is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the agent is not found or doesn't have a Temporal service.</exception>
    public static async Task<HitlTask> FromWorkflowIdAsync(string workflowId)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            throw new ArgumentException("Workflow ID cannot be null or empty.", nameof(workflowId));
        }

        // Parse workflow ID: {tenantId}:{agentName}:Task Workflow:{taskId}
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

        // Get the agent that owns this task workflow
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

        // Get the Temporal client and create HitlTask instance
        var client = await agent.TemporalService.GetClientAsync();
        return new HitlTask(taskId, tenantId, client);
    }

    /// <summary>
    /// Gets the current task information including status, draft work, and metadata.
    /// </summary>
    /// <returns>The current task information.</returns>
    public async Task<TaskInfo> GetInfoAsync()
    {
        _logger.LogDebug("Getting info for task: TaskId={TaskId}", _taskId);
        return await TaskWorkflowService.QueryTaskInfoAsync(_client, _tenantId, _taskId);
    }

    /// <summary>
    /// Updates the draft work for the task.
    /// </summary>
    /// <param name="updatedDraft">The updated draft content.</param>
    public async Task UpdateDraftAsync(string updatedDraft)
    {
        _logger.LogInformation("Updating draft for task: TaskId={TaskId}", _taskId);
        await TaskWorkflowService.SignalUpdateDraftAsync(_client, _tenantId, _taskId, updatedDraft);
    }

    /// <summary>
    /// Approves/completes the task.
    /// </summary>
    public async Task ApproveAsync()
    {
        _logger.LogInformation("Approving task: TaskId={TaskId}", _taskId);
        await TaskWorkflowService.SignalCompleteTaskAsync(_client, _tenantId, _taskId);
    }

    /// <summary>
    /// Completes the task (alias for ApproveAsync).
    /// </summary>
    public async Task CompleteAsync()
    {
        await ApproveAsync();
    }

    /// <summary>
    /// Rejects the task with a rejection message.
    /// </summary>
    /// <param name="rejectionMessage">The reason for rejection.</param>
    public async Task RejectAsync(string rejectionMessage)
    {
        _logger.LogInformation("Rejecting task: TaskId={TaskId}, Reason={Reason}", _taskId, rejectionMessage);
        await TaskWorkflowService.SignalRejectTaskAsync(_client, _tenantId, _taskId, rejectionMessage);
    }

    /// <summary>
    /// Gets the current draft work content.
    /// </summary>
    /// <returns>The draft work content, or null if not set.</returns>
    public async Task<string?> GetDraftAsync()
    {
        var info = await GetInfoAsync();
        return info.CurrentDraft;
    }

    /// <summary>
    /// Gets whether the task is successful (approved).
    /// </summary>
    /// <returns>True if the task was approved, false otherwise.</returns>
    public async Task<bool> IsSuccessAsync()
    {
        var info = await GetInfoAsync();
        return info.Success;
    }

    /// <summary>
    /// Gets the rejection reason if the task was rejected.
    /// </summary>
    /// <returns>The rejection reason, or null if not rejected.</returns>
    public async Task<string?> GetRejectionReasonAsync()
    {
        var info = await GetInfoAsync();
        return info.RejectionReason;
    }

    /// <summary>
    /// Gets the task title.
    /// </summary>
    /// <returns>The task title.</returns>
    public async Task<string> GetTitleAsync()
    {
        var info = await GetInfoAsync();
        return info.Title;
    }

    /// <summary>
    /// Gets the task description.
    /// </summary>
    /// <returns>The task description.</returns>
    public async Task<string> GetDescriptionAsync()
    {
        var info = await GetInfoAsync();
        return info.Description;
    }

    /// <summary>
    /// Gets the task metadata.
    /// </summary>
    /// <returns>The task metadata dictionary, or null if not set.</returns>
    public async Task<Dictionary<string, object>?> GetMetadataAsync()
    {
        var info = await GetInfoAsync();
        return info.Metadata;
    }

    /// <summary>
    /// Checks if the task is completed (either approved or rejected).
    /// </summary>
    /// <returns>True if the task is completed, false otherwise.</returns>
    public async Task<bool> IsCompletedAsync()
    {
        var info = await GetInfoAsync();
        return info.IsCompleted;
    }

    /// <summary>
    /// Checks if the task is pending.
    /// </summary>
    /// <returns>True if the task is pending, false otherwise.</returns>
    public async Task<bool> IsPendingAsync()
    {
        var info = await GetInfoAsync();
        return !info.IsCompleted;
    }

    /// <summary>
    /// Checks if the task was rejected.
    /// </summary>
    /// <returns>True if the task was rejected, false otherwise.</returns>
    public async Task<bool> IsRejectedAsync()
    {
        var info = await GetInfoAsync();
        return info.IsCompleted && !info.Success;
    }

    /// <summary>
    /// Gets the typed workflow handle for advanced operations.
    /// </summary>
    /// <returns>A typed workflow handle for the task.</returns>
    public WorkflowHandle<TaskWorkflow> GetWorkflowHandle()
    {
        return TaskWorkflowService.GetTaskHandleForClient(_client, _tenantId, _taskId);
    }

    /// <summary>
    /// Helper to get the Platform agent for task operations.
    /// </summary>
    // private static XiansAgent GetPlatformAgent()
    // {
    //     var agentName = "Platform";
    //     if (!XiansContext.TryGetAgent(agentName, out var agent) || agent == null)
    //     {
    //         throw new InvalidOperationException(
    //             $"Agent '{agentName}' not found in registry. Task workflows require the Platform agent to be registered.");
    //     }
        
    //     if (agent.TemporalService == null)
    //     {
    //         throw new InvalidOperationException(
    //             "Platform agent does not have a Temporal service configured. Cannot perform task operations.");
    //     }
        
    //     return agent;
    // }

    /// <summary>
    /// Returns a string representation of the task.
    /// </summary>
    public override string ToString()
    {
        return $"HitlTask(TaskId={_taskId}, TenantId={_tenantId})";
    }
}

