
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
    private readonly RunnerOptions? _options;
    public WorkerService(RunnerOptions? options = null)
    {
        _logger = Logger<WorkerService>.For();
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

    public async Task RunFlowAsync<TFlow>(Runner<TFlow> runner, CancellationToken cancellationToken = default)
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
        await new FlowDefinitionUploader().UploadFlowDefinition(runner);

        // Run the worker for the flow
        var client = await TemporalClientService.Instance.GetClientAsync();

        var workFlowType = runner.WorkflowName;
        var taskQueue = _certificateReader.ReadCertificate()?.TenantId + ":" + workFlowType; 

        if (!string.IsNullOrEmpty(_options?.QueuePrefix))
        {
            taskQueue = _options.QueuePrefix + taskQueue;
        } 

        var options = new TemporalWorkerOptions()
        {
            LoggerFactory = LoggingUtils.CreateTemporalLoggerFactory(),
            TaskQueue = taskQueue
        };
        
        options.AddWorkflow<TFlow>();
        foreach (var stub in runner.ActivityProxies)
        {
            options.AddAllActivities(stub.Key, stub.Value);
        }
        // Add all activities from the SystemActivities class
        options.AddAllActivities(new SystemActivities(runner));

        // Add simple object activities
        foreach (var activity in runner.ObjectActivities.Values)
        {
            _logger.LogDebug($"Adding object activity {activity.GetType().Name}, type {activity.GetType()}");
            options.AddAllActivities(activity.GetType(), activity);
        }

        // Start the workflow if it is configured to start automatically
        if (runner.StartAutomatically)
        {
            _logger.LogInformation($"Starting workflow `{workFlowType}` for agent `{runner.AgentName}`");
            var workflowService = new WorkflowClientService(runner.AgentName);

            await workflowService.StartWorkflow(workFlowType, []);
        }

        // Create all worker tasks with proper disposal
        var workers = new List<TemporalWorker>();
        var workerTasks = new List<Task>();
        
        try
        {
            for (int i = 0; i < runner.NumberOfWorkers; i++)
            {
                var worker = new TemporalWorker(
                    client,
                    options
                );
                workers.Add(worker);
                _logger.LogDebug($"Worker {i + 1} to run `{workFlowType}` on queue `{taskQueue}` created. Ready to run!!");

                // Create task for this worker
                var workerIndex = i + 1; // Capture the current value
                var workerTask = Task.Run(async () =>
                {
                    try
                    {
                        await worker.ExecuteAsync(cancellationToken!);
                        _logger.LogInformation($"Worker {workerIndex} execution completed on queue `{taskQueue}`");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation($"Worker {workerIndex} execution cancelled");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Worker {workerIndex} encountered an error: {ex.Message}");
                        throw;
                    }
                }, cancellationToken);
                
                workerTasks.Add(workerTask);
            }
            
            // Wait for all workers to complete
            await Task.WhenAll(workerTasks);
        }
        finally
        {
            // Dispose all workers first to ensure proper shutdown
            foreach (var worker in workers)
            {
                try
                {
                    worker?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error disposing worker: {ex.Message}");
                }
            }
            
            // Log completion but don't cleanup TemporalClientService here
            // Let CommandLineHelper handle the global cleanup to avoid race conditions
            _logger.LogInformation($"All workers execution completed on queue `{taskQueue}`. Cleaning up temporal connections...");
        }

    }
}

