using System.Reflection;
using Server;
using Temporal;
using Temporalio.Workflows;
using XiansAi.Activity;
using XiansAi.Logging;
using XiansAi.Models;

namespace XiansAi.Flow;

public class KernelPluginOptions {
    public bool Enabled { get; set; } = true;
}

internal class KernelPlugins {
    public KernelPluginOptions DatePlugin { get; set; } = new();
}


/// <summary>
/// Manages workflow activity registration and metadata for a specific workflow class.
/// </summary>
/// <typeparam name="TClass">The workflow class type</typeparam>
public class Runner<TClass> where TClass : class
{
    internal KernelPlugins Plugins { get; set; } = new();

    private readonly Dictionary<Type, object> _activityProxies = new();
    private readonly List<Type> _capabilities = new();
    public Type? DataProcessorType { get; set; }
    public bool ProcessDataInWorkflow { get; set; } = false;
    public Type? ScheduleProcessorType { get; set; }
    public bool ProcessScheduleInWorkflow { get; set; } = false;
    public bool StartAutomatically { get; set; } = true;

#pragma warning disable CS0618 // Type or member is obsolete
    public AgentInfo AgentInfo { get; private set; }
    public FlowInfo? FlowInfo { get; private set; }
    private readonly Logger<Runner<TClass>> _logger = Logger<Runner<TClass>>.For();

    public Runner(AgentInfo agentInfo, FlowInfo? flowInfo = null)
    {
        AgentInfo = agentInfo;
        FlowInfo = flowInfo;
        // Set the agent name to Agent Context
        AgentContext.AgentName = agentInfo.Name;
        // validate the runner
        Validate();
        // test the connection to the server
        SecureApi.InitializeClient(
                PlatformConfig.APP_SERVER_API_KEY!,
                PlatformConfig.APP_SERVER_URL!
            );
        SecureApi.Instance.TestConnection();
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

