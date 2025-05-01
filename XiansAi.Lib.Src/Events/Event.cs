using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiansAi.Events;


public class EventArgs
{
    public object? Payload { get; set; }

    public required EventOptions EventOptions { get; set; }

    public T CastPayload<T>()
    {
        if (Payload == null)
        {
            throw new InvalidOperationException("Payload is null");
        }

        return JsonSerializer.Deserialize<T>(Payload.ToString()!)!;
    }
}

public class EventOptions
{
    [JsonPropertyName("EventType")]
    public required string EventType { get; set; }

    [JsonPropertyName("SourceWorkflowId")]
    public required string SourceWorkflowId { get; set; }

    [JsonPropertyName("SourceWorkflowType")]
    public required string SourceWorkflowType { get; set; }

    [JsonPropertyName("SourceAgent")]
    public required string SourceAgent { get; set; }

    [JsonPropertyName("SourceQueueName")]
    public string? SourceQueueName { get; set; }

    [JsonPropertyName("SourceAssignment")]
    public string? SourceAssignment { get; set; }

    [JsonPropertyName("Timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("TargetWorkflowId")]
    public string? TargetWorkflowId { get; set; }

    [JsonPropertyName("TargetWorkflowType")]
    public required string TargetWorkflowType { get; set; }

}


public class EventDto : EventOptions
{
    [JsonPropertyName("Payload")]
    public object? Payload { get; set; }
}

