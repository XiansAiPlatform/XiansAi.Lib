namespace XiansAi.Flow.Router.Orchestration.SemanticKernel;

/// <summary>
/// Configuration for Semantic Kernel orchestrator supporting OpenAI and Azure OpenAI providers
/// </summary>
public class SemanticKernelConfig : OrchestratorConfig
{
    public SemanticKernelConfig()
    {
        OrchestratorType = OrchestratorType.SemanticKernel;
    }

    /// <summary>
    /// Provider name (openai or azureopenai)
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// API key for authentication
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model name (for OpenAI provider, e.g., gpt-4, gpt-3.5-turbo)
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Deployment name (for Azure OpenAI provider)
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Endpoint URL (for Azure OpenAI provider)
    /// </summary>
    public string? Endpoint { get; set; }
}

