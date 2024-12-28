using System.Reflection;
using Temporalio.Activities;
using Temporalio.Workflows;


public class ActivityInfo
{
    public required string AgentName { get; set; }
    public required string[] Instructions { get; set; }
    public required string ActivityName { get; set; }
    public required string ClassName {get; set;}
    
}

public class FlowInfo   
{
    public required string FlowName { get; set; }
    public required ActivityInfo[] Activities { get; set; }
    public required string ClassName { get; set; }
    
}
public class Flow<TClass>
{
    private readonly Dictionary<Type, object> _activities = new Dictionary<Type, object>();
    public Flow<TClass> AddActivity<TActivity>(TActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        _activities[typeof(TActivity)] = activity;
        return this;
    }

    public Dictionary<Type, object> GetActivities()
    {
        return _activities;
    }

    public FlowInfo ExtractFlowInformation()
    {
        var flowAttribute = typeof(TClass).GetCustomAttribute<WorkflowAttribute>();
        var flowName = flowAttribute?.Name ?? typeof(TClass).Name;
        var className = typeof(TClass).FullName ?? typeof(TClass).Name;

        var activities = new List<ActivityInfo>();
        foreach (var activity in _activities)
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
                    Instructions = agentAttribute.Instructions,
                    ActivityName = activityAttribute?.Name ?? type.Name,
                    ClassName = type.FullName ?? type.Name
                });
            }
        }
        return new FlowInfo
        {
            FlowName = flowName,
            ClassName = className,
            Activities = activities.ToArray()
        };
    }
}