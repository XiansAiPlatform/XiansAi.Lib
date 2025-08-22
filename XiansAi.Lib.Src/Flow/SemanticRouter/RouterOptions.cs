
namespace XiansAi.Flow.Router;

/// <summary>
/// Configuration options for the DynamicOrchestrator.
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
    public int HistorySizeToFetch { get; set; } = 20;

    /// <summary>
    /// Gets or sets the welcome message to send when a user sends a null or empty message.
    /// This message will be sent from the agent to initiate the conversation.
    /// </summary>
    public string? WelcomeMessage { get; set; }

    /// <summary>
    /// Gets or sets the HTTP timeout for the AI model.
    /// </summary>
    public int HTTPTimeoutSeconds { get; set; } = 5 * 60; // 5 minutes

}

