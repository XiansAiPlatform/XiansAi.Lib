namespace Xians.Lib.Agents.A2A;

/// <summary>
/// Response payload for Agent-to-Agent communication.
/// Sent as a Temporal signal back to the requesting workflow.
/// </summary>
public class A2AResponse
{
    /// <summary>
    /// Gets or sets the correlation ID matching the original request.
    /// </summary>
    public required string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the text content of the response.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets optional data payload in the response.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets whether the request was successful.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Gets or sets the error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets optional metadata.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the response was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

