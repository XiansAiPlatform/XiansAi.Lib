
namespace Agentri.Flow.Router;

/// <summary>
/// Configuration options for the DynamicOrchestrator.
/// 
/// Example usage with token limiting:
/// <code>
/// var options = new RouterOptions
/// {
///     // Basic configuration
///     ProviderName = "openai",
///     ModelName = "gpt-4",
///     ApiKey = "your-api-key",
///     
///     // Token limiting (prevents context_length_exceeded errors)
///     TokenLimit = 80000,                    // Trigger reduction at 80k tokens
///     TargetTokenCount = 50000,              // Reduce to 50k tokens
///     MaxTokensPerFunctionResult = 5000,     // Limit large function results
///     
///     // Other settings
///     HistorySizeToFetch = 50,               // Can fetch more; reducer handles limits
///     Temperature = 0.3
/// };
/// </code>
/// </summary>
public class RouterOptions
{
    /// <summary>
    /// Gets or sets the deployment name for the AI model.
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the endpoint for the AI model.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the API key for the AI model.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the provider name for the AI model.
    /// </summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the model name for the AI model.
    /// </summary>
    public string? ModelName;

    /// <summary>
    /// Gets or sets the temperature for the AI model. Controls randomness in the output.
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Gets or sets the maximum number of tokens to generate in the response.
    /// </summary>
    public int MaxTokens { get; set; } = 10000;


    /// <summary>
    /// Gets or sets the maximum number of tokens to generate in the response.
    /// </summary>
    public int HistorySizeToFetch { get; set; } = 10;

    /// <summary>
    /// Gets or sets the welcome message to send when a user sends a null or empty message.
    /// This message will be sent from the agent to initiate the conversation.
    /// </summary>
    public string? WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets the HTTP timeout for the AI model.
    /// </summary>
    public int HTTPTimeoutSeconds { get; set; } = 5 * 60; // 5 minutes

    /// <summary>
    /// Gets or sets the maximum token limit for chat history.
    /// When chat history exceeds this limit, it will be reduced using the configured reduction strategy.
    /// Default is 80000 to leave ample room for system prompts, function calls, and responses (for 128k context models).
    /// Set to 0 to disable token limiting.
    /// </summary>
    public int TokenLimit { get; set; } = 80000;

    /// <summary>
    /// Gets or sets the target token count to reduce to when token limit is exceeded.
    /// This should be significantly lower than TokenLimit to avoid frequent reductions.
    /// Default is 50000 tokens.
    /// </summary>
    public int TargetTokenCount { get; set; } = 50000;

    /// <summary>
    /// Gets or sets the maximum tokens allowed for a single function result.
    /// Very large function results (like web scraping) will be truncated to this limit.
    /// Default is 5000 tokens (~20,000 characters) to handle large scraped content.
    /// </summary>
    public int MaxTokensPerFunctionResult { get; set; } = 10000;
}

