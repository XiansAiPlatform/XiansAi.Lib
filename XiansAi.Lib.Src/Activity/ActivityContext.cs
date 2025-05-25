using Temporalio.Activities;
using XiansAi.Models;

namespace XiansAi.Activity;
public static class ActivityContext
{
    internal static bool IsInWorkflow() 
    {
        return ActivityExecutionContext.HasCurrent;
    }

    internal static ActivityHistory GetCurrentActivity()
    {
        var context = ActivityExecutionContext.Current ?? throw new InvalidOperationException("ActivityExecutionContext is null");
        return new ActivityHistory
        {
            Agent = AgentContext.AgentName ?? throw new InvalidOperationException("Agent can not be null, set the agent first"),
            ActivityId = context.Info.ActivityId ?? throw new InvalidOperationException("ActivityId is null"),
            ActivityName = context.Info.ActivityType ?? throw new InvalidOperationException("ActivityType is null"),
            StartedTime = context.Info.StartedTime,
            Attempt = context.Info.Attempt,
            WorkflowNamespace = context.Info.WorkflowNamespace ?? throw new InvalidOperationException("WorkflowNamespace is null"),
            WorkflowId = context.Info.WorkflowId ?? throw new InvalidOperationException("WorkflowId is null"),
            WorkflowType = context.Info.WorkflowType ?? throw new InvalidOperationException("WorkflowType is null"),
            TaskQueue = context.Info.TaskQueue ?? throw new InvalidOperationException("TaskQueue is null"),
            WorkflowRunId = context.Info.WorkflowRunId ?? throw new InvalidOperationException("WorkflowRunId is null")
        };
    }

}
