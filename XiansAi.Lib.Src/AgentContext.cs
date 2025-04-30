using System.Reflection;
using Temporal;
using Temporalio.Activities;
using Temporalio.Workflows;

public class AgentContext {

    private static AgentContext? _explicitInstance;

    public static bool MockedInstance { get; set; }

    public AgentContext() {

    }

    public static AgentContext Instance { 
        get {
            if (MockedInstance) {
                return _explicitInstance ?? throw new InvalidOperationException("AgentContext not set explicitly and not mocked");
            }
            if (Workflow.InWorkflow) {
                var memoUtil = new MemoUtil(Workflow.Memo);
                return new AgentContext {
                    Agent = memoUtil.GetAgent(),
                    WorkflowId = Workflow.Info.WorkflowId,
                    WorkflowType = Workflow.Info.WorkflowType,
                    TenantId = memoUtil.GetTenantId(),
                    QueueName = Workflow.Info.TaskQueue,
                    Assignment = memoUtil.GetAssignment(),
                    UserId = memoUtil.GetUserId()
                };
            } else if (ActivityExecutionContext.HasCurrent) {
                // if someone has not set the instance (for performance reasons), load it from the temporal server
                if (_explicitInstance == null) {
                    // load it from the temporal server
                    return LoadFromTemporalServer();
                }
                return _explicitInstance;
            } else {
                throw new InvalidOperationException("Executing neither in workflow nor activity. Agent context not available.");
            }
        }
    }

    private static AgentContext LoadFromTemporalServer() {
        var client = TemporalClientService.Instance.GetClientAsync();
        var workflowId = ActivityExecutionContext.Current.Info.WorkflowId;

        var workflowInfo = client.ListWorkflowsAsync($"WorkflowId = '{workflowId}'").GetAsyncEnumerator().Current;
        if (workflowInfo == null) {
            throw new InvalidOperationException("Workflow not found.");
        }
        var memoUtil = new MemoUtil(workflowInfo.Memo);
        return new AgentContext {
            Agent = memoUtil.GetAgent(),
            WorkflowId = workflowId,
            WorkflowType = workflowInfo.WorkflowType,
            TenantId = memoUtil.GetTenantId(),
            QueueName = workflowInfo.TaskQueue,
            Assignment = memoUtil.GetAssignment(),
            UserId = memoUtil.GetUserId()
        };
    }

    public static void ClearExplicitInstance() {
        _explicitInstance = null;
    }

    public static void SetExplicitInstance(AgentContext agentContext) {
        _explicitInstance = agentContext;
    }

    public string GetSingletonWorkflowIdFor(Type flowClassType)
    {
        var workflowId = $"{TenantId}:{Agent}:{GetWorkflowTypeFor(flowClassType)}";
        return workflowId.Replace(" ", "");
    }

    public string GetWorkflowTypeFor(Type flowClassType)
    {
        var workflowAttr = flowClassType.GetCustomAttribute<WorkflowAttribute>();
        return workflowAttr?.Name ?? flowClassType.Name;
    }


    public required string Agent { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string TenantId { get; set; }
    public  string? QueueName { get; set; }
    public  string? Assignment { get; set; }
    public required string UserId { get; set; }
    
}