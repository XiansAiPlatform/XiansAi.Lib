namespace Xians.Lib.Agents.A2A;

/// <summary>
/// Request payload for Agent-to-Agent communication.
/// Sent as a Temporal signal to the target workflow.
/// </summary>
public class A2ARequest
{
    /// <summary>
    /// Gets or sets the unique correlation ID for matching request with response.
    /// Format: a2a:{guid}:{timestamp}
    /// </summary>
    public required string CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the source workflow ID (for sending response back).
    /// </summary>
    public required string SourceWorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the source agent name extracted from the source workflow type.
    /// Cached to avoid repeated string splitting.
    /// </summary>
    private string? _sourceAgentName;
    public string SourceAgentName
    {
        get
        {
            if (_sourceAgentName == null)
            {
                _sourceAgentName = SourceWorkflowType?.Split(':', 2).FirstOrDefault() ?? "unknown";
            }
            return _sourceAgentName;
        }
    }

    /// <summary>
    /// Gets or sets the source workflow type.
    /// </summary>
    public required string SourceWorkflowType { get; set; }

    /// <summary>
    /// Gets or sets the text content of the message.
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Gets or sets optional data payload.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets the tenant ID for tenant isolation.
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Gets or sets optional metadata.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets optional authorization token.
    /// </summary>
    public string? Authorization { get; set; }

    /// <summary>
    /// Gets or sets the participant ID from the original context.
    /// </summary>
    public string? ParticipantId { get; set; }

    /// <summary>
    /// Gets or sets the request ID from the original context.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Gets or sets the scope from the original context.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the hint from the original context.
    /// </summary>
    public string? Hint { get; set; }

    /// <summary>
    /// Gets or sets the thread ID from the original context.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

