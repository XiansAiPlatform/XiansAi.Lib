using System.Reflection;
using Temporalio.Activities;
using Temporalio.Worker;
using Temporalio.Workflows;

public interface IFlowMetadataService<TClass>
{
    FlowInfo ExtractFlowInformation(Flow<TClass> flow);
}

public class FlowMetadataService<TClass> : IFlowMetadataService<TClass>
{

    public FlowMetadataService()
    {

    }


    public FlowInfo ExtractFlowInformation(Flow<TClass> flow)
    {
        var flowAttribute = typeof(TClass).GetCustomAttribute<WorkflowAttribute>();
        if (flowAttribute == null)
        {
            throw new InvalidOperationException($"Class {typeof(TClass).Name} must have WorkflowAttribute");
        }

        var workflowRunMethod = typeof(TClass).GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<WorkflowRunAttribute>() != null);

        if (workflowRunMethod == null)
        {
            throw new InvalidOperationException($"Class {typeof(TClass).Name} must have a method with WorkflowRunAttribute");
        }

        var parameters = workflowRunMethod.GetParameters();

        var className = typeof(TClass).FullName ?? typeof(TClass).Name;
        var flowName = flowAttribute?.Name ?? typeof(TClass).Name;
        var activities = ExtractActivityInformation(flow);

        return new FlowInfo
        {
            Parameters = parameters,
            FlowName = flowName,
            ClassName = className,
            Activities = activities
        };
    }

    private ActivityInfo[] ExtractActivityInformation(Flow<TClass> flow)
    {
        var activities = new List<ActivityInfo>();
        foreach (var activity in flow.GetActivities())
        {
            var type = activity.Key;
            var agentAttribute = type.GetCustomAttribute<AgentAttribute>();
            
            if (agentAttribute == null) continue;

            var activityMethods = type.GetMethods()
                .Where(m => m.GetCustomAttribute<ActivityAttribute>() != null);

            foreach (var method in activityMethods)
            {
                var activityAttribute = method.GetCustomAttribute<ActivityAttribute>();
                activities.Add(new ActivityInfo
                {
                    AgentName = agentAttribute.Name,
                    Instructions = agentAttribute.Instructions.Select(i => new InstructionInfo
                    {
                        Content = FetchInstructionContent(i),
                        Name = i,
                        Version = null
                    }).ToArray(),
                    ActivityName = activityAttribute?.Name ?? method.Name,
                    ClassName = type.FullName ?? type.Name
                });
            }
        }
        return activities.ToArray();
    }

    private string FetchInstructionContent(string instructionKey)
    {
        if (instructionKey == null) {
            throw new InvalidOperationException("Instruction key is not set");
        }
        var instruction = File.ReadAllText(instructionKey);
        return instruction;
    }


}
