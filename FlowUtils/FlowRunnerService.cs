using System.Reflection;
using Temporalio.Worker;
using Temporalio.Workflows;

public interface IFlowRunnerService
{
    Task RunFlowAsync<TFlow>(Flow<TFlow> flow, CancellationToken cancellationToken)
        where TFlow : class;
}

public class FlowRunnerService : IFlowRunnerService
{
    private readonly TemporalClientService _temporalClientService;
    private readonly TemporalConfig _temporalConfig;
    public FlowRunnerService(TemporalConfig temporalConfig, XiansAIConfig xiansAIConfig)
    {
        _temporalConfig = temporalConfig;
        _temporalClientService = new TemporalClientService(_temporalConfig);
        Globals.XiansAIConfig = xiansAIConfig;
    }

    private string GetWorkflowName<TFlow>() where TFlow : class
    {
        var workflowAttr = typeof(TFlow).GetCustomAttribute<WorkflowAttribute>();
        if (workflowAttr == null)
        {
            throw new InvalidOperationException($"Workflow {typeof(TFlow).Name} is missing WorkflowAttribute");
        }
        return workflowAttr.Name ?? typeof(TFlow).Name;
    }

    public async Task RunFlowAsync<TFlow>(Flow<TFlow> flow, CancellationToken cancellationToken)
        where TFlow : class
    {
        var client = await _temporalClientService.GetClientAsync();
        var workFlowName = GetWorkflowName<TFlow>();
        
        var options = new TemporalWorkerOptions(taskQueue: workFlowName.Replace(" ", ""));
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
