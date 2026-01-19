using Temporalio.Common;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Common;
using Xians.Lib.Common.MultiTenancy;

namespace Xians.Lib.Agents.Workflows;

/// <summary>
/// Options for starting child workflows within a parent workflow.
/// Handles task queue configuration, workflow ID generation, and metadata propagation.
/// </summary>
public class SubWorkflowOptions : ChildWorkflowOptions
{
    /// <summary>
    /// Creates options for a child workflow.
    /// Child workflows always inherit the system-scoped setting from their parent workflow.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="idPostfix">Optional postfix for workflow ID uniqueness.</param>
    /// <param name="retryPolicy">Optional retry policy. Defaults to MaximumAttempts=1.</param>
    public SubWorkflowOptions(
        string workflowType, 
        string? uniqueKey = null, 
        RetryPolicy? retryPolicy = null)
    {
        if (string.IsNullOrWhiteSpace(workflowType))
        {
            throw new ArgumentException(WorkflowConstants.ErrorMessages.WorkflowTypeNullOrEmpty, nameof(workflowType));
        }

        // Always inherit system-scoped setting from parent workflow
        // This is an agent-level property and should not be overridden
        var isSystemScoped = GetSystemScopedFromParent();

        // Get tenant ID from parent workflow
        var tenantId = XiansContext.TenantId;

        // Generate task queue using centralized utility
        // For platform workflows starting with "Platform:", pass the agent name to replace "Platform"
        TaskQueue = TenantContext.GetTaskQueueName(workflowType, isSystemScoped, tenantId);

        // Extract agent name for workflow ID construction
        var agentName = workflowType.Contains(':') ? workflowType.Split(':')[0] : workflowType;

        // Generate workflow ID using shared method (includes parent idPostfix + optional uniqueKey)
        Id = SubWorkflowService.BuildSubWorkflowId(agentName, workflowType, tenantId, uniqueKey);

        // Inherit all parent workflow's memo and search attributes using shared methods
        // This ensures complete metadata propagation from parent to child
        Memo = SubWorkflowService.BuildInheritedMemo(tenantId, agentName, isSystemScoped);
        TypedSearchAttributes = SubWorkflowService.BuildInheritedSearchAttributes(tenantId, agentName);

        // Set workflow summary for debugging
        StaticSummary = $"Sub-workflow of '{XiansContext.WorkflowId}' with type '{workflowType}'" +
                       (uniqueKey != null ? $" and unique key '{uniqueKey}'" : "");

        // Default retry policy: single attempt (fail fast)
        RetryPolicy = retryPolicy ?? new RetryPolicy { MaximumAttempts = 1 };

        // Child workflow should be abandoned when parent closes
        // This prevents orphaned workflows when parent is terminated
        ParentClosePolicy = ParentClosePolicy.Abandon;
    }

    /// <summary>
    /// Gets the system-scoped flag from the parent workflow's memo.
    /// If not in workflow context, defaults to false (tenant-scoped).
    /// </summary>
    private static bool GetSystemScopedFromParent()
    {
        if (!Workflow.InWorkflow)
        {
            // Not in workflow context - default to false (tenant-scoped)
            // This is a safe default as most workflows are tenant-scoped
            return false;
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

}
