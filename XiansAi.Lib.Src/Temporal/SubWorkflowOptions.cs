using Temporalio.Common;
using Temporalio.Workflows;

namespace Temporal;

public class SubWorkflowOptions : ChildWorkflowOptions
{
    public SubWorkflowOptions(string workflowType, string? idPostfix = null)
    {
        var newOptions = new NewWorkflowOptions(workflowType, idPostfix);
        var workflowId = AgentContext.WorkflowId + (idPostfix != null ? ":" + idPostfix : "");
        Id = workflowId;
        TaskQueue = AgentContext.TenantId + ":" + workflowType;
        Memo = newOptions.GetMemo();
        TypedSearchAttributes = newOptions.GetSearchAttributes();
        StaticSummary = $"Sub workflow of `{AgentContext.WorkflowId }` with name `{idPostfix}`";
        RetryPolicy = new RetryPolicy{
            MaximumAttempts = 5
        };
        ParentClosePolicy = ParentClosePolicy.Abandon;
    }
}