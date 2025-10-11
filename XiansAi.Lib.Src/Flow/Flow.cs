
using Temporal;
using Temporalio.Client.Schedules;
using XiansAi.Scheduler;

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
    protected readonly AgentTeam _agentTeam;
    protected readonly Runner<TWorkflow> _runner;

    internal Flow(AgentTeam agentTeam, int numberOfWorkers)
    {
        _agentTeam = agentTeam;

        var agentInfo = agentTeam.GetAgentInfo();
        _runner = new Runner<TWorkflow>(agentInfo, numberOfWorkers);
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

    public Flow<TWorkflow> AddActivities<TActivity>(object activity)
    {
        _runner.AddFlowActivities<TActivity>(activity);
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
    public Flow<TWorkflow> SetDataProcessor<TDataProcessor>(bool processInWorkflow = false)
    {
        _runner.DataProcessorType = typeof(TDataProcessor);
        _runner.ProcessDataInWorkflow = processInWorkflow;
        return this;
    }

    public async Task<Flow<TWorkflow>> SetScheduleAsync(
        ScheduleSpec spec,
        string? scheduleName = null
    )
    {
        scheduleName = scheduleName ?? WorkflowIdentifier.GetWorkflowTypeFor(typeof(TWorkflow)) + "__default_schedule";
        await SchedulerHub.DeleteAsync(scheduleName);

        await SchedulerHub.CreateScheduleAsync(typeof(TWorkflow), spec, scheduleName);
        return this;
    }

    /// <summary>
    /// Sets the schedule processor for this flow.
    /// </summary>
    /// <typeparam name="TProcessor"></typeparam>
    /// <param name="processInWorkflow"> If true, the schedule processor will be processed in the Temporal workflow. If false, the schedule processor will be processed in the Temporal activity.</param>
    /// <param name="startAutomatically"> If true, the schedule processor will start automatically. If false, the schedule processor will not start automatically. Ignored if systemScoped is true.</param>
    /// <param name="runAtStart"> If true, the first execution of the schedule processor will run at start. If false, the schedule processor will not run at start but wait for the wait time to start.</param>
    /// <returns></returns>
    public Flow<TWorkflow> SetScheduleProcessor<TProcessor>(
        bool processInWorkflow = false,
        bool startAutomatically = true,
        bool runAtStart = false
    )
    {
        _runner.ScheduleProcessorType = typeof(TProcessor);
        _runner.ProcessScheduleInWorkflow = processInWorkflow;
        _runner.StartAutomatically = startAutomatically;
        _runner.RunAtStart = runAtStart;

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