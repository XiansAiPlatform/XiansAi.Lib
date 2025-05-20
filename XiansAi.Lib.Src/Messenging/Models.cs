namespace XiansAi.Messaging;
public class MessageSignal
{
    public required MessagePayload Payload { get; set; }
    public required string TargetWorkflowId { get; set; }
    public required string TargetWorkflowType { get; set; }
    public required string SourceAgent { get; set; }
}

public class MessagePayload
{
    public required string Agent { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public required string Content { get; set; }
    public required object Metadata { get; set; }
}

public class OutgoingMessage
{
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string Agent { get; set; }
    public string? QueueName { get; set; }
    public required string Content { get; set; }
    public object? Metadata { get; set; }
    public string? ThreadId { get; set; }
}


public class HandoverMessage 
{    
    public string? WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
    public required string Agent { get; set; }
    public required string ThreadId { get; set; }
    public required string ParticipantId { get; set; }
    public required string FromWorkflowType { get; set; }
    public string? UserRequest { get; set; }
}


public class HistoricalMessage
{
    public string Id { get; set; } = null!;
    public required string ThreadId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public required string Direction { get; set; }
    public required string Content { get; set; }
    public string? Status { get; set; }
    public object? Metadata { get; set; }
    public required string ParticipantId { get; set; }
    public required string WorkflowId { get; set; }
    public required string WorkflowType { get; set; }
}
