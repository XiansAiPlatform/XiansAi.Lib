using System.Reflection;
using XiansAi.Activity;
using XiansAi.Models;

namespace XiansAi.Flow;

public interface IFlowMetadataService<TClass>
{
    FlowDefinition ExtractFlowInformation(FlowInfo<TClass> flow);
}

public class FlowMetadataService<TClass> : IFlowMetadataService<TClass>
{

    public FlowDefinition ExtractFlowInformation(FlowInfo<TClass> flow)
    {
        var flowAttribute = typeof(TClass).GetCustomAttribute<Temporalio.Workflows.WorkflowAttribute>();
        if (flowAttribute == null)
        {
            throw new InvalidOperationException($"Class {typeof(TClass).Name} must have WorkflowAttribute");
        }

        var workflowRunMethod = typeof(TClass).GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<Temporalio.Workflows.WorkflowRunAttribute>() != null);

        if (workflowRunMethod == null)
        {
            throw new InvalidOperationException($"Class {typeof(TClass).Name} must have a method with WorkflowRunAttribute");
        }

        var parameters = workflowRunMethod.GetParameters();

        var className = typeof(TClass).FullName ?? typeof(TClass).Name;
        var flowName = flowAttribute?.Name ?? typeof(TClass).Name;
        var activities = ExtractActivityInformation(flow);

        return new FlowDefinition
        {
            Parameters = parameters,
            TypeName = flowName,
            ClassName = className,
            Activities = activities
        };
    }

    private ActivityDefinition[] ExtractActivityInformation(FlowInfo<TClass> flow)
    {
        var activities = new List<ActivityDefinition>();
        foreach (var activity in flow.GetActivities())
        {
            var type = activity.Key;
            var dockerImageAttribute = type.GetCustomAttribute<DockerImageAttribute>();
            var instructionsAttribute = type.GetCustomAttribute<InstructionsAttribute>();
            
            if (dockerImageAttribute == null) continue;

            var activityMethods = type.GetMethods()
                .Where(m => m.GetCustomAttribute<Temporalio.Activities.ActivityAttribute>() != null);

            foreach (var method in activityMethods)
            {
                var activityAttribute = method.GetCustomAttribute<Temporalio.Activities.ActivityAttribute>();
                activities.Add(new ActivityDefinition
                {
                    DockerImage = dockerImageAttribute.Name,
                    Instructions = instructionsAttribute?.Instructions ?? [],
                    ActivityName = activityAttribute?.Name ?? method.Name,
                    ClassName = type.FullName ?? type.Name
                });
            }
        }
        return activities.ToArray();
    }

}
