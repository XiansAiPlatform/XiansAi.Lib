using Server;
using XiansAi.Models;
using Temporal;

namespace XiansAi.Flow;

/// <summary>
/// Main entry point for creating and managing flows and bots.
/// </summary>
[Obsolete("Use AgentTeam instead")]
public class Agent : AgentTeam
{
    public Agent(string name, RunnerOptions? options = null) : base(name, options){}

}

public class AgentTeam {
    private readonly List<IFlow> _flows = new();
    private readonly List<IBot> _bots = new();
    public string Name { get; }

    private readonly bool? _uploadResources;
    private readonly RunnerOptions? _runnerOptions;

    public AgentTeam(string name, bool? uploadResources=null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _uploadResources = uploadResources;
    }

    public AgentTeam(string name, RunnerOptions? options)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _runnerOptions = options;
        _uploadResources = options?.UploadResources;

        // Set the system scoped flag
        var systemScoped = options?.SystemScoped == true;
        AgentContext.SystemScoped = systemScoped;

        // Initialize SecureApi first
        if (!SecureApi.IsReady)
        {
            SecureApi.InitializeClient(
                PlatformConfig.APP_SERVER_API_KEY!,
                PlatformConfig.APP_SERVER_URL!
            );
        }
    }

    /// <summary>
    /// Adds a new flow for the specified workflow type.
    /// </summary>
    /// <typeparam name="TWorkflow">The workflow class type</typeparam>
    /// <returns>A flow instance for configuring activities</returns>
    [Obsolete("Use AddAgent instead")]
    public Flow<TWorkflow> AddFlow<TWorkflow>(int numberOfWorkers = 1) where TWorkflow : FlowBase
    {
        var flow = new Flow<TWorkflow>(this, numberOfWorkers);
        _flows.Add(flow);
        return flow;
    }

    /// <summary>
    /// Adds a new bot for the specified bot type.
    /// </summary>
    /// <typeparam name="TBot">The bot class type</typeparam>
    /// <returns>A bot instance for configuring capabilities</returns>
    [Obsolete("Use AddAgent instead")]
    public Bot<TBot> AddBot<TBot>(int numberOfWorkers = 1) where TBot : FlowBase
    {
        var bot = new Bot<TBot>(this, numberOfWorkers);
        _bots.Add(bot);
        return bot;
    }

    /// <summary>
    /// Adds a new agent for the specified agent type.
    /// </summary>
    /// <typeparam name="TBot">The agent class type</typeparam>
    /// <returns>A bot instance for configuring capabilities</returns>
    public Agent<TAgent> AddAgent<TAgent>(int numberOfWorkers = 1) where TAgent : FlowBase
    {
        var bot = new Agent<TAgent>(this, numberOfWorkers);
        _bots.Add(bot);
        return bot;
    }

    /// <summary>
    /// Runs all configured flows and bots.
    /// </summary>
    public async Task RunAsync()
    {
        await RunAsyncInternal(_runnerOptions);
    }

    /// <summary>
    /// Runs all configured flows and bots.
    /// </summary>
    /// <param name="options">Runner options (deprecated: pass options to constructor instead)</param>
    [Obsolete("Pass RunnerOptions to the Agent constructor instead. This method will be removed in a future version.")]
    public async Task RunAsync(RunnerOptions? options)
    {
        await RunAsyncInternal(options ?? _runnerOptions);
    }

    private async Task RunAsyncInternal(RunnerOptions? options)
    {
        // test the connection to the server
        SecureApi.InitializeClient(
                PlatformConfig.APP_SERVER_API_KEY!,
                PlatformConfig.APP_SERVER_URL!
            );
        await SecureApi.Instance.TestConnection();
        // Setup graceful shutdown handling first
        CommandLineHelper.SetupGracefulShutdown();
        
        var uploadResources = _uploadResources.GetValueOrDefault() || (bool.TryParse(Environment.GetEnvironmentVariable("UPLOAD_RESOURCES"), out var flag) && flag);
        await new ResourceUploader(uploadResources).UploadResource();

        // Get the shared cancellation token for coordinated shutdown
        var cancellationToken = CommandLineHelper.GetShutdownToken();

        try
        {
            // IMPORTANT: Upload all flow definitions FIRST before starting any workers
            // This ensures that if any upload fails, we exit before starting workers
            foreach (var flow in _flows)
            {
                await flow.UploadDefinitionAsync(options);
            }

            foreach (var bot in _bots)
            {
                await bot.UploadDefinitionAsync(options);
            }

            // Now start all workers (they will skip the upload since it's already done)
            var tasks = new List<Task>();
            
            foreach (var flow in _flows)
            {
                tasks.Add(flow.RunAsync(options));
            }

            foreach (var bot in _bots)
            {
                tasks.Add(bot.RunAsync(options));
            }

            if (tasks.Count == 0)
            {
                throw new InvalidOperationException("No flows or bots have been added to the agent");
            }

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown
            Console.WriteLine("Agent shutdown requested. All bots are shutting down gracefully...");
        }
        finally
        {
            // Ensure cleanup happens once for all services
            await CommandLineHelper.CleanupResourcesAsync();
        }
    }

#pragma warning disable CS0618 // Type or member is obsolete
    internal AgentInfo GetAgentInfo()
#pragma warning restore CS0618 // Type or member is obsolete
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return new AgentInfo(Name);
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
