using System.Reflection;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using XiansAi.Activity;
using XiansAi.Models;

namespace XiansAi.Flow;

/// <summary>
/// Manages workflow activity registration and metadata for a specific workflow class.
/// </summary>
/// <typeparam name="TClass">The workflow class type</typeparam>
public class FlowInfo<TClass>
{
    private readonly Dictionary<Type, object> _stubProxies = new();
    private readonly List<ActivityBase> _stubs = new();
    private readonly List<(Type @interface, object stub, object proxy)> _objects = new();
    private readonly ILogger<FlowInfo<TClass>> _logger = Globals.LogFactory.CreateLogger<FlowInfo<TClass>>();
    public AgentInfo? AgentInfo { get; private set; }

    public FlowInfo(AgentInfo? agentInfo = null)
    {
        AgentInfo = agentInfo;
    }

    /// <summary>
    /// Registers an activity implementation with its interface.
    /// </summary>
    /// <typeparam name="IActivity">The activity interface type</typeparam>
    /// <param name="activity">The activity implementation instance</param>
    /// <returns>The current FlowInfo instance for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when activity is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when IActivity is not an interface</exception>
    public FlowInfo<TClass> AddActivities<IActivity>(ActivityBase activity) 
        where IActivity : class
    {
        Console.WriteLine($"Adding activities for {activity.GetType().Name}");
        _logger.LogDebug($"Adding activities for {activity.GetType().Name}");
        ArgumentNullException.ThrowIfNull(activity, nameof(activity));

        var interfaceType = typeof(IActivity);
        if (!interfaceType.IsInterface)
        {
            throw new InvalidOperationException($"Type parameter {interfaceType.Name} must be an interface");
        }

        try
        {
            activity.Agent = AgentInfo?.Name ?? GetWorkflowName();
            _stubs.Add(activity);
            
            var activityType = activity.GetType();
            var proxyCreateMethod = typeof(ActivityTrackerProxy<,>)
                .MakeGenericType(interfaceType, activityType)
                .GetMethod("Create") 
                ?? throw new InvalidOperationException("Failed to find Create method on ActivityTrackerProxy");

            var stubProxy = proxyCreateMethod.Invoke(null, new[] { activity })
                ?? throw new InvalidOperationException("Failed to create activity proxy");

            Console.WriteLine($"Activity proxy created: {stubProxy} for interface {interfaceType.Name}");

            _stubProxies[interfaceType] = stubProxy;

            _objects.Add((interfaceType, activity, stubProxy));
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
    public List<ActivityBase> GetStubs()
    {
        return _stubs;
    }

    /// <summary>
    /// Gets the registered activity proxies.
    /// </summary>
    /// <returns>Dictionary of interface types to activity proxies</returns>
    public IReadOnlyDictionary<Type, object> GetStubProxies()
    {
        return _stubProxies;
    }

    public List<(Type @interface, object stub, object proxy)> GetObjects()
    {
        return _objects;
    }


    /// <summary>
    /// Gets the workflow name from the WorkflowAttribute or class name.
    /// </summary>
    /// <returns>The workflow name</returns>
    /// <exception cref="InvalidOperationException">Thrown when WorkflowAttribute is missing</exception>
    public string GetWorkflowName() 
    {
        var workflowClass = typeof(TClass);
        var workflowAttr = workflowClass.GetCustomAttribute<WorkflowAttribute>();
        
        if (workflowAttr == null)
        {
            throw new InvalidOperationException(
                $"Workflow {workflowClass.Name} is missing required WorkflowAttribute");
        }
        _logger.LogDebug($"Workflow name: {workflowAttr.Name ?? workflowClass.Name}");

        return workflowAttr.Name ?? workflowClass.Name;
    }

    public string GetAgentName()
    {
        if (AgentInfo != null)
        {
            return AgentInfo.Name;
        }
        return GetWorkflowName();
    }

    public string[] GetCategories()
    {
        var workflowClass = typeof(TClass);
        var categoriesAttr = workflowClass.GetCustomAttribute<CategoriesAttribute>();
        return categoriesAttr?.Categories ?? [];
    }

    public string[] GetKnowledgeIds()
    {
        var workflowClass = typeof(TClass);
        var constructor = workflowClass.GetConstructors().FirstOrDefault();
        var knowledgeIdsAttr = constructor?.GetCustomAttribute<KnowledgeAttribute>();
        return knowledgeIdsAttr?.Knowledge ?? [];
    }

    /// <summary>
    /// Gets the parameters of the workflow's run method.
    /// </summary>
    /// <returns>List of parameter information for the workflow run method</returns>
    public List<ParameterDefinition> GetParameters()
    {
        var workflowType = typeof(TClass);
        var workflowRunMethod = workflowType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<WorkflowRunAttribute>() != null);

        return workflowRunMethod?.GetParameters().Select(p => new ParameterDefinition {
                Name = p.Name,
                Type = p.ParameterType.Name
            }).ToList() ?? [];
    }
}