using System.Reflection;
using Temporalio.Activities;
using Temporalio.Workflows;

public class AgentContext
{

    private static string? _agent { get; set; }
    private static string? _workflowId { get; set; }
    private static string? _workflowType { get; set; }
    private static string? _workflowRunId { get; set; }

    public static string GetSingletonWorkflowIdFor(Type flowClassType)
    {
        var workflowId = $"{Agent}:{GetWorkflowTypeFor(flowClassType)}";
        return workflowId.Replace(" ", "");
    }

    public static string GetWorkflowTypeFor(Type flowClassType)
    {
        var workflowAttr = flowClassType.GetCustomAttribute<WorkflowAttribute>();
        return workflowAttr?.Name ?? flowClassType.Name;
    }


    public static string Agent
    {
        get
        {
            if (Workflow.InWorkflow) 
            {
                var memo = new MemoUtil(Workflow.Memo);
                var agent = memo.GetAgent();
                if (agent == null)
                {
                    throw new InvalidOperationException("Agent is not set, workflow Memo is missing agent info");
                }
                return agent;
            }
            else {
                if (_agent == null)
                {
                    throw new InvalidOperationException("Agent is not set, create a agent runner first");
                }
                return _agent;
            }
        }
        set
        {
            if (_agent != null && _agent != value)
            {
                throw new InvalidOperationException("Agent is already set, cannot change it");
            }
            _agent = value;

        }
    }
    public static string WorkflowId { 
        get 
        {
            if (Workflow.InWorkflow)
            {
                return Workflow.Info.WorkflowId;
            }
            else if (ActivityExecutionContext.HasCurrent)
            {
                return ActivityExecutionContext.Current.Info.WorkflowId;
            }
            else if (_workflowId != null)
            {
                return _workflowId;
            }
            else
            {
                throw new InvalidOperationException("Not in workflow or activity");
            }
        }
        set
        {
            _workflowId = value;
        }
    }
    public static string WorkflowType { 
        get 
        {
            if (Workflow.InWorkflow)
            {
                return Workflow.Info.WorkflowType;
            }
            else if (ActivityExecutionContext.HasCurrent)
            {
                return ActivityExecutionContext.Current.Info.WorkflowType;
            }
            else if (_workflowType != null)
            {
                return _workflowType;
            }
            else
            {
                throw new InvalidOperationException("Not in workflow or activity");
            }
        }
        set
        {
            _workflowType = value;
        }
    }

    public static string WorkflowRunId { 
        get 
        {
            if (Workflow.InWorkflow)
            {
                return Workflow.Info.RunId;
            }
            else if (ActivityExecutionContext.HasCurrent)
            {
                return ActivityExecutionContext.Current.Info.WorkflowRunId;
            }
            else if (_workflowRunId != null)
            {
                return _workflowRunId;
            }
            else
            {
                throw new InvalidOperationException("Not in workflow or activity");
            }
        }
        set
        {
            _workflowRunId = value;
        }
    }
}