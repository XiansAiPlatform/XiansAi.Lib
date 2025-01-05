using XiansAi.Activity;

namespace XiansAi.Flow;

public class FlowInfo<TClass>
{
    private readonly Dictionary<Type, object> _activities = new Dictionary<Type, object>();
    public FlowInfo<TClass> AddActivity<IActivity>(BaseAgent activity) 
        where IActivity : class
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (!typeof(IActivity).IsInterface)
        {
            throw new InvalidOperationException("First type parameter must be an interface");
        }
        
        var activityType = activity.GetType();
        var activityProxy = typeof(ActivityTrackerProxy<,>)
            .MakeGenericType(typeof(IActivity), activityType)
            .GetMethod("Create")!
            .Invoke(null, new[] { activity });

        _activities[typeof(IActivity)] = activityProxy!;
        return this;
    }

    public Dictionary<Type, object> GetActivities()
    {
        return _activities;
    }

}