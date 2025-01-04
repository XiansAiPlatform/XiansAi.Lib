using System.Reflection;

public class ActivityInfo
{
    public required string DockerImage { get; set; }
    public required string[] Instructions { get; set; }
    public required string ActivityName { get; set; }
    public required string ClassName {get; set;}
}

public class FlowInfo   
{
    public required string FlowName { get; set; }
    public required ActivityInfo[] Activities { get; set; }
    public required string ClassName { get; set; }
    public required ParameterInfo[] Parameters { get; set; }
    public string? Version { get; set; }
    public string? Source { get; set; }
}

public class Flow<TClass>
{
    private readonly Dictionary<Type, object> _activities = new Dictionary<Type, object>();
    public Flow<TClass> AddActivity<IActivity, TActivity>(TActivity activity) 
        where TActivity : BaseAgent, IActivity
        where IActivity : class
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (!typeof(IActivity).IsInterface)
        {
            throw new InvalidOperationException("First type parameter must be an interface");
        }
        var activityProxy = ActivityTrackerProxy<IActivity, TActivity>.Create(activity);

        _activities[typeof(IActivity)] = activityProxy!;
        return this;
    }

    public Dictionary<Type, object> GetActivities()
    {
        return _activities;
    }

}