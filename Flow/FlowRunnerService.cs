using System.Reflection;
using Microsoft.Extensions.Logging;
using Temporalio.Worker;
using Temporalio.Workflows;
using XiansAi.Http;
using XiansAi.Temporal;
using XiansAi.Server;
using System.Runtime.Serialization;
using System.Text.Json;

namespace XiansAi.Flow;

public interface IFlowRunnerService
{
    Task RunFlowAsync<TFlow>(FlowInfo<TFlow> flow, CancellationToken cancellationToken)
        where TFlow : class;
}

public class FlowRunnerService : IFlowRunnerService
{
    private readonly TemporalClientService _temporalClientService;
    private readonly Config _config;
    private readonly ILogger<FlowRunnerService> _logger;
    private readonly FlowDefinitionUploader _flowDefinitionUploader;

    public FlowRunnerService(Config config)
    {
        _logger = Globals.LogFactory.CreateLogger<FlowRunnerService>();
        _config = config;
        _temporalClientService = new TemporalClientService(_config);
        _flowDefinitionUploader = new FlowDefinitionUploader();
        
        if (config.AppServerCertPath != null && config.AppServerCertPwd != null && config.AppServerUrl != null) {
            _logger.LogInformation("Initializing SecureApi with AppServerUrl: {AppServerUrl}", config.AppServerUrl);
            SecureApi.Initialize(
                config.AppServerCertPath,
                config.AppServerCertPwd,
                config.AppServerUrl
            );
        }
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

    public async Task RunFlowAsync<TFlow>(FlowInfo<TFlow> flow, CancellationToken cancellationToken)
        where TFlow : class
    {
        // Upload the flow definition to the server
        //await _flowDefinitionUploader.UploadFlowDefinition(flow);

        // Run the worker for the flow
        var client = await _temporalClientService.GetClientAsync();
        var workFlowName = GetWorkflowName<TFlow>();
        
        var options = new TemporalWorkerOptions(taskQueue: workFlowName.Replace(" ", ""));
        options.AddWorkflow<TFlow>();
        foreach (var activity in flow.GetProxyActivities())
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
