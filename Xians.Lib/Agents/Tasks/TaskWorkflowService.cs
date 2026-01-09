using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Common;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Temporal.Workflows;
using Xians.Lib.Temporal.Workflows.Tasks;

namespace Xians.Lib.Agents.Tasks;

/// <summary>
/// Service for creating and managing human-in-the-loop task workflows.
/// Provides methods to start tasks and wait for their completion.
/// </summary>
public static class TaskWorkflowService
{
    private static readonly ILogger _logger = Xians.Lib.Common.Infrastructure.LoggerFactory.CreateLogger<TaskWorkflowServiceLogger>();

    // Dummy class for logger typing
    private class TaskWorkflowServiceLogger { }

    #region Private Helper Methods

    /// <summary>
    /// Gets the agent-specific task workflow type name from the current workflow context.
    /// </summary>
    private static string GetTaskWorkflowType()
    {
        var agentName = XiansContext.CurrentAgent?.Name 
            ?? throw new InvalidOperationException("Agent name not available in workflow context");
        return WorkflowConstants.WorkflowTypes.GetTaskWorkflowType(agentName);
    }

    /// <summary>
    /// Ensures the request has a TaskId, generating one if needed.
    /// </summary>
    private static TaskWorkflowRequest EnsureTaskId(TaskWorkflowRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TaskId))
        {
            return request;
        }

        return request with { TaskId = Guid.NewGuid().ToString() };
    }

    /// <summary>
    /// Creates task workflow options from a request.
    /// </summary>
    private static TaskWorkflowOptions CreateWorkflowOptions(TaskWorkflowRequest request)
    {
        return new TaskWorkflowOptions(
            taskId: request.TaskId!,
            title: request.Title,
            description: request.Description,
            participantId: request.ParticipantId);
    }

    #endregion

    #region Workflow-Context Methods (Within Workflow)

    /// <summary>
    /// Creates a task child workflow and waits for its completion.
    /// </summary>
    /// <param name="request">The task workflow request.</param>
    /// <returns>The task workflow result.</returns>
    public static async Task<TaskWorkflowResult> CreateAndWaitAsync(TaskWorkflowRequest request)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "CreateAndWaitAsync can only be called from within a workflow context.");
        }

        var requestWithTaskId = EnsureTaskId(request);
        var options = CreateWorkflowOptions(requestWithTaskId);

        _logger.LogInformation(
            "Creating task workflow: TaskId={TaskId}, Title={Title}, ParticipantId={ParticipantId}",
            requestWithTaskId.TaskId,
            requestWithTaskId.Title,
            requestWithTaskId.ParticipantId);

        var result = await Workflow.ExecuteChildWorkflowAsync<TaskWorkflowResult>(
            GetTaskWorkflowType(),
            new[] { requestWithTaskId },
            options);

        _logger.LogInformation(
            "Task workflow completed: TaskId={TaskId}, Success={Success}",
            result.TaskId,
            result.Success);

        return result;
    }

    /// <summary>
    /// Starts a task child workflow and returns a handle without waiting for completion.
    /// Use GetResultAsync with the returned handle to await the result.
    /// </summary>
    /// <param name="request">The task workflow request.</param>
    /// <returns>A child workflow handle that can be used to get the result.</returns>
    public static async Task<ChildWorkflowHandle> StartTaskAsync(TaskWorkflowRequest request)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "StartTaskAsync can only be called from within a workflow context.");
        }

        var requestWithTaskId = EnsureTaskId(request);
        var options = CreateWorkflowOptions(requestWithTaskId);

        _logger.LogInformation(
            "Starting task workflow: TaskId={TaskId}, Title={Title}, ParticipantId={ParticipantId}",
            requestWithTaskId.TaskId,
            requestWithTaskId.Title,
            requestWithTaskId.ParticipantId);

        var handle = await Workflow.StartChildWorkflowAsync(
            GetTaskWorkflowType(),
            new[] { requestWithTaskId },
            options);

        _logger.LogInformation(
            "Task workflow started: TaskId={TaskId}",
            requestWithTaskId.TaskId);

        return handle;
    }

    /// <summary>
    /// Awaits the result of a task workflow using its handle.
    /// </summary>
    /// <param name="handle">The child workflow handle returned from StartTaskAsync.</param>
    /// <returns>The task workflow result.</returns>
    public static async Task<TaskWorkflowResult> GetResultAsync(
        ChildWorkflowHandle handle)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "GetResultAsync can only be called from within a workflow context.");
        }

        var result = await handle.GetResultAsync<TaskWorkflowResult>();

        _logger.LogInformation(
            "Task workflow completed: TaskId={TaskId}, Success={Success}",
            result.TaskId,
            result.Success);

        return result;
    }

    /// <summary>
    /// Creates a task child workflow without waiting for completion (fire and forget).
    /// </summary>
    /// <param name="request">The task workflow request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task CreateAsync(TaskWorkflowRequest request)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "CreateAsync can only be called from within a workflow context.");
        }

        var requestWithTaskId = EnsureTaskId(request);
        var options = CreateWorkflowOptions(requestWithTaskId);

        _logger.LogInformation(
            "Starting task workflow (fire and forget): TaskId={TaskId}, Title={Title}, ParticipantId={ParticipantId}",
            requestWithTaskId.TaskId,
            requestWithTaskId.Title,
            requestWithTaskId.ParticipantId);

        await Workflow.StartChildWorkflowAsync(
            GetTaskWorkflowType(),
            new[] { requestWithTaskId },
            options);

        _logger.LogInformation(
            "Task workflow started: TaskId={TaskId}",
            requestWithTaskId.TaskId);
    }

    /// <summary>
    /// Creates a task with a simplified interface (fire and forget).
    /// </summary>
    /// <param name="taskId">Unique identifier for the task. If null, a GUID will be auto-generated.</param>
    /// <param name="title">Task title.</param>
    /// <param name="description">Task description.</param>
    /// <param name="participantId">User ID of the task participant.</param>
    /// <param name="draftWork">Initial draft work content.</param>
    /// <param name="metadata">Additional metadata for the task.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task CreateAsync(
        string? taskId,
        string title,
        string description,
        string participantId,
        string? draftWork = null,
        Dictionary<string, object>? metadata = null)
    {
        var request = new TaskWorkflowRequest
        {
            TaskId = taskId,
            Title = title,
            Description = description,
            ParticipantId = participantId,
            DraftWork = draftWork,
            Metadata = metadata
        };

        await CreateAsync(request);
    }

    /// <summary>
    /// Creates a task with a simplified interface and waits for completion.
    /// </summary>
    /// <param name="taskId">Unique identifier for the task. If null, a GUID will be auto-generated.</param>
    /// <param name="title">Task title.</param>
    /// <param name="description">Task description.</param>
    /// <param name="participantId">User ID of the task participant.</param>
    /// <param name="draftWork">Initial draft work content.</param>
    /// <param name="metadata">Additional metadata for the task.</param>
    /// <returns>The task workflow result.</returns>
    public static async Task<TaskWorkflowResult> CreateAndWaitAsync(
        string? taskId,
        string title,
        string description,
        string participantId,
        string? draftWork = null,
        Dictionary<string, object>? metadata = null)
    {
        var request = new TaskWorkflowRequest
        {
            TaskId = taskId,
            Title = title,
            Description = description,
            ParticipantId = participantId,
            DraftWork = draftWork,
            Metadata = metadata
        };

        return await CreateAndWaitAsync(request);
    }

    #endregion

    #region Dual-Context Signal Methods

    /// <summary>
    /// Gets the workflow handle for an existing task workflow.
    /// Can be used to send signals to the task.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <returns>A workflow handle for the task workflow.</returns>
    private static ExternalWorkflowHandle GetTaskHandle(string taskId)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "Task signals can only be sent from within a workflow context.");
        }

        // Build the workflow ID following the same pattern as TaskWorkflowOptions
        var tenantId = XiansContext.TenantId;
        var workflowId = $"{tenantId}:{GetTaskWorkflowType()}:{taskId}";

        return Workflow.GetExternalWorkflowHandle(workflowId);
    }

    /// <summary>
    /// Updates the draft work for a task.
    /// Automatically detects context: uses ExternalWorkflowHandle from within workflows,
    /// or WorkflowHandle via Temporal client from outside workflows.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <param name="updatedDraft">The updated draft content.</param>
    /// <param name="tenantId">The tenant ID (required only when called from outside workflow context).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task UpdateDraftAsync(string taskId, string updatedDraft, string? tenantId = null)
    {
        if (Workflow.InWorkflow)
        {
            // Within workflow - use external workflow handle
            var handle = GetTaskHandle(taskId);
            await handle.SignalAsync("UpdateDraft", new object[] { updatedDraft });
            _logger.LogInformation("Draft updated for task from workflow: TaskId={TaskId}", taskId);
        }
        else
        {
            // Outside workflow - use Temporal client
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentException("Tenant ID is required when calling from outside workflow context.", nameof(tenantId));
            }
            
            var agent = GetAgentFromTaskId();
            var client = await agent.TemporalService!.GetClientAsync();
            await SignalUpdateDraftAsync(client, agent.Name, tenantId, taskId, updatedDraft);
        }
    }

    /// <summary>
    /// Marks a task as completed.
    /// Automatically detects context: uses ExternalWorkflowHandle from within workflows,
    /// or WorkflowHandle via Temporal client from outside workflows.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <param name="tenantId">The tenant ID (required only when called from outside workflow context).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task CompleteTaskAsync(string taskId, string? tenantId = null)
    {
        if (Workflow.InWorkflow)
        {
            // Within workflow - use external workflow handle
            var handle = GetTaskHandle(taskId);
            await handle.SignalAsync("CompleteTask", Array.Empty<object>());
            _logger.LogInformation("Task marked as complete from workflow: TaskId={TaskId}", taskId);
        }
        else
        {
            // Outside workflow - use Temporal client
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentException("Tenant ID is required when calling from outside workflow context.", nameof(tenantId));
            }
            
            var agent = GetAgentFromTaskId();
            var client = await agent.TemporalService!.GetClientAsync();
            await SignalCompleteTaskAsync(client, agent.Name, tenantId, taskId);
        }
    }

    /// <summary>
    /// Rejects a task with a rejection message.
    /// Automatically detects context: uses ExternalWorkflowHandle from within workflows,
    /// or WorkflowHandle via Temporal client from outside workflows.
    /// </summary>
    /// <param name="taskId">The task ID.</param>
    /// <param name="rejectionMessage">The reason for rejection.</param>
    /// <param name="tenantId">The tenant ID (required only when called from outside workflow context).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RejectTaskAsync(string taskId, string rejectionMessage, string? tenantId = null)
    {
        if (Workflow.InWorkflow)
        {
            // Within workflow - use external workflow handle
            var handle = GetTaskHandle(taskId);
            await handle.SignalAsync("RejectTask", new object[] { rejectionMessage });
            _logger.LogWarning("Task rejected from workflow: TaskId={TaskId}, Reason={Reason}", taskId, rejectionMessage);
        }
        else
        {
            // Outside workflow - use Temporal client
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentException("Tenant ID is required when calling from outside workflow context.", nameof(tenantId));
            }
            
            var agent = GetAgentFromTaskId();
            var client = await agent.TemporalService!.GetClientAsync();
            await SignalRejectTaskAsync(client, agent.Name, tenantId, taskId, rejectionMessage);
        }
    }

    /// <summary>
    /// Helper to get the Platform agent for task operations.
    /// </summary>
    private static XiansAgent GetAgentFromTaskId()
    {
        // Task workflows are owned by the Platform agent
        var agentName = "Platform";
        if (!XiansContext.TryGetAgent(agentName, out var agent) || agent == null)
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' not found in registry. Task workflows require the Platform agent to be registered.");
        }
        
        if (agent.TemporalService == null)
        {
            throw new InvalidOperationException(
                "Platform agent does not have a Temporal service configured. Cannot perform task operations outside of workflow context.");
        }
        
        return agent;
    }

    #endregion

    #region Client-Side Operations (Outside Workflow Context)

    /// <summary>
    /// Gets a typed workflow handle for querying a task from outside a workflow context.
    /// Use this when you need to query task status from a UI, API, or activity.
    /// </summary>
    /// <param name="client">The Temporal client.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="taskId">The task ID.</param>
    /// <returns>A typed workflow handle for the task.</returns>
    public static WorkflowHandle GetTaskHandleForClient(
        ITemporalClient client,
        string agentName,
        string tenantId,
        string taskId)
    {
        var taskWorkflowType = WorkflowConstants.WorkflowTypes.GetTaskWorkflowType(agentName);
        var workflowId = TenantContext.BuildWorkflowId(taskWorkflowType, tenantId, taskId);
        return client.GetWorkflowHandle(workflowId);
    }

    /// <summary>
    /// Queries the current status of a task workflow from outside a workflow context.
    /// Use this from UIs, APIs, or activities to get task information.
    /// </summary>
    /// <param name="client">The Temporal client.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="taskId">The task ID.</param>
    /// <returns>The current task information.</returns>
    public static async Task<TaskInfo> QueryTaskInfoAsync(
        ITemporalClient client,
        string agentName,
        string tenantId,
        string taskId)
    {
        var handle = GetTaskHandleForClient(client, agentName, tenantId, taskId);
        return await handle.QueryAsync<TaskInfo>("GetTaskInfo", Array.Empty<object>());
    }

    /// <summary>
    /// Sends a signal to update the draft work from outside a workflow context.
    /// </summary>
    /// <param name="client">The Temporal client.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="taskId">The task ID.</param>
    /// <param name="updatedDraft">The updated draft content.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task SignalUpdateDraftAsync(
        ITemporalClient client,
        string agentName,
        string tenantId,
        string taskId,
        string updatedDraft)
    {
        var handle = GetTaskHandleForClient(client, agentName, tenantId, taskId);
        await handle.SignalAsync("UpdateDraft", new object[] { updatedDraft });
        
        _logger.LogInformation("Draft updated for task via client: TaskId={TaskId}", taskId);
    }

    /// <summary>
    /// Sends a signal to complete the task from outside a workflow context.
    /// </summary>
    /// <param name="client">The Temporal client.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="taskId">The task ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task SignalCompleteTaskAsync(
        ITemporalClient client,
        string agentName,
        string tenantId,
        string taskId)
    {
        var handle = GetTaskHandleForClient(client, agentName, tenantId, taskId);
        await handle.SignalAsync("CompleteTask", Array.Empty<object>());
        
        _logger.LogInformation("Task marked as complete via client: TaskId={TaskId}", taskId);
    }

    /// <summary>
    /// Sends a signal to reject the task from outside a workflow context.
    /// </summary>
    /// <param name="client">The Temporal client.</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="taskId">The task ID.</param>
    /// <param name="rejectionMessage">The reason for rejection.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task SignalRejectTaskAsync(
        ITemporalClient client,
        string agentName,
        string tenantId,
        string taskId,
        string rejectionMessage)
    {
        var handle = GetTaskHandleForClient(client, agentName, tenantId, taskId);
        await handle.SignalAsync("RejectTask", new object[] { rejectionMessage });
        
        _logger.LogWarning("Task rejected via client: TaskId={TaskId}, Reason={Reason}", taskId, rejectionMessage);
    }

    #endregion
}
