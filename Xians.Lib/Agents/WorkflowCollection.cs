namespace Xians.Lib.Agents;

/// <summary>
/// Manages the collection of workflows for an agent.
/// </summary>
public class WorkflowCollection
{
    private readonly XiansAgent _agent;
    private readonly List<XiansWorkflow> _workflows = new();

    internal WorkflowCollection(XiansAgent agent)
    {
        _agent = agent;
    }

    /// <summary>
    /// Defines the default workflow for the agent.
    /// </summary>
    /// <param name="workers">Number of workers for the workflow. Default is 1.</param>
    /// <param name="name">Optional name for the workflow.</param>
    /// <returns>A new default XiansWorkflow instance.</returns>
    public XiansWorkflow DefineDefault(int workers = 1, string? name = null)
    {
        // TODO: Implement default workflow creation
        var workflow = new XiansWorkflow(_agent, name, workers);
        _workflows.Add(workflow);
        return workflow;
    }

    /// <summary>
    /// Defines a custom workflow for the agent.
    /// </summary>
    /// <typeparam name="T">The custom workflow type.</typeparam>
    /// <param name="workers">Number of workers for the workflow. Default is 1.</param>
    /// <returns>A new custom XiansWorkflow instance.</returns>
    public XiansWorkflow DefineCustom<T>(int workers = 1) where T : class
    {
        // TODO: Implement custom workflow creation
        var workflow = new XiansWorkflow(_agent, typeof(T).Name, workers);
        _workflows.Add(workflow);
        return workflow;
    }

    /// <summary>
    /// Runs all registered workflows asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task RunAllAsync()
    {
        // TODO: Consider running workflows concurrently with Task.WhenAll
        var tasks = _workflows.Select(w => w.RunAsync());
        await Task.WhenAll(tasks);
    }
}

