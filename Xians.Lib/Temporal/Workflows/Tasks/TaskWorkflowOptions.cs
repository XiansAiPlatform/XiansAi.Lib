using Temporalio.Common;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Tasks.Models;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Common.MultiTenancy;

namespace Xians.Lib.Temporal.Workflows.Tasks;

/// <summary>
/// Options for starting task child workflows.
/// Includes standard search attributes/memos plus task-specific attributes.
/// </summary>
public class TaskWorkflowOptions : ChildWorkflowOptions
{
    /// <summary>
    /// Creates options for a task child workflow.
    /// </summary>
    /// <param name="request">Request containing task details.</param>
    public TaskWorkflowOptions(TaskWorkflowRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Task title cannot be null or empty.", nameof(request.Title));
        }

        // Always inherit system-scoped setting from parent workflow
        var isSystemScoped = GetSystemScopedFromParent();

        // Get tenant ID from parent workflow
        var tenantId = XiansContext.TenantId;

        // Get agent name and construct task workflow type
        var agentName = XiansContext.CurrentAgent?.Name 
            ?? throw new InvalidOperationException("Agent name not available in workflow context");
        
        // If participantId not provided, inherit from parent workflow's UserId
        var effectiveParticipantId = request.ParticipantId ?? GetUserIdFromParent();
        
        var taskWorkflowType = WorkflowConstants.WorkflowTypes.GetTaskWorkflowType(agentName);

        // Generate task queue using centralized utility
        TaskQueue = TenantContext.GetTaskQueueName(taskWorkflowType, isSystemScoped, tenantId);

        var idPostfix = XiansContext.GetIdPostfix();
        // Generate workflow ID with task ID as postfix
        Id = TenantContext.BuildWorkflowId(taskWorkflowType, tenantId, idPostfix) + "--" + request.TaskName;

        // Default actions if not provided
        var effectiveActions = request.Actions is { Length: > 0 } ? request.Actions : ["OK"];
        
        // Inherit all parent workflow's memo and search attributes, then add task-specific attributes
        Memo = BuildInheritedMemo(tenantId, agentName, isSystemScoped, request.Title, request.Description, effectiveParticipantId, effectiveActions);
        TypedSearchAttributes = BuildInheritedSearchAttributes(tenantId, agentName, effectiveParticipantId);

        // Set workflow summary for debugging
        StaticSummary = $"Task workflow '{request.Title}' in '{XiansContext.WorkflowId}'";

        // Default retry policy: single attempt (fail fast)
        RetryPolicy =  request.RetryPolicy ?? new RetryPolicy { MaximumAttempts = 1 };

        // Task workflow should be abandoned when parent closes
        ParentClosePolicy = request.SurviveParentClose ?? true ? ParentClosePolicy.Abandon : ParentClosePolicy.Terminate;

        // Set execution timeout to 1 day from the request timeout
        ExecutionTimeout = request.Timeout?.Add(TimeSpan.FromDays(1));

        // If a workflow with the same ID is already running, terminate it
        IdReusePolicy = Temporalio.Api.Enums.V1.WorkflowIdReusePolicy.TerminateIfRunning;

    }

    /// <summary>
    /// Gets the system-scoped flag from the parent workflow's memo.
    /// </summary>
    private static bool GetSystemScopedFromParent()
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "TaskWorkflowOptions can only be created within a workflow context.");
        }

        // Try to get from memo
        if (Workflow.Memo.TryGetValue(WorkflowConstants.Keys.SystemScoped, out var value))
        {
            var stringValue = value.Payload.Data.ToStringUtf8();
            return stringValue == "true" || stringValue == "True";
        }

        // Default to false if not set
        return false;
    }

    /// <summary>
    /// Gets the UserId from the parent workflow's memo.
    /// </summary>
    private static string GetUserIdFromParent()
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "TaskWorkflowOptions can only be created within a workflow context. ParticipantId cannot be inherited from parent workflow. Must be explicitly provided.");
        }

        // Try to get from memo
        if (Workflow.Memo.TryGetValue(WorkflowConstants.Keys.UserId, out var value))
        {
            var stringValue = value.Payload.Data.ToStringUtf8();
            // Remove quotes if present (JSON string serialization)
            return stringValue.Trim('"');
        }

        throw new InvalidOperationException(
            "Parent workflow does not have a UserId in memo. Cannot determine task participant id. Must be explicitly provided.");
    }

    /// <summary>
    /// Builds the memo dictionary for the task workflow by inheriting all parent memo entries
    /// and adding task-specific attributes (title, description, participantId, actions).
    /// </summary>
    private static Dictionary<string, object> BuildInheritedMemo(
        string tenantId, 
        string agentName,
        bool systemScoped,
        string title,
        string description,
        string participantId,
        string[] actions)
    {
        // Inherit all parent workflow's memo entries
        var memo = SubWorkflowService.BuildInheritedMemo(tenantId, agentName, systemScoped);
        
        // Add task-specific attributes
        memo[WorkflowConstants.Keys.UserId] = participantId;
        memo[WorkflowConstants.Keys.TaskTitle] = title;
        memo[WorkflowConstants.Keys.TaskDescription] = description;
        memo[WorkflowConstants.Keys.TaskActions] = string.Join(",", actions);
        
        return memo;
    }

    /// <summary>
    /// Builds search attributes for the task workflow with standard fields plus participantId.
    /// Unlike SubWorkflowService which inherits parent search attributes as-is, task workflows
    /// always include the participantId as a searchable attribute for querying tasks by user.
    /// </summary>
    private static SearchAttributeCollection BuildInheritedSearchAttributes(
        string tenantId, 
        string agentName,
        string participantId) =>
        WorkflowMetadataResolver.BuildSearchAttributes(tenantId, agentName, participantId, XiansContext.GetIdPostfix());
}

