using System.Net;
using Xians.Lib.Temporal;
using Xians.Lib.Agents.Knowledge;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Agents.Documents;
using Xians.Lib.Agents.Tasks;

namespace Xians.Lib.Agents.Core;

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
    /// Gets the knowledge collection for managing agent knowledge.
    /// </summary>
    public KnowledgeCollection Knowledge { get; private set; }

    /// <summary>
    /// Gets the documents collection for managing agent documents.
    /// Documents are scoped to the agent and tenant.
    /// </summary>
    public DocumentCollection Documents { get; private set; }

    /// <summary>
    /// Gets the task collection for creating and managing human-in-the-loop tasks.
    /// </summary>
    public TaskCollection Tasks { get; private set; }

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
    /// Gets the summary of the agent.
    /// </summary>
    public string? Summary { get; private set; }

    /// <summary>
    /// Gets the author of the agent.
    /// </summary>
    public string? Author { get; private set; }

    /// <summary>
    /// Gets whether the agent is system-scoped.
    /// </summary>
    public bool SystemScoped { get; private set; }

    internal ITemporalClientService? TemporalService { get; private set; }
    internal Http.IHttpClientService? HttpService { get; private set; }
    internal XiansOptions? Options { get; private set; }

    internal Xians.Lib.Common.Caching.CacheService? CacheService { get; private set; }

    internal XiansAgent(string name, bool systemScoped, string? description, string? summary, string? version, string? author,
        WorkflowDefinitionUploader? uploader, ITemporalClientService? temporalService, 
        Http.IHttpClientService? httpService, XiansOptions? options, 
        Xians.Lib.Common.Caching.CacheService? cacheService)
    {
        Name = name.Trim(); // Trim to handle whitespace variations

        if (Name.Contains(':'))
        {
            throw new ArgumentException("Agent name cannot contain ':' character as it is used as a delimiter in workflow identifiers.", nameof(name));
        }
        
        SystemScoped = systemScoped;
        Description = description;
        Summary = summary;
        Version = version;
        Author = author;
        TemporalService = temporalService;
        HttpService = httpService;
        Options = options;
        CacheService = cacheService;
        Workflows = new WorkflowCollection(this, uploader);
        Knowledge = new KnowledgeCollection(this, httpService, cacheService);
        Documents = new DocumentCollection(this, httpService);
        Tasks = new TaskCollection(this);
        
        // Register this agent in the static context
        XiansContext.RegisterAgent(this);
    }

    /// <summary>
    /// Gets a built-in workflow by name.
    /// If name is null, returns the unnamed built-in workflow.
    /// </summary>
    /// <param name="name">The name of the built-in workflow, or null for the unnamed workflow.</param>
    /// <returns>The built-in workflow or null if not found.</returns>
    public XiansWorkflow? GetBuiltInWorkflow(string? name = null) => Workflows.GetBuiltIn(name);

    /// <summary>
    /// Gets a custom workflow by its type.
    /// </summary>
    /// <typeparam name="T">The custom workflow type.</typeparam>
    /// <returns>The custom workflow or null if not found.</returns>
    public XiansWorkflow? GetCustomWorkflow<T>() where T : class => Workflows.GetCustom<T>();

    /// <summary>
    /// Gets all workflows for this agent.
    /// </summary>
    /// <returns>A read-only list of workflows.</returns>
    public IReadOnlyList<XiansWorkflow> GetAllWorkflows() => Workflows.GetAll();

    /// <summary>
    /// Uploads all workflow definitions to the server without running the workflows.
    /// This is useful when you want to register the agent with the server before running workflows.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UploadWorkflowDefinitionsAsync()
    {
        await Workflows.UploadAllDefinitionsAsync();
    }

    /// <summary>
    /// Deploys a system-scoped template agent to the current tenant.
    /// Creates replicas of the agent, its flow definitions, and associated knowledge.
    /// This method is primarily used in tests to deploy system-scoped agents before use.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when HTTP service is not available or deployment fails.</exception>
    public async Task DeployAsync()
    {
        if (HttpService == null)
        {
            throw new InvalidOperationException("HTTP service is not available. Cannot deploy agent.");
        }

        var client = await HttpService.GetHealthyClientAsync();
        var deployRequest = new { agentName = Name };
        
        var response = await client.PostAsync(
            $"{Common.WorkflowConstants.ApiEndpoints.AgentDefinitions}/deploy-template",
            System.Net.Http.Json.JsonContent.Create(deployRequest));

        // Treat 409 Conflict as idempotent success to avoid redeploy failures
        // when the template agent already exists in the tenant.
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to deploy agent '{Name}'. Status: {response.StatusCode}, Error: {errorMessage}");
        }
    }

    /// <summary>
    /// Deletes the agent and all associated resources (definitions, knowledge, schedules, documents, logs, etc.).
    /// Requires tenant admin or system admin role.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when HTTP service is not available or deletion fails.</exception>
    public async Task DeleteAsync()
    {
        if (HttpService == null)
        {
            throw new InvalidOperationException("HTTP service is not available. Cannot delete agent.");
        }

        var client = await HttpService.GetHealthyClientAsync();
        var deleteUrl = $"{Common.WorkflowConstants.ApiEndpoints.AgentDefinitions}/agent?agentName={Uri.EscapeDataString(Name)}&systemScoped={SystemScoped}";
        
        var response = await client.DeleteAsync(deleteUrl);

        // Treat not-found responses as idempotent success since the desired
        // post-condition (agent absent) is already satisfied.
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (body.Contains("Agent not found", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to delete agent '{Name}'. Status: {response.StatusCode}, Error: {errorMessage}");
        }
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
