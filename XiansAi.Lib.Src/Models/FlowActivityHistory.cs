using System.Text.Json.Serialization;

namespace XiansAi.Models;

public class FlowActivityHistory
{
    [JsonPropertyName("activityId")]
    public required string ActivityId { get; set; }

    [JsonPropertyName("activityName")]
    public required string ActivityName { get; set; }

    [JsonPropertyName("startedTime")]
    public required DateTime StartedTime { get; set; }

    [JsonPropertyName("endedTime")]
    public DateTime? EndedTime { get; set; }

    [JsonPropertyName("inputs")]
    public Dictionary<string, object?> Inputs { get; set; } = [];

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("workflowId")]
    public required string WorkflowId { get; set; }

    [JsonPropertyName("workflowRunId")]
    public required string WorkflowRunId { get; set; }

    [JsonPropertyName("workflowType")]
    public required string WorkflowType { get; set; }

    [JsonPropertyName("taskQueue")]
    public required string TaskQueue { get; set; }

    [JsonPropertyName("agentToolNames")]
    public List<string> AgentToolNames { get; set; } = [];

    [JsonPropertyName("instructionIds")]
    public List<string> InstructionIds { get; set; } = [];

    [JsonPropertyName("attempt")]
    public int Attempt { get; set; }

    [JsonPropertyName("workflowNamespace")]
    public required string WorkflowNamespace { get; set; }
} 