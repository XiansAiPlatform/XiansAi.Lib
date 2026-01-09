using Temporalio.Client;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Temporal.Workflows;
using Xians.Lib.Temporal.Workflows.Tasks;

namespace Xians.Lib.Agents.Tasks;

/// <summary>
/// Collection wrapper for task workflow operations.
/// Provides instance-level access to task workflow functionality.
/// Uses activity executor pattern for context-aware operations.
/// </summary>
public class TaskCollection
{
    private readonly XiansAgent _agent;

    internal TaskCollection(XiansAgent agent)
    {
        _agent = agent;
    }

    /// <summary>
    /// Gets or creates the task activity executor for context-aware operations.
    /// </summary>
    private TaskActivityExecutor GetExecutor()
    {
        if (_agent.TemporalService == null)
        {
            throw new InvalidOperationException(
                "Temporal service is not configured. Cannot perform task operations.");
        }

        // Get tenant ID from XiansContext (in workflow) or use default (out of workflow)
        string tenantId;
        try
        {
            tenantId = XiansContext.TenantId;
        }
        catch
        {
            // Outside workflow context - use default tenant
            tenantId = "default";
        }

        var client = _agent.TemporalService.GetClientAsync().GetAwaiter().GetResult();
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<TaskActivityExecutor>();
        
        return new TaskActivityExecutor(client, tenantId, logger);
    }

    /// <summary>
    /// Creates a task child workflow and waits for its completion.
    /// </summary>
    /// <param name="request">The task workflow request.</param>
    /// <returns>The task workflow result.</returns>
    public Task<TaskWorkflowResult> CreateAndWaitAsync(TaskWorkflowRequest request)
        => TaskWorkflowService.CreateAndWaitAsync(request);

    /// <summary>
    /// Creates a task with a simplified interface and waits for completion.
    /// </summary>
    public Task<TaskWorkflowResult> CreateAndWaitAsync(
        string? taskId,
        string title,
        string description,
        string participantId,
        string? draftWork = null,
        Dictionary<string, object>? metadata = null)
        => TaskWorkflowService.CreateAndWaitAsync(taskId, title, description, participantId, draftWork, metadata);

    /// <summary>
    /// Starts a task child workflow and returns a handle without waiting for completion.
    /// </summary>
    /// <param name="request">The task workflow request.</param>
    /// <returns>A child workflow handle that can be used to get the result.</returns>
    public async Task<ChildWorkflowHandle> StartTaskAsync(TaskWorkflowRequest request)
    {
        return await TaskWorkflowService.StartTaskAsync(request);
    }

    /// <summary>
    /// Awaits the result of a task workflow using its handle.
    /// </summary>
    /// <param name="handle">The child workflow handle returned from StartTaskAsync.</param>
    /// <returns>The task workflow result.</returns>
    public async Task<TaskWorkflowResult> GetResultAsync(ChildWorkflowHandle handle)
    {
        return await TaskWorkflowService.GetResultAsync(handle);
    }

    /// <summary>
    /// Creates a task child workflow without waiting for completion (fire and forget).
    /// </summary>
    /// <param name="request">The task workflow request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task CreateAsync(TaskWorkflowRequest request)
        => TaskWorkflowService.CreateAsync(request);

    /// <summary>
    /// Creates a task with a simplified interface (fire and forget).
    /// </summary>
    public Task CreateAsync(
        string? taskId,
        string title,
        string description,
        string participantId,
        string? draftWork = null,
        Dictionary<string, object>? metadata = null)
        => TaskWorkflowService.CreateAsync(taskId, title, description, participantId, draftWork, metadata);

    /// <summary>
    /// Updates the draft work for a task.
    /// Works in both workflow and client contexts using activity executor pattern.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <param name="updatedDraft">The updated draft content.</param>
    /// <param name="tenantId">The tenant ID (optional, auto-detected if not provided).</param>
    public async Task UpdateDraftAsync(string taskId, string updatedDraft, string? tenantId = null)
    {
        var executor = GetExecutor();
        await executor.UpdateDraftAsync(taskId, updatedDraft);
    }

    /// <summary>
    /// Marks a task as completed.
    /// Works in both workflow and client contexts using activity executor pattern.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <param name="tenantId">The tenant ID (optional, auto-detected if not provided).</param>
    public async Task CompleteTaskAsync(string taskId, string? tenantId = null)
    {
        var executor = GetExecutor();
        await executor.CompleteTaskAsync(taskId);
    }

    /// <summary>
    /// Rejects a task with a rejection message.
    /// Works in both workflow and client contexts using activity executor pattern.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <param name="rejectionMessage">The reason for rejection.</param>
    /// <param name="tenantId">The tenant ID (optional, auto-detected if not provided).</param>
    public async Task RejectTaskAsync(string taskId, string rejectionMessage, string? tenantId = null)
    {
        var executor = GetExecutor();
        await executor.RejectTaskAsync(taskId, rejectionMessage);
    }

    /// <summary>
    /// Gets a typed workflow handle for querying a task from outside a workflow context.
    /// </summary>
    /// <param name="client">The Temporal client.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="taskId">The task ID.</param>
    /// <returns>A typed workflow handle for the task.</returns>
    public WorkflowHandle GetTaskHandleForClient(
        ITemporalClient client,
        string tenantId,
        string taskId)
    {
        return TaskWorkflowService.GetTaskHandleForClient(client, _agent.Name, tenantId, taskId);
    }

    /// <summary>
    /// Queries the current status of a task workflow.
    /// Works in both workflow and client contexts using activity executor pattern.
    /// </summary>
    /// <param name="client">The Temporal client.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="taskId">The task ID.</param>
    /// <returns>The current task information.</returns>
    public async Task<TaskInfo> QueryTaskInfoAsync(
        ITemporalClient client,
        string tenantId,
        string taskId)
    {
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
        var taskService = new TaskService(client, _agent.Name, tenantId, logger);
        return await taskService.QueryTaskInfoAsync(taskId);
    }

    /// <summary>
    /// Sends a signal to update the draft work from outside a workflow context.
    /// </summary>
    public async Task SignalUpdateDraftAsync(
        ITemporalClient client,
        string tenantId,
        string taskId,
        string updatedDraft)
    {
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
        var taskService = new TaskService(client, _agent.Name, tenantId, logger);
        await taskService.UpdateDraftAsync(taskId, updatedDraft);
    }

    /// <summary>
    /// Sends a signal to complete the task from outside a workflow context.
    /// </summary>
    public async Task SignalCompleteTaskAsync(
        ITemporalClient client,
        string tenantId,
        string taskId)
    {
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
        var taskService = new TaskService(client, _agent.Name, tenantId, logger);
        await taskService.CompleteTaskAsync(taskId);
    }

    /// <summary>
    /// Sends a signal to reject the task from outside a workflow context.
    /// </summary>
    public async Task SignalRejectTaskAsync(
        ITemporalClient client,
        string tenantId,
        string taskId,
        string rejectionMessage)
    {
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
        var taskService = new TaskService(client, _agent.Name, tenantId, logger);
        await taskService.RejectTaskAsync(taskId, rejectionMessage);
    }
}

