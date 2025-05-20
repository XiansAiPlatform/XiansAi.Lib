using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XiansAi.Events;


public class EventArgs
{
    public required EventSignal EventDto { get; set; }

    public T CastPayload<T>()
    {
        if (EventDto.Payload == null)
        {
            throw new InvalidOperationException("Payload is null");
        }

        return JsonSerializer.Deserialize<T>(EventDto.Payload.ToString()!)!;
    }
}

public class EventSignal
{
    [JsonPropertyName("EventType")]
    public required string EventType { get; set; }

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

