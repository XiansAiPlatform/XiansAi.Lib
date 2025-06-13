using System.Text.Json.Serialization;

namespace XiansAi.Messaging;
public class MessageSignal
{
    public required MessagePayload Payload { get; set; }
    public required string SourceAgent { get; set; }
    public required string SourceWorkflowId { get; set; }
    public required string SourceWorkflowType { get; set; }
}

public class MessagePayload
{
    public required string Agent { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public required string Text { get; set; }
    public required object Data { get; set; }
    public required string Token { get; set; }
    public required string Type { get; set; }
}

public enum MessageType
{
    Chat,
    Data,
    Handoff
}

public class ChatOrDataRequest
{
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string Agent { get; set; }
    public object? Data { get; set; }
    public string? Text { get; set; }
    public string? ThreadId { get; set;} 
}

public class HandoffRequest
{
    public string? TargetWorkflowId { get; set; }
    public string? TargetWorkflowType { get; set; }
    public required string SourceAgent { get; set; }
    public required string SourceWorkflowType { get; set; }
    public required string SourceWorkflowId { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public required string Text { get; set; }
    public object? Data { get; set; }
    public MessageType Type { get; set; } = MessageType.Handoff;
}


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
}


public class EventMetadata
{
    public required string SourceWorkflowId { get; set; }
    public required string SourceWorkflowType { get; set; }
    public required string SourceAgent { get; set; }
}

public class EventMetadata<T> : EventMetadata
{
    public required T Payload { get; set; }
}


public class EventSignal
{
    [JsonPropertyName("Payload")]
    public object? Payload { get; set; }

    [JsonPropertyName("SourceWorkflowId")]
    public required string SourceWorkflowId { get; set; }

    [JsonPropertyName("SourceWorkflowType")]
    public required string SourceWorkflowType { get; set; }

    [JsonPropertyName("SourceAgent")]
    public required string SourceAgent { get; set; }

    [JsonPropertyName("TargetWorkflowId")]
    public string? TargetWorkflowId { get; set; }

    [JsonPropertyName("TargetWorkflowType")]
    public required string TargetWorkflowType { get; set; }

    [JsonPropertyName("Timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
