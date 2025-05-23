using System.Reflection;
using Temporalio.Workflows;
using XiansAi.Activity;
using XiansAi.Logging;
using XiansAi.Models;

namespace XiansAi.Flow;

/// <summary>
/// Main entry point for creating and managing flows and bots.
/// </summary>
public class Agent
{
    private readonly List<IFlow> _flows = new();
    private readonly List<IBot> _bots = new();
    
    public string Name { get; }

    public Agent(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        AgentContext.Agent = name;
    }

    /// <summary>
    /// Adds a new flow for the specified workflow type.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type</typeparam>
    /// <returns>A flow instance for configuring activities</returns>
    public Flow<TWorkflow> AddFlow<TWorkflow>() where TWorkflow : FlowBase
    {
        var flow = new Flow<TWorkflow>(this);
        _flows.Add(flow);
        return flow;
    }

    /// <summary>
    /// Adds a new bot for the specified bot type.
    /// </summary>
    /// <typeparam name="TBot">The bot class type</typeparam>
    /// <returns>A bot instance for configuring capabilities</returns>
    public Bot<TBot> AddBot<TBot>() where TBot : FlowBase
    {
        var bot = new Bot<TBot>(this);
        _bots.Add(bot);
        return bot;
    }

    /// <summary>
    /// Runs all configured flows and bots.
    /// </summary>
    public async Task RunAsync(RunnerOptions? options = null)
    {
        var tasks = new List<Task>();

        // Run all flows
        foreach (var flow in _flows)
        {
            tasks.Add(flow.RunAsync(options));
        }

        // Run all bots
        foreach (var bot in _bots)
        {
            tasks.Add(bot.RunAsync(options));
        }

        if (tasks.Count == 0)
        {
            throw new InvalidOperationException("No flows or bots have been added to the agent");
        }

        await Task.WhenAll(tasks);
    }

    internal AgentInfo GetAgentInfo()
    {
        return new AgentInfo(Name);
    }
}

/// <summary>
/// Interface for type-erased flow storage.
/// </summary>
internal interface IFlow
{
    Task RunAsync(RunnerOptions? options);
}

/// <summary>
/// Interface for type-erased bot storage.
/// </summary>
internal interface IBot
{
    Task RunAsync(RunnerOptions? options);
}

/// <summary>
/// Manages activities for a specific workflow type.
/// </summary>
/// <typeparam name="TWorkflow">The workflow class type</typeparam>
public class Flow<TWorkflow> : IFlow where TWorkflow : class
{
    protected readonly Agent _agent;
    protected readonly Runner<TWorkflow> _runner;

    internal Flow(Agent agent)
    {
        _agent = agent;
        var agentInfo = agent.GetAgentInfo();
        var flowInfo = new FlowInfo();
        _runner = new Runner<TWorkflow>(agentInfo, flowInfo);
    }

    /// <summary>
    /// Adds activities to this flow.
    /// </summary>
    /// <typeparam name="IActivity">The activity interface type</typeparam>
    /// <typeparam name="TActivity">The activity implementation type</typeparam>
    /// <returns>This flow instance for method chaining</returns>
    public Flow<TWorkflow> AddActivities<IActivity, TActivity>()
        where IActivity : class
        where TActivity : class
    {
        _runner.AddFlowActivities<IActivity, TActivity>();
        return this;
    }

    /// <summary>
    /// Adds activities to this flow with constructor arguments.
    /// </summary>
    /// <typeparam name="IActivity">The activity interface type</typeparam>
    /// <typeparam name="TActivity">The activity implementation type</typeparam>
    /// <param name="args">Constructor arguments for the activity</param>
    /// <returns>This flow instance for method chaining</returns>
    public Flow<TWorkflow> AddActivities<IActivity, TActivity>(params object[] args)
        where IActivity : class
        where TActivity : class
    {
        _runner.AddFlowActivities<IActivity, TActivity>(args);
        return this;
    }

    /// <summary>
    /// Runs this flow.
    /// </summary>
    public async Task RunAsync(RunnerOptions? options)
    {
        await _runner.RunAsync(options);
    }
}

/// <summary>
/// Manages capabilities for a specific bot type.
/// </summary>
/// <typeparam name="TBot">The bot class type</typeparam>
public class Bot<TBot> : Flow<TBot>, IBot where TBot : FlowBase
{
    internal Bot(Agent agent) : base(agent)
    {
    }

    /// <summary>
    /// Adds capabilities to this bot.
    /// </summary>
    /// <param name="capabilityType">The capability type to add</param>
    /// <returns>This bot instance for method chaining</returns>
    public Bot<TBot> AddCapabilities(Type capabilityType)
    {
        _runner.AddBotCapabilities(capabilityType);
        return this;
    }

    /// <summary>
    /// Adds capabilities to this bot.
    /// </summary>
    /// <typeparam name="TCapability">The capability type to add</typeparam>
    /// <returns>This bot instance for method chaining</returns>
    public Bot<TBot> AddCapabilities<TCapability>()
    {
        _runner.AddBotCapabilities<TCapability>();
        return this;
    }
}

/// <summary>
/// Manages workflow activity registration and metadata for a specific workflow class.
/// </summary>
/// <typeparam name="TClass">The workflow class type</typeparam>
public class Runner<TClass> where TClass : class
{
    private readonly Dictionary<Type, object> _activityProxies = new();
    private readonly List<Type> _capabilities = new();
    public AgentInfo AgentInfo { get; private set; }
    public FlowInfo? FlowInfo { get; private set; }
    private readonly Logger<Runner<TClass>> _logger = Logger<Runner<TClass>>.For();

    public Runner(AgentInfo agentInfo, FlowInfo? flowInfo = null)
    {
        AgentInfo = agentInfo;
        FlowInfo = flowInfo;
        // Set the agent name to Agent Context
        AgentContext.Agent = agentInfo.Name;
        // validate the runner
        Validate();
    }

    private void Validate()
    {
        if (string.IsNullOrEmpty(AgentInfo?.Name))
        {
            throw new InvalidOperationException("AgentInfo.Name is required");
        }
        // Workflow name should start with AgentName:
        if (!WorkflowName.StartsWith(AgentName + ":"))
        {
            throw new InvalidOperationException($"WorkflowName must start with `{AgentName}:`");
        }
    }

    public async Task RunAsync(RunnerOptions? options = null)
    {
        try
        {
            var runner = new WorkerService(options);
            await runner.RunFlowAsync(this);
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

    public Runner<TClass> AddBotCapabilities<TCapability>()
    {
        _capabilities.Add(typeof(TCapability));
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

        var activity = Activator.CreateInstance(typeof(TActivity), args);

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
            return AgentInfo.Name;
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
}


public class RunnerOptions
{
        public string? QueuePrefix { get; set; }
}

public class FlowInfo
{
    public bool AutoActivate { get; set; } = false;
}

public class AgentInfo
{
    public AgentInfo(string name, string? description = null, string? svgIcon = null)
    {
        Name = name;
        Description = description;
        SvgIcon = svgIcon;
    }

    public string Name { get; set; }
    public string? Description { get; set; }
    public string? SvgIcon { get; set; }
}

