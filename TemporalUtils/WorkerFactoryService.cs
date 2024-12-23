using Temporalio.Client;
using Temporalio.Worker;

public interface IWorkerFactoryService
{
    Task<TemporalWorker> CreateWorkerAsync<TWorkflow, TActivities>(TActivities activities)
        where TWorkflow : class
        where TActivities : class;
}

public class WorkerFactoryService : IWorkerFactoryService
{
    private readonly TemporalClientService _temporalClientService;
    private readonly TemporalConfig _temporalConfig;

    private readonly List<TemporalWorker> _workers = new();

    public WorkerFactoryService(TemporalConfig temporalConfig)
    {
        _temporalConfig = temporalConfig;
        _temporalClientService = new TemporalClientService(_temporalConfig);
    }

    public async Task<TemporalWorker> CreateWorkerAsync<TWorkflow, TActivities>(TActivities activities)
        where TWorkflow : class
        where TActivities : class
    {
        var client = await _temporalClientService.GetClientAsync();

        var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions(taskQueue: "DefaultQueue")
                .AddAllActivities(activities)
                .AddWorkflow<TWorkflow>()
        );
        _workers.Add(worker);

        return worker;
    }

    public TemporalWorker[] GetAllWorkers()
    {
        return _workers.ToArray();
    }
}
