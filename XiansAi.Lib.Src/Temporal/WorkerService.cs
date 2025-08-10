
using Temporalio.Worker;
using Server;
using XiansAi.Server;
using XiansAi.Logging;
using XiansAi.Flow;
using XiansAi;

namespace Temporal;

internal class WorkerService
{
    private readonly Logger<WorkerService> _logger;    
    private readonly CertificateReader _certificateReader;
    private readonly WorkflowClientService _workflowService;
    private readonly RunnerOptions? _options;
    public WorkerService(RunnerOptions? options = null)
    {
        _logger = Logger<WorkerService>.For();
        _certificateReader = new CertificateReader();
        _workflowService = new WorkflowClientService();
        _options = options;

        ValidateConfig();

        // Initialize SecureApi first
        if (!SecureApi.IsReady)
        {
            SecureApi.InitializeClient(
                PlatformConfig.APP_SERVER_API_KEY!,
                PlatformConfig.APP_SERVER_URL!
            );
        }
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
    }

    public async Task RunFlowAsync<TFlow>(Runner<TFlow> flow, CancellationToken cancellationToken = default)
        where TFlow : class
    {

        if (cancellationToken == default) {
            // Only set up cancellation token if CommandLineHelper hasn't already done it
            // This prevents conflicts between multiple Console.CancelKeyPress handlers
            if (!CommandLineHelper.IsShutdownConfigured())
            {
                // Cancellation token cancelled on ctrl+c
                var tokenSource = new CancellationTokenSource();
                Console.CancelKeyPress += (_, eventArgs) => { tokenSource.Cancel(); eventArgs.Cancel = true; };
                cancellationToken = tokenSource.Token;
            }
            else
            {
                // CommandLineHelper is handling shutdown, use its cancellation token
                cancellationToken = CommandLineHelper.GetShutdownToken();
            }
        }

        // Upload the flow definition to the server
        await new FlowDefinitionUploader().UploadFlowDefinition(flow);

        // Run the worker for the flow
        var client = await TemporalClientService.Instance.GetClientAsync();

        var workFlowType = flow.WorkflowName;
        var taskQueue = _certificateReader.ReadCertificate()?.TenantId + ":" + workFlowType; 

        if (!string.IsNullOrEmpty(_options?.QueuePrefix))
        {
            taskQueue = _options.QueuePrefix + taskQueue;
        } 

        _logger.LogInformation($"Running worker for `{workFlowType}` on queue `{taskQueue}`");

        var options = new TemporalWorkerOptions()
        {
            LoggerFactory = LoggingUtils.CreateTemporalLoggerFactory(),
            TaskQueue = taskQueue
        };
        
        options.AddWorkflow<TFlow>();
        foreach (var stub in flow.ActivityProxies)
        {
            options.AddAllActivities(stub.Key, stub.Value);
        }
        // Add all activities from the SystemActivities class
        options.AddAllActivities(new SystemActivities(flow));

        // Add simple object activities
        foreach (var activity in flow.ObjectActivities.Values)
        {
            _logger.LogDebug($"Adding object activity {activity.GetType().Name}, type {activity.GetType()}");
            options.AddAllActivities(activity.GetType(), activity);
        }


        var worker = new TemporalWorker(
            client,
            options
        );
        _logger.LogTrace($"Worker to run `{workFlowType}` on queue `{taskQueue}` created. Ready to run!!");

        // Start the workflow if it is configured to start automatically
        if (flow.StartAutomatically)
        {
            _logger.LogInformation($"Starting workflow `{workFlowType}`");
            await _workflowService.StartWorkflow(workFlowType, []);
        }
        
        try
        {
            await worker.ExecuteAsync(cancellationToken!);
        }
        finally
        {
            // Ensure proper cleanup when the worker stops
            _logger.LogInformation("Worker execution completed. Cleaning up temporal connections...");
            await TemporalClientService.CleanupAsync();
        }
    }
}

