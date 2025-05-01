using System.Reflection;
using Microsoft.Extensions.Logging;
using Temporalio.Worker;
using Temporalio.Workflows;
using Server;
using Temporal;
using XiansAi.Logging;

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
    private readonly Logging.Logger<FlowRunnerService> _logger;
    private readonly string? _priorityQueue;
    private readonly Lazy<Task>? _initializationTask;
    
    public FlowRunnerService(FlowRunnerOptions? options = null)
    {
        if (options?.PriorityQueue != null)
        {
            _priorityQueue = options.PriorityQueue;
        }

        if (options?.LoggerFactory != null)
        {
            Globals.LogFactory = options.LoggerFactory;
        }

        ValidateConfig();

        // Initialize SecureApi first
        if (!SecureApi.IsReady)
        {
            SecureApi.InitializeClient(
                PlatformConfig.APP_SERVER_API_KEY!,
                PlatformConfig.APP_SERVER_URL!
            );
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("TEST_CONFIGURATION"), out var testConfiguration) && testConfiguration)
        {
            _initializationTask = new Lazy<Task>(() => Task.Run(TestMe));
            // Force initialization
            _ = _initializationTask.Value;
        }

        _logger = Logging.Logger<FlowRunnerService>.For();
    }
    
    private void ValidateConfig()
    {
        if (string.IsNullOrEmpty(PlatformConfig.APP_SERVER_API_KEY))
        {
            _logger.LogError("App server connection failed because of missing configuration");
            throw new InvalidOperationException("App server connection failed because of missing APP_SERVER_API_KEY");
        }
        if (string.IsNullOrEmpty(PlatformConfig.APP_SERVER_URL))
        {
            _logger.LogError("App server connection failed because of missing configuration");
            throw new InvalidOperationException("App server connection failed because of missing APP_SERVER_URL");
        }
        if (string.IsNullOrEmpty(PlatformConfig.FLOW_SERVER_URL))
        {
            _logger.LogError("Flow server connection failed because of missing configuration");
            throw new InvalidOperationException("Flow server connection failed because of missing FLOW_SERVER_URL");
        }
        if (string.IsNullOrEmpty(PlatformConfig.FLOW_SERVER_NAMESPACE))
        {   
            _logger.LogError("Flow server connection failed because of missing configuration");
            throw new InvalidOperationException("Flow server connection failed because of missing FLOW_SERVER_NAMESPACE");
        }
        if (string.IsNullOrEmpty(PlatformConfig.FLOW_SERVER_API_KEY))
        {
            _logger.LogError("Flow server connection failed because of missing configuration");
            throw new InvalidOperationException("Flow server connection failed because of missing FLOW_SERVER_API_KEY");
        }
    }

    public void TestMe()
    {
        _logger.LogInformation($"Trying to connect to flow server at: {PlatformConfig.FLOW_SERVER_URL}");
        var temporalClient = TemporalClientService.Instance.GetClientAsync();
        if (temporalClient == null)
        {
            _logger.LogError("Flow server connection failed");
            return;
        }
        else
        {
            _logger.LogInformation("Flow server is successfully connected");
        }

        _logger.LogInformation($"Trying to connect to app server at: {PlatformConfig.APP_SERVER_URL}");
        HttpClient? client = null;

        if (PlatformConfig.APP_SERVER_API_KEY != null)
        {
            if (!SecureApi.IsReady)
            {
                _logger.LogError("App server connection failed because SecureApi is not ready");
                throw new InvalidOperationException("App server connection failed because SecureApi is not ready");
            }
            client = SecureApi.Instance.Client;
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

        if (cancellationToken == default) {
            // Cancellation token cancelled on ctrl+c
            var tokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) => { tokenSource.Cancel(); eventArgs.Cancel = true; };
            cancellationToken = tokenSource.Token;
        }


        // Upload the flow definition to the server
        await new FlowDefinitionUploader().UploadFlowDefinition(flow);

        // Run the worker for the flow
        var client = TemporalClientService.Instance.GetClientAsync();
        var workFlowName = GetWorkflowName<TFlow>();

        var taskQueue = string.IsNullOrEmpty(_priorityQueue) ? workFlowName : _priorityQueue + "--" + workFlowName;

        var options = new TemporalWorkerOptions(taskQueue: taskQueue)
        {
            LoggerFactory = CreateTemporalLoggerFactory(),
        };
        
        options.AddWorkflow<TFlow>();
        foreach (var stub in flow.GetStubProxies())
        {
            options.AddAllActivities(stub.Key, stub.Value);
        }
        // Add all activities from the SystemActivities class
        options.AddAllActivities(new SystemActivities());

        var worker = new TemporalWorker(
            client,
            options
        );
        _logger.LogInformation($"Worker to run `{workFlowName}` on queue `{taskQueue}` created. Ready to run!!");
        await worker.ExecuteAsync(cancellationToken!);
    }

    private ILoggerFactory CreateTemporalLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new ApiLoggerProvider("/api/agent/logs"));
            builder.SetMinimumLevel(LogLevel.Trace);
        });
    }
}

