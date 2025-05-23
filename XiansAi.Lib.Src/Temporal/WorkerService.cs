using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Temporalio.Worker;
using Server;
using Temporal;
using XiansAi.Logging;

namespace XiansAi.Flow;

internal class WorkerService
{
    private readonly Logging.Logger<WorkerService> _logger;    

    private readonly RunnerOptions? _options;
    public WorkerService(RunnerOptions? options = null)
    {
        _logger = Logging.Logger<WorkerService>.For();
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

    public void TestMe()
    {
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
        var taskQueue = workFlowName; 

        if (!string.IsNullOrEmpty(_options?.QueuePrefix))
        {
            taskQueue = _options.QueuePrefix + taskQueue;
        } 

        _logger.LogInformation($"Running worker for `{workFlowName}` on queue `{taskQueue}`");

        var options = new TemporalWorkerOptions()
        {
            LoggerFactory = CreateTemporalLoggerFactory(),
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

    private LogLevel GetConsoleLogLevel()
    {
        var consoleLogLevel = Environment.GetEnvironmentVariable("CONSOLE_LOG_LEVEL")?.ToUpper();
        return consoleLogLevel switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFORMATION" => LogLevel.Information,
            "WARNING" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            "CRITICAL" => LogLevel.Critical,
            _ => LogLevel.Information // Default to Information if not set or invalid
        };
    }

    private ILoggerFactory CreateTemporalLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new ApiLoggerProvider());
            var consoleLogLevel = GetConsoleLogLevel();
            _logger.LogInformation($"Console log level: {consoleLogLevel}");
            
            // Set global minimum level to capture everything
            builder.SetMinimumLevel(LogLevel.Trace);
            
            // Configure console with specific filtering for Temporalio
            builder.AddConsole(options => 
            {
                options.LogToStandardErrorThreshold = consoleLogLevel;
            });
            
            // Explicitly filter Temporalio category to Information level for the console
            builder.AddFilter<ConsoleLoggerProvider>("Temporalio", consoleLogLevel);
        });
    }
}

