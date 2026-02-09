using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Common;
using Xians.Lib.Common.MultiTenancy;
using Xians.Lib.Temporal.Workflows.Tasks;

namespace Xians.Lib.Agents.Tasks;

/// <summary>
/// Service for creating and managing human-in-the-loop task workflows.
/// Provides methods to start tasks and wait for their completion.
/// </summary>
public static class TaskWorkflowService
{
    private static readonly ILogger _logger = Common.Infrastructure.LoggerFactory.CreateLogger<TaskWorkflowServiceLogger>();

    private class TaskWorkflowServiceLogger { }

    #region Private Helper Methods

    private static string GetTaskWorkflowType()
    {
        var agentName = XiansContext.CurrentAgent?.Name 
            ?? throw new InvalidOperationException("Agent name not available in workflow context");
        return WorkflowConstants.WorkflowTypes.GetTaskWorkflowType(agentName);
    }

    private static TaskWorkflowOptions CreateWorkflowOptions(TaskWorkflowRequest request)
    {
        return new TaskWorkflowOptions(request);
    }

    #endregion

    #region Workflow-Context Methods (Within Workflow)

    /// <summary>
    /// Creates a task child workflow and waits for its completion.
    /// </summary>
    public static async Task<TaskWorkflowResult> CreateAndWaitAsync(TaskWorkflowRequest request)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "CreateAndWaitAsync can only be called from within a workflow context.");
        }

        var options = CreateWorkflowOptions(request);

        var result = await Workflow.ExecuteChildWorkflowAsync<TaskWorkflowResult>(
            GetTaskWorkflowType(),
            new[] { request },
            options);


        return result;
    }

    /// <summary>
    /// Starts a task child workflow and returns a handle without waiting for completion.
    /// </summary>
    public static async Task<ChildWorkflowHandle> StartTaskAsync(TaskWorkflowRequest request)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "StartTaskAsync can only be called from within a workflow context.");
        }

        var options = CreateWorkflowOptions(request);

        var handle = await Workflow.StartChildWorkflowAsync(
            GetTaskWorkflowType(),
            new[] { request },
            options);

        return handle;
    }

    /// <summary>
    /// Awaits the result of a task workflow using its handle.
    /// </summary>
    public static async Task<TaskWorkflowResult> GetResultAsync(ChildWorkflowHandle handle)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "GetResultAsync can only be called from within a workflow context.");
        }

        var result = await handle.GetResultAsync<TaskWorkflowResult>();

        return result;
    }

    /// <summary>
    /// Creates a task child workflow without waiting for completion (fire and forget).
    /// </summary>
    public static async Task CreateAsync(TaskWorkflowRequest request)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "CreateAsync can only be called from within a workflow context.");
        }

        var options = CreateWorkflowOptions(request);

        await Workflow.StartChildWorkflowAsync(
            GetTaskWorkflowType(),
            new[] { request },
            options);
    }

    /// <summary>
    /// Creates a task with a simplified interface (fire and forget).
    /// </summary>
    public static async Task CreateAsync(
        string? taskId,
        string title,
        string description,
        string? participantId = null,
        string? draftWork = null,
        string[]? actions = null,
        Dictionary<string, object>? metadata = null)
    {
        var request = new TaskWorkflowRequest
        {
            Title = title,
            Description = description,
            ParticipantId = participantId,
            DraftWork = draftWork,
            Actions = actions,
            Metadata = metadata
        };

        await CreateAsync(request);
    }

    /// <summary>
    /// Creates a task with a simplified interface and waits for completion.
    /// </summary>
    public static async Task<TaskWorkflowResult> CreateAndWaitAsync(
        string? taskId,
        string title,
        string description,
        string? participantId = null,
        string? draftWork = null,
        string[]? actions = null,
        Dictionary<string, object>? metadata = null)
    {
        var request = new TaskWorkflowRequest
        {
            Title = title,
            Description = description,
            ParticipantId = participantId,
            DraftWork = draftWork,
            Actions = actions,
            Metadata = metadata
        };

        return await CreateAndWaitAsync(request);
    }

    #endregion

    #region Dual-Context Signal Methods

    private static ExternalWorkflowHandle GetTaskHandle(string taskId)
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "Task signals can only be sent from within a workflow context.");
        }

        var tenantId = XiansContext.TenantId;
        var workflowId = $"{tenantId}:{GetTaskWorkflowType()}:{taskId}";

        return Workflow.GetExternalWorkflowHandle(workflowId);
    }

    /// <summary>
    /// Updates the draft work for a task.
    /// </summary>
    public static async Task UpdateDraftAsync(string taskId, string updatedDraft, string? tenantId = null)
    {
        if (Workflow.InWorkflow)
        {
            var handle = GetTaskHandle(taskId);
            await handle.SignalAsync("UpdateDraft", new object[] { updatedDraft });
            _logger.LogDebug("Draft updated for task: TaskId={TaskId}", taskId);
        }
        else
        {
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
    /// Performs an action on a task with an optional comment.
    /// </summary>
    public static async Task PerformActionAsync(string taskId, string action, string? comment = null, string? tenantId = null)
    {
        var actionRequest = new TaskActionRequest { Action = action, Comment = comment };
        
        if (Workflow.InWorkflow)
        {
            var handle = GetTaskHandle(taskId);
            await handle.SignalAsync("PerformAction", new object[] { actionRequest });
            _logger.LogDebug("Action performed on task: TaskId={TaskId}, Action={Action}", taskId, action);
        }
        else
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentException("Tenant ID is required when calling from outside workflow context.", nameof(tenantId));
            }
            
            var agent = GetAgentFromTaskId();
            var client = await agent.TemporalService!.GetClientAsync();
            await SignalPerformActionAsync(client, agent.Name, tenantId, taskId, action, comment);
        }
    }

    private static XiansAgent GetAgentFromTaskId()
    {
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
    /// Gets a workflow handle for querying a task from outside a workflow context.
    /// </summary>
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
    /// </summary>
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
    public static async Task SignalUpdateDraftAsync(
        ITemporalClient client,
        string agentName,
        string tenantId,
        string taskId,
        string updatedDraft)
    {
        var handle = GetTaskHandleForClient(client, agentName, tenantId, taskId);
        await handle.SignalAsync("UpdateDraft", new object[] { updatedDraft });
        
        _logger.LogDebug("Draft updated for task via client: TaskId={TaskId}", taskId);
    }

    /// <summary>
    /// Sends a signal to perform an action on a task from outside a workflow context.
    /// </summary>
    public static async Task SignalPerformActionAsync(
        ITemporalClient client,
        string agentName,
        string tenantId,
        string taskId,
        string action,
        string? comment = null)
    {
        var handle = GetTaskHandleForClient(client, agentName, tenantId, taskId);
        var actionRequest = new TaskActionRequest { Action = action, Comment = comment };
        await handle.SignalAsync("PerformAction", new object[] { actionRequest });
        
        _logger.LogDebug("Action performed on task via client: TaskId={TaskId}, Action={Action}", taskId, action);
    }

    #endregion
}
