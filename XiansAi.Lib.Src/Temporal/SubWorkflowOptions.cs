using Temporalio.Common;
using Temporalio.Workflows;

namespace Temporal;

public class SubWorkflowOptions : ChildWorkflowOptions
{
    public SubWorkflowOptions(string postfix, string workflowType)
    {
        var newOptions = new NewWorkflowOptions(workflowType);
        var workflowId = AgentContext.WorkflowId + ":" + postfix;
        Id = workflowId;
        TaskQueue = AgentContext.TenantId + ":" + workflowType;
        Memo = newOptions.GetMemo();
        TypedSearchAttributes = newOptions.GetSearchAttributes();
        StaticSummary = $"Sub workflow of `{AgentContext.WorkflowId }` with name `{postfix}`";
        RetryPolicy = new RetryPolicy{
            MaximumAttempts = 5
        };
        ParentClosePolicy = ParentClosePolicy.Abandon;
    }
}