using System.Reflection;
using Temporalio.Workflows;

namespace XiansAi.Lib.Flow;

public class NameUtil
{
    private readonly AgentContext? _agentContext;
    private NameUtil(AgentContext? agentContext = null)
    {
        _agentContext = agentContext;
    }

    private string GetWorkflowType(Type flowClassType)
    {
        var workflowAttr = flowClassType.GetCustomAttribute<WorkflowAttribute>();
        return workflowAttr?.Name ?? flowClassType.Name;
    }

    // public string GetSingletonWorkflowId(Type flowClassType)
    // {
    //     if (Workflow.InWorkflow)
    //     {
    //         var memoUtil = new MemoUtil(Workflow.Memo);
    //         var workflowId = $"{memoUtil.GetTenantId()}:{memoUtil.GetAgent()}:{GetWorkflowType(flowClassType)}";
    //         return workflowId.Replace(" ", "");
    //     } else if (_agentContext != null) {
    //         var workflowId = $"{_agentContext.TenantId}:{_agentContext.Agent}:{GetWorkflowType(flowClassType)}";
    //         return workflowId.Replace(" ", "");
    //     } else {
    //         throw new InvalidOperationException("You are not in a workflow. Pass `RouteContext` to the constructor of `NameUtil`.");
    //     }
    // }
}