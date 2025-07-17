using Server;
using XiansAi.Models;

namespace XiansAi.Flow;

/// <summary>
/// Main entry point for creating and managing flows and bots.
/// </summary>
public class Agent
{
    private readonly List<IFlow> _flows = new();
    private readonly List<IBot> _bots = new();
    private readonly bool _uploadResources;
    public string Name { get; }

    public Agent(string name, bool? uploadResources=null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _uploadResources = uploadResources.GetValueOrDefault()
            || (bool.TryParse(Environment.GetEnvironmentVariable("UPLOAD_RESOURCES"), out var flag) && flag);

        AgentContext.AgentName = name;
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
        await new ResourceUploader(_uploadResources).UploadResource();

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

#pragma warning disable CS0618 // Type or member is obsolete
    internal AgentInfo GetAgentInfo()
#pragma warning restore CS0618 // Type or member is obsolete
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return new AgentInfo(Name);
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
