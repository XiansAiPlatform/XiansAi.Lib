using System.Reflection;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using XiansAi.Activity;
using XiansAi.Knowledge;
using XiansAi.Models;

namespace XiansAi.Flow;

/// <summary>
/// Manages workflow activity registration and metadata for a specific workflow class.
/// </summary>
/// <typeparam name="TClass">The workflow class type</typeparam>
public class Runner<TClass> where TClass : class
{
    private readonly Dictionary<Type, object> _stubProxies = new();
    private readonly List<ActivityBase> _stubs = new();

    private readonly List<Type> _capabilities = new();
    private readonly List<(Type @interface, object stub, object proxy)> _objects = new();
    private readonly ILogger<Runner<TClass>> _logger = Globals.LogFactory.CreateLogger<Runner<TClass>>();
    public AgentInfo AgentInfo { get; private set; }

    public Runner(AgentInfo agentInfo)
    {
        AgentInfo = agentInfo;
    }

    public async Task RunAsync(FlowRunnerOptions? options = null)
    {
        var runner = new FlowRunnerService(options);
        await runner.RunFlowAsync(this);
    }

    public Runner<TClass> AddBotCapabilities(Type capabilityClass) {
        _capabilities.Add(capabilityClass);
        return this;
    }


    public Runner<TClass> AddBotCapabilities<TCapability>() {
        _capabilities.Add(typeof(TCapability));
        return this;
    }

    public Runner<TClass> AddFlowActivities<IActivity, TActivity>() 
        where IActivity : class
        where TActivity : ActivityBase
    {
        return AddFlowActivities<IActivity, TActivity>(new object[] { });
    }

    public Runner<TClass> AddFlowActivities<IActivity, TActivity>(params object[] args) 
        where IActivity : class
        where TActivity : ActivityBase
    {
        string agentName = AgentName;
        
        var activity = Activator.CreateInstance(typeof(TActivity), args);

        if (activity == null)
        {
            throw new InvalidOperationException($"Failed to create activity instance for {typeof(TActivity).Name}");
        }

        var activityBase = (TActivity)activity as ActivityBase;
        activityBase.Agent = agentName;

        return AddFlowActivities<IActivity>(activityBase);
    }
    private Runner<TClass> AddFlowActivities<IActivity>(ActivityBase activity) 
        where IActivity : class
    {
        _logger.LogDebug($"Adding activities for {activity.GetType().Name}");
        ArgumentNullException.ThrowIfNull(activity, nameof(activity));

        var interfaceType = typeof(IActivity);
        if (!interfaceType.IsInterface)
        {
            throw new InvalidOperationException($"Type parameter {interfaceType.Name} must be an interface");
        }

        try
        {
            _stubs.Add(activity);
            
            var activityType = activity.GetType();
            var proxyCreateMethod = typeof(ActivityTrackerProxy<,>)
                .MakeGenericType(interfaceType, activityType)
                .GetMethod("Create") 
                ?? throw new InvalidOperationException("Failed to find Create method on ActivityTrackerProxy");

            var stubProxy = proxyCreateMethod.Invoke(null, new[] { activity })
                ?? throw new InvalidOperationException("Failed to create activity proxy");

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
    /// Gets the registered activity proxies.
    /// </summary>
    /// <returns>Dictionary of interface types to activity proxies</returns>
    internal IReadOnlyDictionary<Type, object> StubProxies
    {
        get
        {
            return _stubProxies;
        }
    }

    internal List<(Type @interface, object stub, object proxy)> ActivityObjects
    {
        get
        {
            return _objects;
        }
    }


    /// <summary>
    /// Gets the workflow name from the WorkflowAttribute or class name.
    /// </summary>
    /// <returns>The workflow name</returns>
    /// <exception cref="InvalidOperationException">Thrown when WorkflowAttribute is missing</exception>
    public string WorkflowName
    {
        get
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
    }

    public List<Type> Capabilities
    {
        get
        {
            return _capabilities;
        }
    }

    internal string AgentName
    {
        get
        {
            if (AgentInfo?.Name != null)
            {
                return AgentInfo.Name;
            }
            return WorkflowName;
        }
    }

    /// <summary>
    /// Gets the parameters of the workflow's run method.
    /// </summary>
    /// <returns>List of parameter information for the workflow run method</returns>
    internal List<ParameterDefinition> Parameters
    {
        get
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
}