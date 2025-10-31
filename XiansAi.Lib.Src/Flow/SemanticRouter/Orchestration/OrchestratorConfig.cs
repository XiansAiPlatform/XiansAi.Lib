namespace XiansAi.Flow.Router.Orchestration;

/// <summary>
/// Base configuration for AI orchestrators.
/// Derived classes add provider-specific settings.
/// </summary>
public abstract class OrchestratorConfig
{
    /// <summary>
    /// The type of orchestrator to use
    /// </summary>
    public required OrchestratorType OrchestratorType { get; init; }

    /// <summary>
    /// Temperature for AI model. Controls randomness in the output (0.0 = deterministic, 2.0 = very random)
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Maximum number of tokens to generate in the response
    /// </summary>
    public int MaxTokens { get; set; } = 10000;

    /// <summary>
    /// Number of historical messages to fetch for context
    /// </summary>
    public int HistorySizeToFetch { get; set; } = 10;

    /// <summary>
    /// Welcome message to send when user sends null/empty message
    /// </summary>
    public string? WelcomeMessage { get; set; }

    /// <summary>
    /// HTTP timeout in seconds for API calls
    /// </summary>
    public int HTTPTimeoutSeconds { get; set; } = 5 * 60; // 5 minutes

    /// <summary>
    /// Maximum token limit for chat history before reduction is triggered
    /// </summary>
    public int TokenLimit { get; set; } = 80000;

    /// <summary>
    /// Target token count to reduce to when limit is exceeded
    /// </summary>
    public int TargetTokenCount { get; set; } = 50000;

    /// <summary>
    /// Maximum tokens allowed for a single function result
    /// </summary>
    public int MaxTokensPerFunctionResult { get; set; } = 10000;

    /// <summary>
    /// Maximum consecutive function calls before terminating
    /// </summary>
    public int MaxConsecutiveCalls { get; set; } = 10;
}

/// <summary>
/// Enum defining supported orchestrator types
/// </summary>
public enum OrchestratorType
{
    /// <summary>
    /// Microsoft Semantic Kernel with OpenAI or Azure OpenAI
    /// </summary>
    SemanticKernel,

    /// <summary>
    /// AWS Bedrock Agent Runtime
    /// </summary>
    AWSBedrock,

    /// <summary>
    /// Azure AI Foundry Persistent Agents
    /// </summary>
    AzureAIFoundry
}


