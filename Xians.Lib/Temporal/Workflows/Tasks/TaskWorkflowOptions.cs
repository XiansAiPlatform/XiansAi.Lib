using Temporalio.Common;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
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
    /// <param name="participantId">User ID of the task participant (added to search attributes).</param>
    /// <param name="retryPolicy">Optional retry policy. Defaults to MaximumAttempts=1.</param>
    public TaskWorkflowOptions(
        string taskId,
        string title,
        string description,
        string participantId,
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
        
        var taskWorkflowType = WorkflowConstants.WorkflowTypes.GetTaskWorkflowType(agentName);

        // Generate task queue using centralized utility
        TaskQueue = TenantContext.GetTaskQueueName(taskWorkflowType, isSystemScoped, tenantId);

        // Generate workflow ID with task ID as postfix
        Id = TenantContext.BuildWorkflowId(taskWorkflowType, tenantId, taskId);

        // Build memo with standard attributes plus task-specific attributes
        Memo = BuildMemo(tenantId, isSystemScoped, title, description, participantId);

        // Build search attributes with standard attributes plus task-specific attributes
        TypedSearchAttributes = BuildSearchAttributes(tenantId, participantId);

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
    /// Builds the memo dictionary for the task workflow.
    /// Includes standard metadata plus task title and description.
    /// </summary>
    private static Dictionary<string, object> BuildMemo(
        string tenantId, 
        bool systemScoped,
        string title,
        string description,
        string participantId)
    {
        var agentName = XiansContext.CurrentAgent.Name;

        var memo = new Dictionary<string, object>
        {
            { WorkflowConstants.Keys.TenantId, tenantId },
            { WorkflowConstants.Keys.Agent, agentName },
            { WorkflowConstants.Keys.UserId, participantId },
            { WorkflowConstants.Keys.SystemScoped, systemScoped },
            { WorkflowConstants.Keys.TaskTitle, title },
            { WorkflowConstants.Keys.TaskDescription, description }
        };
        return memo;
    }

    /// <summary>
    /// Builds search attributes for the task workflow.
    /// Includes standard attributes plus taskId and participantId.
    /// </summary>
    private static SearchAttributeCollection BuildSearchAttributes(
        string tenantId, 
        string participantId)
    {
        var agentName = XiansContext.CurrentAgent.Name;

        var builder = new SearchAttributeCollection.Builder()
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.TenantId), tenantId)
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.Agent), agentName)
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.UserId), participantId);

        return builder.ToSearchAttributeCollection();
    }
}

