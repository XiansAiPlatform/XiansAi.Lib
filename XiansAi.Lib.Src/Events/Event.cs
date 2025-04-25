using System;
using System.Text.Json.Serialization;

namespace XiansAi.Events;

public class BaseEvent
{
    [JsonPropertyName("EventType")]
    public required string EventType { get; set; }

    [JsonPropertyName("SourceWorkflowId")]
    public required string SourceWorkflowId { get; set; }

    [JsonPropertyName("SourceWorkflowType")]
    public string? SourceWorkflowType { get; set; }

    [JsonPropertyName("SourceAgent")]
    public string? SourceAgent { get; set; }

    [JsonPropertyName("SourceQueueName")]
    public string? SourceQueueName { get; set; }

    [JsonPropertyName("SourceAssignment")]
    public string? SourceAssignment { get; set; }

    [JsonPropertyName("Timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("Payload")]
    public object? Payload { get; set; }
}

public class Event : BaseEvent
{
    [JsonPropertyName("TargetWorkflowId")]
    public required string TargetWorkflowId { get; set; }
}

public class StartAndSendEvent : BaseEvent
{
    [JsonPropertyName("TargetWorkflowType")]
    public required string TargetWorkflowType { get; set; }
}
