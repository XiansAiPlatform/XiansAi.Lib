
namespace XiansAi.Flow;

/// <summary>
/// Interface for type-erased flow storage.
/// </summary>
internal interface IFlow
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
    /// Sets the data processor for this flow.
    /// </summary>
    /// <typeparam name="TDataProcessor"></typeparam>
    /// <returns></returns>
    public Flow<TWorkflow> SetDataProcessor<TDataProcessor>()
    {
        _runner.DataProcessorType = typeof(TDataProcessor);
        return this;
    }

    /// <summary>
    /// Sets the data processor for this flow.
    /// </summary>
    /// <param name="dataProcessorType"></param>
    /// <returns></returns>
    public Flow<TWorkflow> SetDataProcessor(Type dataProcessorType)
    {
        _runner.DataProcessorType = dataProcessorType;
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