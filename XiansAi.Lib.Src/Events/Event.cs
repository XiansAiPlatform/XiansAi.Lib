namespace XiansAi.Events;

public class BaseEvent 
{
    
    public required string EventType { get; set; }
    public required string SourceWorkflowId { get; set; }
    public string? SourceWorkflowType { get; set; }
    public string? SourceAgent { get; set; }
    public string? SourceQueueName { get; set; }
    public string? SourceAssignment { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public object? Payload { get; set; }
}

public class Event : BaseEvent
{
    public required string TargetWorkflowId { get; set; }
}

public class StartAndSendEvent : BaseEvent
{
    public required string TargetWorkflowType { get; set; }
}
