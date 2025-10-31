namespace XiansAi.Flow.Router.Orchestration.AzureAIFoundry;

/// <summary>
/// Configuration for Azure AI Foundry Persistent Agents orchestrator
/// </summary>
public class AzureAIFoundryConfig : OrchestratorConfig
{
    public AzureAIFoundryConfig()
    {
        OrchestratorType = OrchestratorType.AzureAIFoundry;
    }

    /// <summary>
    /// Azure AI project endpoint URL
    /// </summary>
    public required string ProjectEndpoint { get; init; }

    /// <summary>
    /// Model deployment name
    /// </summary>
    public required string ModelDeploymentName { get; init; }

    /// <summary>
    /// Optional Azure AI Search connection ID for RAG capabilities
    /// </summary>
    public string? AzureAISearchConnectionId { get; init; }

    /// <summary>
    /// Optional search index name for RAG
    /// </summary>
    public string? SearchIndexName { get; init; }

    /// <summary>
    /// Optional filter for search results
    /// </summary>
    public string? SearchFilter { get; init; }

    /// <summary>
    /// Number of top results to retrieve from search (default: 5)
    /// </summary>
    public int SearchTopK { get; set; } = 5;

    /// <summary>
    /// Azure AI Search query type
    /// </summary>
    public string SearchQueryType { get; set; } = "Simple";

    /// <summary>
    /// Optional agent instructions override
    /// </summary>
    public string? AgentInstructions { get; init; }

    /// <summary>
    /// Optional agent name override
    /// </summary>
    public string? AgentName { get; init; }
}

