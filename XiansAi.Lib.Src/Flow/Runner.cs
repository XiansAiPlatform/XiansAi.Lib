using System.Reflection;
using System.Collections.Concurrent;
using Server;
using Temporal;
using Temporalio.Workflows;
using XiansAi.Activity;
using XiansAi.Logging;
using XiansAi.Models;

namespace XiansAi.Flow;

/// <summary>
/// Non-generic static helper class for accessing runner registry.
/// </summary>
public static class RunnerRegistry
{
    private static readonly ConcurrentDictionary<Type, IRunner> _globalRunnerRegistry = new();

    /// <summary>
    /// Gets a runner instance for the specified workflow class type.
    /// </summary>
    /// <param name="workflowType">The workflow class type</param>
    /// <returns>The runner instance for the specified type, or null if not found</returns>
    public static IRunner? GetRunner(Type workflowType)
    {
        return _globalRunnerRegistry.TryGetValue(workflowType, out var runner) ? runner : null;
    }

    /// <summary>
    /// Gets all registered runner instances.
    /// </summary>
    /// <returns>A dictionary of workflow types to runner instances</returns>
    public static IReadOnlyDictionary<Type, IRunner> GetAllRunners()
    {
        return _globalRunnerRegistry;
    }

    /// <summary>
    /// Removes a runner instance from the registry.
    /// </summary>
    /// <param name="workflowType">The workflow class type</param>
    /// <returns>True if the runner was removed, false if it wasn't found</returns>
    public static bool RemoveRunner(Type workflowType)
    {
        return _globalRunnerRegistry.TryRemove(workflowType, out _);
    }

    /// <summary>
    /// Removes a runner instance from the registry.
    /// </summary>
    /// <typeparam name="T">The workflow class type</typeparam>
    /// <returns>True if the runner was removed, false if it wasn't found</returns>
    public static bool RemoveRunner<T>() where T : class
    {
        return _globalRunnerRegistry.TryRemove(typeof(T), out _);
    }

    /// <summary>
    /// Clears all runner instances from the registry.
    /// </summary>
    public static void ClearRegistry()
    {
        _globalRunnerRegistry.Clear();
    }

    /// <summary>
    /// Internal method used by Runner<T> instances to register themselves.
    /// </summary>
    internal static void RegisterRunner(Type workflowType, IRunner runner)
    {
        _globalRunnerRegistry.AddOrUpdate(workflowType, runner, (key, oldValue) => runner);
    }
}

public interface IRunner
{
    List<Type> Capabilities { get; }
    List<IKernelModifier> KernelModifiers { get; }
    IChatInterceptor? ChatInterceptor { get; set; }
}

/// <summary>
/// Manages workflow activity registration and metadata for a specific workflow class.
/// </summary>
/// <typeparam name="TClass">The workflow class type</typeparam>
public class Runner<TClass> : IRunner where TClass : class
{
    private readonly Dictionary<Type, object> _objectActivities = new();

    private readonly Dictionary<Type, object> _activityProxies = new();
    private readonly List<Type> _capabilities = new();
    public Type? DataProcessorType { get; set; }
    public bool ProcessDataInWorkflow { get; set; } = false;
    public bool RunAtStart { get; set; } = false;
    public Type? ScheduleProcessorType { get; set; }
    public bool ProcessScheduleInWorkflow { get; set; } = false;
    public bool StartAutomatically { get; set; } = false;
    public int NumberOfWorkers { get; set; } = 1;

#pragma warning disable CS0618 // Type or member is obsolete
    public AgentInfo AgentInfo { get; private set; }
    private readonly Logger<Runner<TClass>> _logger = Logger<Runner<TClass>>.For();

    public Runner(AgentInfo agentInfo, int workers)
    {
        AgentInfo = agentInfo;
        NumberOfWorkers = workers;
        // Set the agent name to Agent Context
        //AgentContext.AgentName = agentInfo.Name;
        // validate the runner
        Validate();
        // Register this runner instance
        RegisterRunner();
    }
#pragma warning restore CS0618 // Type or member is obsolete

