using System.Reflection;
using Temporalio.Workflows;
using XiansAi.Activity;

namespace XiansAi.Flow;

/// <summary>
/// Manages workflow activity registration and metadata for a specific workflow class.
/// </summary>
/// <typeparam name="TClass">The workflow class type</typeparam>
public class FlowInfo<TClass>
{
    private readonly Dictionary<Type, object> _proxyActivities = new();
    private readonly Dictionary<Type, object> _activities = new();

    /// <summary>
    /// Registers an activity implementation with its interface.
    /// </summary>
    /// <typeparam name="IActivity">The activity interface type</typeparam>
    /// <param name="activity">The activity implementation instance</param>
    /// <returns>The current FlowInfo instance for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when activity is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when IActivity is not an interface</exception>
    public FlowInfo<TClass> AddActivities<IActivity>(BaseStub activity) 
        where IActivity : class
    {
        ArgumentNullException.ThrowIfNull(activity, nameof(activity));

        var interfaceType = typeof(IActivity);
        if (!interfaceType.IsInterface)
        {
            throw new InvalidOperationException($"Type parameter {interfaceType.Name} must be an interface");
        }

        try
        {
            _activities[interfaceType] = activity;
            
            var activityType = activity.GetType();
            var proxyMethod = typeof(ActivityTrackerProxy<,>)
                .MakeGenericType(interfaceType, activityType)
                .GetMethod("Create") 
                ?? throw new InvalidOperationException("Failed to find Create method on ActivityTrackerProxy");

            var activityProxy = proxyMethod.Invoke(null, new[] { activity })
                ?? throw new InvalidOperationException("Failed to create activity proxy");

            _proxyActivities[interfaceType] = activityProxy;
            return this;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to add activity for interface {interfaceType.Name}", ex);
        }
    }

    /// <summary>
    /// Gets the registered activity implementations.
    /// </summary>
    /// <returns>Dictionary of interface types to activity implementations</returns>
    public IReadOnlyDictionary<Type, object> GetActivities()
    {
        return _activities;
    }

    /// <summary>
    /// Gets the registered activity proxies.
    /// </summary>
    /// <returns>Dictionary of interface types to activity proxies</returns>
    public IReadOnlyDictionary<Type, object> GetProxyActivities()
    {
        return _proxyActivities;
    }

    /// <summary>
    /// Gets the workflow name from the WorkflowAttribute or class name.
    /// </summary>
    /// <returns>The workflow name</returns>
    /// <exception cref="InvalidOperationException">Thrown when WorkflowAttribute is missing</exception>
    public string GetWorkflowName() 
    {
        var workflowType = typeof(TClass);
        var workflowAttr = workflowType.GetCustomAttribute<WorkflowAttribute>();
        
        if (workflowAttr == null)
        {
            throw new InvalidOperationException(
                $"Workflow {workflowType.Name} is missing required WorkflowAttribute");
        }

        return workflowAttr.Name ?? workflowType.Name;
    }

    /// <summary>
    /// Gets the parameters of the workflow's run method.
    /// </summary>
    /// <returns>List of parameter information for the workflow run method</returns>
    public IReadOnlyList<ParameterInfo> GetParameters()
    {
        var workflowType = typeof(TClass);
        var workflowRunMethod = workflowType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<WorkflowRunAttribute>() != null);

        return workflowRunMethod?.GetParameters() ?? Array.Empty<ParameterInfo>();
    }
}