using Temporalio.Client;
using Temporalio.Worker;

public interface IWorkerFactoryService
{
    Task<TemporalWorker> CreateWorkerAsync<TWorkflow>(Dictionary<Type, object> activities)
        where TWorkflow : class;
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

    public async Task<TemporalWorker> CreateWorkerAsync<TWorkflow>(Dictionary<Type, object> activities)
        where TWorkflow : class
    {
        var client = await _temporalClientService.GetClientAsync();
        var options = new TemporalWorkerOptions(taskQueue: "DefaultQueue");
        options.AddWorkflow<TWorkflow>();
        foreach (var activity in activities)
        {
            options.AddAllActivities(activity.Key, activity.Value);
        }

        var worker = new TemporalWorker(
            client,
            options
        );
        _workers.Add(worker);

        return worker;
    }

    public TemporalWorker[] GetAllWorkers()
    {
        return _workers.ToArray();
    }
}
