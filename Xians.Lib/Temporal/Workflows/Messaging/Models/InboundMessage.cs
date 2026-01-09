namespace Xians.Lib.Temporal.Workflows.Messaging.Models;

/// <summary>
/// Represents an inbound message signal from the Xians platform.
/// Matches the structure sent by the server.
/// </summary>
public class InboundMessage
{
    public required InboundMessagePayload Payload { get; set; }
    public required string SourceAgent { get; set; }
    public required string SourceWorkflowId { get; set; }
    public required string SourceWorkflowType { get; set; }
}

/// <summary>
/// Payload of an inbound message.
/// Must match MessagePayload structure from XiansAi.Lib.Src/Messenging/Models.cs
/// </summary>
public class InboundMessagePayload
{
    public required string Agent { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public string? Authorization { get; set; }
    public required string Text { get; set; }
    
    private string? _requestId;
    public required string RequestId 
    { 
        get => string.IsNullOrEmpty(_requestId) ? $"generated-{Guid.NewGuid()}" : _requestId;
        set => _requestId = value;
    }
    
    public required string Hint { get; set; }
    public required string Scope { get; set; }
    public required object Data { get; set; }
    public required string Type { get; set; }
    public List<DbMessage>? History { get; set; }
}

/// <summary>
/// Database message structure for conversation history.
/// Matches DbMessage from XiansAi.Lib.Src/Messenging/Models.cs
/// </summary>
public class DbMessage
{
    public string Id { get; set; } = null!;
    public required string ThreadId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public required string Direction { get; set; }
    public string? Text { get; set; }
    public string? Status { get; set; }
    public object? Data { get; set; }
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public string? RequestId { get; set; }
}