    private void Validate()
    {
        if (string.IsNullOrEmpty(AgentInfo?.Name))
        {
            throw new InvalidOperationException("AgentInfo.Name is required");
        }
        // Workflow name should start with AgentName:
        if (!WorkflowName.StartsWith(AgentName + ":"))
        {
            throw new InvalidOperationException($"Invalid workflow name `{WorkflowName}`. WorkflowName must start with `agent_name:`, i.e. `{AgentName}:`");
        }
    }

    public async Task RunAsync(RunnerOptions? options = null)
    {
        try
        {
            var workerService = new WorkerService(options);
            await workerService.RunFlowAsync(this);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Application shutdown requested. Shutting down gracefully...");
        }
    }

    public Runner<TClass> AddBotCapabilities(Type capabilityClass)
    {
        _capabilities.Add(capabilityClass);
        return this;
    }

    public Runner<TClass> SetScheduleProcessor<TProcessor>()
    {
        ScheduleProcessorType = typeof(TProcessor);
        return this;
    }

    public Runner<TClass> AddBotCapabilities<TCapability>()
    {
        _capabilities.Add(typeof(TCapability));
        return this;
    }

    public Runner<TClass> AddFlowActivities<TActivity>(object activity)
    {
        _objectActivities.Add(typeof(TActivity), activity);
        return this;
    }

    public Runner<TClass> AddFlowActivities<IActivity, TActivity>()
        where IActivity : class
        where TActivity : class
    {
        return AddFlowActivities<IActivity, TActivity>(new object[] { });
    }

    public Runner<TClass> AddFlowActivities<IActivity, TActivity>(params object[] args)
        where IActivity : class
        where TActivity : class
    {

        var activity = TypeActivator.CreateWithOptionalArgs(typeof(TActivity), args);

        if (activity == null)
        {
            throw new InvalidOperationException($"Failed to create activity instance for {typeof(TActivity).Name}");
        }

        var interfaceType = typeof(IActivity);

        try
        {
            // Use the ActivityProxy's CreateProxyFor method instead of direct reflection
            var stubProxy = ActivityProxyFactory.CreateProxyFor(interfaceType, activity);
            _activityProxies[interfaceType] = stubProxy;
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
    internal IReadOnlyDictionary<Type, object> ActivityProxies
    {
        get
        {
            return _activityProxies;
        }
    }

    internal List<Type> ActivityInterfaces
    {
        get
        {
            return _activityProxies.Keys.ToList();
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

            if (string.IsNullOrEmpty(workflowAttr.Name))
            {
                throw new InvalidOperationException($"Workflow {workflowClass.Name} is missing required WorkflowAttribute.Name");
            }

            return workflowAttr.Name;
        }
    }

    public IChatInterceptor? ChatInterceptor { get; set; }
    public List<IKernelModifier> KernelModifiers { get; set; } = new();

    public List<Type> Capabilities
    {
        get
        {
            return _capabilities;
        }
    }

    public string AgentName
    {
        get
        {
            return AgentInfo.Name;
        }
        
    }

    internal Dictionary<Type, object> ObjectActivities
    {
        get
        {
            return _objectActivities;
        }
    }

    /// <summary>
    /// Gets the parameters of the workflow's run method.
    /// </summary>
    /// <returns>List of parameter information for the workflow run method</returns>
    internal List<ParameterDefinition> WorkflowParameters
    {
        get
        {
            var workflowType = typeof(TClass);
            var workflowRunMethod = workflowType.GetMethods()
            .FirstOrDefault(m => m.GetCustomAttribute<WorkflowRunAttribute>() != null);

            return workflowRunMethod?.GetParameters().Select(p => new ParameterDefinition
            {
                Name = p.Name,
                Type = p.ParameterType.Name
            }).ToList() ?? [];
        }
    }

    /// <summary>
    /// Registers this runner instance in the global registry.
    /// </summary>
    private void RegisterRunner()
    {
        RunnerRegistry.RegisterRunner(typeof(TClass), this);
    }


}


public class RunnerOptions
{
    public string? QueuePrefix { get; set; }
}

public class FlowInfo
{
}

