using System.Reflection;

public class InstructionInfo
{
    public required string Content { get; set; }
    public required string Name { get; set; }
    public Type? Type { get; set; }
    public string? Version { get; set; }
}

public class ActivityInfo
{
    public required string AgentName { get; set; }
    public required InstructionInfo[] Instructions { get; set; }
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

}