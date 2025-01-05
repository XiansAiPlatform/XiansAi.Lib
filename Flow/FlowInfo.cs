using System.Reflection;
using Temporalio.Workflows;
using XiansAi.Activity;

namespace XiansAi.Flow;

public class FlowInfo<TClass>
{
    private readonly Dictionary<Type, object> _proxyActivities = new Dictionary<Type, object>();
    private readonly Dictionary<Type, object> _activities = new Dictionary<Type, object>();
    public FlowInfo<TClass> AddActivity<IActivity>(BaseAgent activity) 
        where IActivity : class
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (!typeof(IActivity).IsInterface)
        {
            throw new InvalidOperationException("First type parameter must be an interface");
        }

        _activities[typeof(IActivity)] = activity;
        
        var activityType = activity.GetType();
        var activityProxy = typeof(ActivityTrackerProxy<,>)
            .MakeGenericType(typeof(IActivity), activityType)
            .GetMethod("Create")!
            .Invoke(null, new[] { activity });

        _proxyActivities[typeof(IActivity)] = activityProxy!;
        return this;
    }

    public Dictionary<Type, object> GetActivities()
    {
        return _activities;
    }

    public Dictionary<Type, object> GetProxyActivities()
    {
        return _proxyActivities;
    }


    public string GetWorkflowName() 
    {
        var workflowAttr = typeof(TClass).GetCustomAttribute<WorkflowAttribute>();
        if (workflowAttr == null)
        {
            throw new InvalidOperationException($"Workflow {typeof(TClass).Name} is missing WorkflowAttribute");
        }
        return workflowAttr.Name ?? typeof(TClass).Name;
    }


    public List<ParameterInfo> GetParameters()
    {
        var workflowRunMethod = typeof(TClass).GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<WorkflowRunAttribute>() != null);
        return workflowRunMethod?.GetParameters().ToList() ?? new List<ParameterInfo>();
    }
}