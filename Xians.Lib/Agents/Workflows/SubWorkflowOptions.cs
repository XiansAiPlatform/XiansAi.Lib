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
        string? idPostfix = null, 
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
        var agentName = XiansContext.CurrentAgent?.Name;
        TaskQueue = TenantContext.GetTaskQueueName(workflowType, isSystemScoped, tenantId, agentName);

        // Generate workflow ID
        Id = TenantContext.BuildWorkflowId(workflowType, tenantId, idPostfix);

        // Propagate parent workflow's memo and search attributes
        Memo = BuildMemo(tenantId, workflowType, isSystemScoped);
        TypedSearchAttributes = BuildSearchAttributes(tenantId, workflowType);

        // Set workflow summary for debugging
        StaticSummary = $"Sub-workflow of '{XiansContext.WorkflowId}' with type '{workflowType}'" +
                       (idPostfix != null ? $" and postfix '{idPostfix}'" : "");

        // Default retry policy: single attempt (fail fast)
        RetryPolicy = retryPolicy ?? new RetryPolicy { MaximumAttempts = 1 };

        // Child workflow should be abandoned when parent closes
        // This prevents orphaned workflows when parent is terminated
        ParentClosePolicy = ParentClosePolicy.Abandon;
    }

    /// <summary>
    /// Gets the system-scoped flag from the parent workflow's memo.
    /// </summary>
    private static bool GetSystemScopedFromParent()
    {
        if (!Workflow.InWorkflow)
        {
            throw new InvalidOperationException(
                "Cannot determine system-scoped setting outside of workflow context.");
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
    /// Builds the memo dictionary for the child workflow.
    /// Propagates important metadata from parent workflow.
    /// </summary>
    private static Dictionary<string, object> BuildMemo(string tenantId, string workflowType, bool systemScoped)
    {
        var agentName = workflowType.Contains(':') 
            ? workflowType.Split(':')[0] 
            : workflowType;

        var memo = new Dictionary<string, object>
        {
            { WorkflowConstants.Keys.TenantId, tenantId },
            { WorkflowConstants.Keys.Agent, agentName },
            { WorkflowConstants.Keys.SystemScoped, systemScoped }
        };

        // Try to propagate UserId from parent if available
        if (Workflow.Memo.TryGetValue(WorkflowConstants.Keys.UserId, out var userId))
        {
            var userIdStr = userId.Payload.Data.ToStringUtf8();
            if (!string.IsNullOrWhiteSpace(userIdStr))
            {
                memo[WorkflowConstants.Keys.UserId] = userIdStr;
            }
        }

        return memo;
    }

    /// <summary>
    /// Builds search attributes for the child workflow.
    /// Propagates searchable metadata for workflow discovery and filtering.
    /// </summary>
    private static SearchAttributeCollection BuildSearchAttributes(string tenantId, string workflowType)
    {
        var agentName = workflowType.Contains(':') 
            ? workflowType.Split(':')[0] 
            : workflowType;

        var builder = new SearchAttributeCollection.Builder()
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.TenantId), tenantId)
            .Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.Agent), agentName);

        // Try to propagate UserId from parent if available
        if (Workflow.Memo.TryGetValue(WorkflowConstants.Keys.UserId, out var userId))
        {
            var userIdStr = userId.Payload.Data.ToStringUtf8();
            if (!string.IsNullOrWhiteSpace(userIdStr))
            {
                builder.Set(SearchAttributeKey.CreateKeyword(WorkflowConstants.Keys.UserId), userIdStr);
            }
        }

        return builder.ToSearchAttributeCollection();
    }
}

