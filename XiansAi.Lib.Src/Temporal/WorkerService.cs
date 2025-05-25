
using Temporalio.Worker;
using Server;
using Temporal;
using XiansAi.Server;

namespace XiansAi.Flow;

internal class WorkerService
{
    private readonly Logging.Logger<WorkerService> _logger;    
    private readonly CertificateReader _certificateReader;

    private readonly RunnerOptions? _options;
    public WorkerService(RunnerOptions? options = null)
    {
        _logger = Logging.Logger<WorkerService>.For();
        _certificateReader = new CertificateReader();
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
            // Cancellation token cancelled on ctrl+c
            var tokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) => { tokenSource.Cancel(); eventArgs.Cancel = true; };
            cancellationToken = tokenSource.Token;
        }

        // Upload the flow definition to the server
        await new FlowDefinitionUploader().UploadFlowDefinition(flow);

        // Run the worker for the flow
        var client = TemporalClientService.Instance.GetClientAsync();

        var workFlowName = flow.WorkflowName;
        var taskQueue = _certificateReader.ReadCertificate()?.TenantId + ":" + workFlowName; 

        if (!string.IsNullOrEmpty(_options?.QueuePrefix))
        {
            taskQueue = _options.QueuePrefix + taskQueue;
        } 

        _logger.LogInformation($"Running worker for `{workFlowName}` on queue `{taskQueue}`");

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
        options.AddAllActivities(new SystemActivities(flow.Capabilities));

        var worker = new TemporalWorker(
            client,
            options
        );
        _logger.LogInformation($"Worker to run `{workFlowName}` on queue `{taskQueue}` created. Ready to run!!");
        await worker.ExecuteAsync(cancellationToken!);
    }
}

