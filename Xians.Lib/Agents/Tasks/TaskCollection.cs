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

    private TaskActivityExecutor GetExecutor()
    {
        if (_agent.TemporalService == null)
        {
            throw new InvalidOperationException(
                "Temporal service is not configured. Cannot perform task operations.");
        }

        var tenantId = XiansContext.GetTenantId();

        var client = _agent.TemporalService.GetClientAsync().GetAwaiter().GetResult();
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<TaskActivityExecutor>();
        
        return new TaskActivityExecutor(client, tenantId, logger);
    }

    /// <summary>
    /// Creates a task child workflow and waits for its completion.
    /// </summary>
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
        string[]? actions = null,
        Dictionary<string, object>? metadata = null)
        => TaskWorkflowService.CreateAndWaitAsync(taskId, title, description, participantId, draftWork, actions, metadata);

    /// <summary>
    /// Starts a task child workflow and returns a handle without waiting for completion.
    /// </summary>
    public async Task<ChildWorkflowHandle> StartTaskAsync(TaskWorkflowRequest request)
    {
        return await TaskWorkflowService.StartTaskAsync(request);
    }

    /// <summary>
    /// Awaits the result of a task workflow using its handle.
    /// </summary>
    public async Task<TaskWorkflowResult> GetResultAsync(ChildWorkflowHandle handle)
    {
        return await TaskWorkflowService.GetResultAsync(handle);
    }

    /// <summary>
    /// Creates a task child workflow without waiting for completion (fire and forget).
    /// </summary>
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
        string[]? actions = null,
        Dictionary<string, object>? metadata = null)
        => TaskWorkflowService.CreateAsync(taskId, title, description, participantId, draftWork, actions, metadata);

    /// <summary>
    /// Updates the draft work for a task.
    /// </summary>
    public async Task UpdateDraftAsync(string taskId, string updatedDraft, string? tenantId = null)
    {
        var executor = GetExecutor();
        await executor.UpdateDraftAsync(taskId, updatedDraft);
    }

    /// <summary>
    /// Performs an action on a task with an optional comment.
    /// </summary>
    public async Task PerformActionAsync(string taskId, string action, string? comment = null, string? tenantId = null)
    {
        var executor = GetExecutor();
        await executor.PerformActionAsync(taskId, action, comment);
    }

    /// <summary>
    /// Gets a typed workflow handle for querying a task from outside a workflow context.
    /// </summary>
    public WorkflowHandle GetTaskHandleForClient(
        ITemporalClient client,
        string tenantId,
        string taskId)
    {
        return TaskWorkflowService.GetTaskHandleForClient(client, _agent.Name, tenantId, taskId);
    }

    /// <summary>
    /// Queries the current status of a task workflow.
    /// </summary>
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
    /// Sends a signal to perform an action on a task from outside a workflow context.
    /// </summary>
    public async Task SignalPerformActionAsync(
        ITemporalClient client,
        string tenantId,
        string taskId,
        string action,
        string? comment = null)
    {
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<TaskService>();
        var taskService = new TaskService(client, _agent.Name, tenantId, logger);
        await taskService.PerformActionAsync(taskId, action, comment);
    }
}
