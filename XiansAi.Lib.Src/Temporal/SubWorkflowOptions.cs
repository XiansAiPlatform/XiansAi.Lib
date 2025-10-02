using Temporalio.Common;
using Temporalio.Workflows;

namespace Temporal;

public class SubWorkflowOptions : ChildWorkflowOptions
{
    public SubWorkflowOptions(string workflowType, string? idPostfix = null, RetryPolicy? retryPolicy = null)
    {
        var newOptions = new NewWorkflowOptions(workflowType, idPostfix);
        var systemScoped = AgentContext.SystemScoped;
        if (systemScoped) {
            TaskQueue = workflowType;
        } else {
            TaskQueue = AgentContext.TenantId + ":" + workflowType;
        }
        Id = AgentContext.TenantId + ":" + workflowType + (idPostfix != null ? ":" + idPostfix : "");;
        Memo = newOptions.GetMemo();
        TypedSearchAttributes = newOptions.GetSearchAttributes();
        StaticSummary = $"Sub workflow of `{AgentContext.WorkflowId }` with name `{idPostfix}`";
        RetryPolicy = retryPolicy ?? new RetryPolicy{
            MaximumAttempts = 1
        };
        ParentClosePolicy = ParentClosePolicy.Abandon;
    }
}