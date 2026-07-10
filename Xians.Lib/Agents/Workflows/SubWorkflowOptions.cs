using Temporalio.Api.Enums.V1;
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
    /// The system-scoped setting is resolved from the target agent when it is registered in this
    /// process; otherwise it is inherited from the parent workflow.
    /// </summary>
    /// <param name="workflowType">The workflow type (format: "AgentName:WorkflowName").</param>
    /// <param name="uniqueKeys">Optional unique keys for workflow ID uniqueness.</param>
    /// <param name="retryPolicy">Optional retry policy. Defaults to MaximumAttempts=1.</param>
    /// <param name="activationName">Optional target activation name (idPostfix) for the child workflow.
    /// When null, the parent's idPostfix is inherited only when the child belongs to the same agent.</param>
    public SubWorkflowOptions(
        string workflowType, 
        string[] uniqueKeys, 
        RetryPolicy? retryPolicy = null,
        string? activationName = null)
    {
        if (string.IsNullOrWhiteSpace(workflowType))
        {
            throw new ArgumentException(WorkflowConstants.ErrorMessages.WorkflowTypeNullOrEmpty, nameof(workflowType));
        }

        // Extract agent name (the target agent, which may differ from the calling agent)
        var agentName = workflowType.Contains(':') ? workflowType.Split(':')[0] : workflowType;

        // Resolve system-scoped from the target agent when registered in this process.
        // The parent's setting is only a fallback: using it for a differently-scoped target
        // agent would compute a task queue no worker listens on.
        var isSystemScoped = XiansContext.TryGetAgent(agentName, out var targetAgent) && targetAgent != null
            ? targetAgent.SystemScoped
            : GetSystemScopedFromParent();

        // Get tenant ID from parent workflow
        var tenantId = XiansContext.TenantId;

        // Generate task queue using centralized utility
        // For platform workflows starting with "Platform:", pass the agent name to replace "Platform"
        TaskQueue = TenantContext.GetTaskQueueName(workflowType, isSystemScoped, tenantId);

        // Generate workflow ID using shared method (uniqueKeys already carry the resolved child idPostfix)
        Id = SubWorkflowService.BuildSubWorkflowId(agentName, workflowType, tenantId, uniqueKeys);

        // Resolve the activation context (idPostfix) the child should carry
        var childIdPostfix = SubWorkflowService.ResolveChildIdPostfix(workflowType, activationName);

        // Inherit all parent workflow's memo and search attributes using shared methods,
        // overriding child-specific metadata (tenantId, agent, idPostfix)
        var searchAttributes = SubWorkflowService.BuildInheritedSearchAttributes(tenantId, agentName, childIdPostfix);
        Memo = SubWorkflowService.BuildInheritedMemo(tenantId, agentName, isSystemScoped, childIdPostfix, searchAttributes);
        TypedSearchAttributes = searchAttributes;

        // Set workflow summary for debugging
        StaticSummary = $"Sub-workflow of '{XiansContext.WorkflowId}' with type '{workflowType}'" +
                       $" and unique keys '{string.Join(", ", uniqueKeys)}'";

        // Default retry policy: single attempt (fail fast)
        RetryPolicy = retryPolicy ?? new RetryPolicy { MaximumAttempts = 1 };

        // Child workflow should be abandoned when parent closes
        // This prevents orphaned workflows when parent is terminated
        ParentClosePolicy = Temporalio.Workflows.ParentClosePolicy.Abandon;

        IdReusePolicy = WorkflowIdReusePolicy.AllowDuplicate;
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
