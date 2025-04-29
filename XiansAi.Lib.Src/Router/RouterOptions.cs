
namespace XiansAi.Router;

/// <summary>
/// Configuration options for the DynamicOrchestrator.
/// </summary>
public class RouterOptions
{
    /// <summary>
    /// Gets or sets the model name for the AI model. Default is "gpt-4o".
    /// </summary>
    public string ModelName = "gpt-4o";

    /// <summary>
    /// Gets or sets the temperature for the AI model. Controls randomness in the output.
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Gets or sets the maximum number of tokens to generate in the response.
    /// </summary>
    public int MaxTokens { get; set; } = 1000;


    /// <summary>
    /// Gets or sets the maximum number of tokens to generate in the response.
    /// </summary>
    public int HistorySizeToFetch { get; set; } = 20;

    /// <summary>  
    /// Gets or sets the agent for the router.
    /// </summary>
    public required string Agent { get; set; }

    /// <summary>
    /// Gets or sets the queue name for the router.
    /// </summary>
    public string? QueueName { get; set; }

    /// <summary>
    /// Gets or sets the assignment id for the router.
    /// </summary>
    public string? AssignmentId { get; set; }

    /// <summary>
    /// Gets or sets the workflow id for the router.
    /// </summary>
    public required string WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the workflow type for the router.
    /// </summary>
    public required string WorkflowType { get; set; }

}

