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
    private readonly ILogger<FlowRunnerService> _logger;
    private readonly FlowDefinitionUploader _flowDefinitionUploader;

    public static void SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        Globals.LogFactory = loggerFactory;
    }

    public FlowRunnerService()
    {
        _logger = Globals.LogFactory.CreateLogger<FlowRunnerService>();
        _temporalClientService = new TemporalClientService();
        _flowDefinitionUploader = new FlowDefinitionUploader();

        if (PlatformConfig.APP_SERVER_CERT_PWD != null)
        {
            if (PlatformConfig.APP_SERVER_CERT_PATH != null && PlatformConfig.APP_SERVER_URL != null)
            {
                _logger.LogDebug("Initializing SecureApi with AppServerUrl: {AppServerUrl}", PlatformConfig.APP_SERVER_URL);
                SecureApi.Initialize(
                    PlatformConfig.APP_SERVER_CERT_PATH,
                    PlatformConfig.APP_SERVER_URL,
                    PlatformConfig.APP_SERVER_CERT_PWD
                );

            }
            else
            {
                _logger.LogError("App server connection failed because of missing configuration");
            }
        }
        else if (PlatformConfig.APP_SERVER_API_KEY != null && PlatformConfig.APP_SERVER_URL != null)
        {
            _logger.LogDebug("Initializing SecureApi with AppServerUrl: {AppServerUrl}", PlatformConfig.APP_SERVER_URL);
            SecureApi.Initialize(
                PlatformConfig.APP_SERVER_API_KEY,
                PlatformConfig.APP_SERVER_URL
            );
        }
        else
        {
            _logger.LogError("App server connection failed because of missing configuration");
        }
    }

    public async Task TestMe()
    {
        _logger.LogInformation("Trying to connect to flow server at: {FlowServerUrl}", PlatformConfig.FLOW_SERVER_URL);
        var temporalClient = await _temporalClientService.GetClientAsync();
        if (temporalClient == null)
        {
            _logger.LogError("Flow server connection failed");
            return;
        }
        else
        {
            _logger.LogInformation("Flow server is successfully connected");
        }

        _logger.LogInformation("Trying to connect to app server at: {AppServerUrl}", PlatformConfig.APP_SERVER_URL);
        var secureApi = SecureApi.Initialize(
            PlatformConfig.APP_SERVER_CERT_PATH ?? throw new InvalidOperationException("APP_SERVER_CERT_PATH is not set"),
            PlatformConfig.APP_SERVER_CERT_PWD ?? throw new InvalidOperationException("APP_SERVER_CERT_PWD is not set"),
            PlatformConfig.APP_SERVER_URL ?? throw new InvalidOperationException("APP_SERVER_URL is not set")
        );
        if (secureApi == null)
        {
            _logger.LogError("App server connection failed");
            return;
        }
        else
        {
            _logger.LogInformation("App server is successfully connected");
        }

        _logger.LogInformation("All connections are successful! You are ready to go!");

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
        var workFlowName = GetWorkflowName<TFlow>();

        var options = new TemporalWorkerOptions(taskQueue: workFlowName);
        options.AddWorkflow<TFlow>();
        foreach (var stub in flow.GetStubProxies())
        {
            options.AddAllActivities(stub.Key, stub.Value);
        }

        var worker = new TemporalWorker(
            client,
            options
        );
        _logger.LogInformation("Worker process to run flow `{FlowName}` is successfully created. Ready to run flow tasks!", workFlowName);
        await worker.ExecuteAsync(cancellationToken);
    }
}

