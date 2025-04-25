namespace XiansAi.Events;

public class BaseEvent 
{
    public required string eventType { get; set; }
    public required string sourceWorkflowId { get; set; }
    public string? sourceWorkflowType { get; set; }
    public string? sourceAgent { get; set; }
    public string? sourceQueueName { get; set; }
    public string? sourceAssignment { get; set; }
    public DateTimeOffset timestamp { get; set; } = DateTimeOffset.UtcNow;
    public object? payload { get; set; }
}

public class Event : BaseEvent
{
    public required string targetWorkflowId { get; set; }
}

public class StartAndSendEvent : BaseEvent
{
    public required string targetWorkflowType { get; set; }
}
