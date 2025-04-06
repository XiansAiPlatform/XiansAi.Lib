using System.Reflection;
using Microsoft.Extensions.Logging;
using Temporalio.Worker;
using Temporalio.Workflows;
using Server.Http;
using Server;
using Temporal;

namespace XiansAi.Flow;

public interface IFlowRunnerService
{
    Task RunFlowAsync<TFlow>(FlowInfo<TFlow> flow, CancellationToken cancellationToken)
        where TFlow : class;
}

public class FlowRunnerOptions
{
    public ILoggerFactory? LoggerFactory { get; set; }
    public string? PriorityQueue { get; set; }
}

public class FlowRunnerService : IFlowRunnerService
{
    private readonly TemporalClientService _temporalClientService;
    private readonly ILogger<FlowRunnerService> _logger;
    private readonly FlowDefinitionUploader _flowDefinitionUploader;
    private readonly string? _priorityQueue;

    public static void SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        Globals.LogFactory = loggerFactory;
    }

    public FlowRunnerService(FlowRunnerOptions? options = null): this(options?.LoggerFactory)
    {
        if (options?.PriorityQueue != null)
        {
            _priorityQueue = options.PriorityQueue;
        }
    }

    private FlowRunnerService(ILoggerFactory? loggerFactory = null)
    {
        if (loggerFactory != null)
        {
            SetLoggerFactory(loggerFactory);
        }
        _logger = Globals.LogFactory.CreateLogger<FlowRunnerService>();
        _temporalClientService = new TemporalClientService();
        _flowDefinitionUploader = new FlowDefinitionUploader();

        if (PlatformConfig.APP_SERVER_API_KEY != null && PlatformConfig.APP_SERVER_URL != null)
        {
            _logger.LogDebug("Initializing SecureApi with AppServerUrl: {AppServerUrl}", PlatformConfig.APP_SERVER_URL);
            SecureApi.InitializeClient(
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
        HttpClient? client = null;


        if (PlatformConfig.APP_SERVER_API_KEY != null)
        {
            client = SecureApi.InitializeClient(
                PlatformConfig.APP_SERVER_API_KEY,
                PlatformConfig.APP_SERVER_URL ?? throw new InvalidOperationException("APP_SERVER_URL is not set")
            );
        }
        else
        {
            _logger.LogError("App server connection failed because of missing configuration");
            throw new InvalidOperationException("App server connection failed because of missing configuration");
        }

        if (client == null)
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

        var taskQueue = string.IsNullOrEmpty(_priorityQueue) ? workFlowName : _priorityQueue + "--" + workFlowName;

        var options = new TemporalWorkerOptions(taskQueue: taskQueue)
        {
            LoggerFactory = Globals.LogFactory,
        };
        
        options.AddWorkflow<TFlow>();
        foreach (var stub in flow.GetStubProxies())
        {
            options.AddAllActivities(stub.Key, stub.Value);
        }

        var worker = new TemporalWorker(
            client,
            options
        );
        _logger.LogInformation("Worker to run `{FlowName}` on queue `{Queue}` created. Ready to run!", workFlowName, taskQueue);
        await worker.ExecuteAsync(cancellationToken);
    }
}

