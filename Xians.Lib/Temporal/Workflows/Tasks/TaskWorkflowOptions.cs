using Temporalio.Common;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
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
    /// <param name="taskId">Unique identifier for the task (used as workflow ID postfix).</param>
    /// <param name="title">Task title (added to memo for display purposes).</param>
    /// <param name="description">Task description (added to memo for detailed information).</param>
    /// <param name="participantId">User ID of the task participant. If null, inherits from parent workflow's UserId.</param>
    /// <param name="actions">Available actions for this task. If null/empty, defaults to ["approve", "reject"].</param>
    /// <param name="retryPolicy">Optional retry policy. Defaults to MaximumAttempts=1.</param>
    public TaskWorkflowOptions(
        string taskId,
        string title,
        string description,
        string? participantId,
        string[]? actions = null,
        RetryPolicy? retryPolicy = null)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("Task ID cannot be null or empty.", nameof(taskId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Task title cannot be null or empty.", nameof(title));
        }

        // Always inherit system-scoped setting from parent workflow
        var isSystemScoped = GetSystemScopedFromParent();

        // Get tenant ID from parent workflow
        var tenantId = XiansContext.TenantId;

        // Get agent name and construct task workflow type
        var agentName = XiansContext.CurrentAgent?.Name 
            ?? throw new InvalidOperationException("Agent name not available in workflow context");
        
        // If participantId not provided, inherit from parent workflow's UserId
        var effectiveParticipantId = participantId ?? GetUserIdFromParent();
        
        var taskWorkflowType = WorkflowConstants.WorkflowTypes.GetTaskWorkflowType(agentName);

        // Generate task queue using centralized utility
        TaskQueue = TenantContext.GetTaskQueueName(taskWorkflowType, isSystemScoped, tenantId);

        // Generate workflow ID with task ID as postfix
        Id = TenantContext.BuildWorkflowId(taskWorkflowType, tenantId, taskId);

        // Default actions if not provided
        var effectiveActions = actions is { Length: > 0 } ? actions : new[] { "approve", "reject" };
        
        // Inherit all parent workflow's memo and search attributes, then add task-specific attributes
        Memo = BuildInheritedMemo(tenantId, agentName, isSystemScoped, title, description, effectiveParticipantId, effectiveActions);
        TypedSearchAttributes = BuildInheritedSearchAttributes(tenantId, agentName, effectiveParticipantId);

        // Set workflow summary for debugging
        StaticSummary = $"Task workflow '{title}' (ID: {taskId}) in '{XiansContext.WorkflowId}'";

        // Default retry policy: single attempt (fail fast)
        RetryPolicy = retryPolicy ?? new RetryPolicy { MaximumAttempts = 1 };

        // Task workflow should be abandoned when parent closes
        ParentClosePolicy = ParentClosePolicy.Terminate;
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
        string participantId)
    {
        // Build search attributes with all standard fields plus participantId
        // Note: We don't inherit custom search attributes from parent because we need to
        // ensure participantId is always present for task querying capabilities
        return new SearchAttributeCollection.Builder()
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.TenantId), tenantId)
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.Agent), agentName)
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.idPostfix), XiansContext.GetIdPostfix())
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.UserId), participantId)
            .ToSearchAttributeCollection();
    }
}

