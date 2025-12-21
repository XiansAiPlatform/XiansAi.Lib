using Xians.Lib.Temporal;

namespace Xians.Lib.Agents;

/// <summary>
/// Represents a registered agent in the Xians platform.
/// </summary>
public class XiansAgent
{
    /// <summary>
    /// Gets the workflows collection for managing agent workflows.
    /// </summary>
    public WorkflowCollection Workflows { get; private set; }

    /// <summary>
    /// Gets the name of the agent.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the version of the agent.
    /// </summary>
    public string? Version { get; private set; }

    /// <summary>
    /// Gets the description of the agent.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets whether the agent is system-scoped.
    /// </summary>
    public bool SystemScoped { get; private set; }

    internal ITemporalClientService? TemporalService { get; private set; }
    internal Http.IHttpClientService? HttpService { get; private set; }
    internal XiansOptions? Options { get; private set; }

    internal XiansAgent(string name, bool systemScoped, WorkflowDefinitionUploader? uploader, 
        ITemporalClientService? temporalService, Http.IHttpClientService? httpService, XiansOptions? options)
    {
        Name = name;
        SystemScoped = systemScoped;
        TemporalService = temporalService;
        HttpService = httpService;
        Options = options;
        Workflows = new WorkflowCollection(this, uploader);
    }

    /// <summary>
    /// Runs all registered workflows for this agent asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAllAsync(CancellationToken cancellationToken = default)
    {
        // Set up cancellation token if not provided
        if (cancellationToken == default)
        {
            var tokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                tokenSource.Cancel();
                eventArgs.Cancel = true;
            };
            cancellationToken = tokenSource.Token;
        }

        await Workflows.RunAllAsync(cancellationToken);
    }
}

