using Xians.Lib.Common.MultiTenancy;
namespace Xians.Lib.Common.Models;

/// <summary>
/// Represents the parsed components of a workflow identifier.
/// </summary>
public class WorkflowIdentifier
{
    /// <summary>
    /// Gets the tenant ID extracted from the workflow ID.
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// Gets the workflow type extracted from the workflow ID.
    /// </summary>
    public string WorkflowType { get; }

    /// <summary>
    /// Gets the complete workflow ID.
    /// </summary>
    public string WorkflowId { get; }

    internal WorkflowIdentifier(string workflowId)
    {
        WorkflowId = workflowId;
        TenantId = TenantContext.ExtractTenantId(workflowId);
        WorkflowType = TenantContext.ExtractWorkflowType(workflowId);
    }

    public override string ToString()
    {
        return $"WorkflowId='{WorkflowId}', TenantId='{TenantId}', WorkflowType='{WorkflowType}'";
    }
}


