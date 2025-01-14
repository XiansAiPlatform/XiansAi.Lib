using System.Reflection;
using Microsoft.Extensions.Logging;
using Temporalio.Worker;
using Temporalio.Workflows;
using XiansAi.Http;
using XiansAi.Temporal;
using XiansAi.Server;
using XiansAi.Models;

namespace XiansAi.Flow;

public interface IFlowRunnerService
{
    Task RunFlowAsync<TFlow>(FlowInfo<TFlow> flow, CancellationToken cancellationToken)
        where TFlow : class;
}

public class FlowRunnerService : IFlowRunnerService
{
    private readonly TemporalClientService _temporalClientService;
    private readonly PlatformConfig _config;
    private readonly ILogger<FlowRunnerService> _logger;
    private readonly FlowDefinitionUploader _flowDefinitionUploader;

    public FlowRunnerService(PlatformConfig config)
    {
        _logger = Globals.LogFactory.CreateLogger<FlowRunnerService>();
        _config = config;
        _temporalClientService = new TemporalClientService(_config);
        _flowDefinitionUploader = new FlowDefinitionUploader();
        
        if (config.AppServerCertPath != null && config.AppServerCertPwd != null && config.AppServerUrl != null) {
            _logger.LogDebug("Initializing SecureApi with AppServerUrl: {AppServerUrl}", config.AppServerUrl);
            SecureApi.Initialize(
                config.AppServerCertPath,
                config.AppServerCertPwd,
                config.AppServerUrl
            );
        }
    }

    public async Task TestMe()
    {
        _logger.LogInformation("Trying to connect to flow server at: {FlowServerUrl}", _config.FlowServerUrl);
        var temporalClient = await _temporalClientService.GetClientAsync();
        if (temporalClient == null)
        {
            _logger.LogError("Flow server connection failed");
            return;
        } else {
            _logger.LogInformation("Flow server is successfully connected");
        }

        _logger.LogInformation("Trying to connect to app server at: {AppServerUrl}", _config.AppServerUrl);
        var secureApi = SecureApi.Initialize(_config.AppServerCertPath, _config.AppServerCertPwd, _config.AppServerUrl);
        if (secureApi == null)
        {
            _logger.LogError("App server connection failed");
            return;
        } else {
            _logger.LogInformation("App server is successfully connected");
        }

        _logger.LogInformation("Flow server is successfully configured");

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

    public async Task RunFlowAsync<TFlow>(FlowInfo<TFlow> flow, CancellationToken cancellationToken = default)
        where TFlow : class
    {
        // Upload the flow definition to the server
        await _flowDefinitionUploader.UploadFlowDefinition(flow);

        // Run the worker for the flow
        var client = await _temporalClientService.GetClientAsync();
        var workFlowType = GetWorkflowName<TFlow>();
        
        var options = new TemporalWorkerOptions(taskQueue: workFlowType);
        options.AddWorkflow<TFlow>();
        foreach (var activity in flow.GetProxyActivities())
        {
            options.AddAllActivities(activity.Key, activity.Value);
        }

        var worker = new TemporalWorker(
            client,
            options
        );
        _logger.LogInformation("Worker process to run flow `{FlowName}` is successfully created. Ready to run flow tasks!", workFlowType);
        await worker.ExecuteAsync(cancellationToken);
    }
}

