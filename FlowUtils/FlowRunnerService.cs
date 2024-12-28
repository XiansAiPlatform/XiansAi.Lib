using Temporalio.Worker;

public interface IFlowRunnerService
{
    Task RunFlowAsync<TFlow>(Flow<TFlow> flow, CancellationToken cancellationToken)
        where TFlow : class;
}

public class FlowRunnerService : IFlowRunnerService
{
    private readonly TemporalClientService _temporalClientService;
    private readonly TemporalConfig _temporalConfig;

    public FlowRunnerService(TemporalConfig temporalConfig)
    {
        _temporalConfig = temporalConfig;
        _temporalClientService = new TemporalClientService(_temporalConfig);
    }

    public async Task RunFlowAsync<TFlow>(Flow<TFlow> flow, CancellationToken cancellationToken)
        where TFlow : class
    {
        var client = await _temporalClientService.GetClientAsync();
        var options = new TemporalWorkerOptions(taskQueue: "DefaultQueue");
        options.AddWorkflow<TFlow>();
        foreach (var activity in flow.GetActivities())
        {
            options.AddAllActivities(activity.Key, activity.Value);
        }

        var worker = new TemporalWorker(
            client,
            options
        );

        await worker.ExecuteAsync(cancellationToken);
    }

}
