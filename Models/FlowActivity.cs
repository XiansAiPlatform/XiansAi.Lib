using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace XiansAi.Models;

public class FlowActivity
{
    [BsonId]
    [JsonPropertyName("activityId")]
    public required string ActivityId { get; set; }

    [BsonElement("activityName")]
    [JsonPropertyName("activityName")]
    public required string ActivityName { get; set; }

    [BsonElement("startedTime")]
    [JsonPropertyName("startedTime")]
    public required DateTime StartedTime { get; set; }

    [BsonElement("endedTime")]
    [JsonPropertyName("endedTime")]
    public DateTime? EndedTime { get; set; }

    [BsonElement("inputs")]
    [JsonPropertyName("inputs")]
    [BsonSerializer(typeof(MongoDB.Bson.Serialization.Serializers.DictionaryInterfaceImplementerSerializer<Dictionary<string, object>>))]
    public Dictionary<string, object?> Inputs { get; set; } = [];

    [BsonElement("result")]
    [JsonPropertyName("result")]
    [BsonSerializer(typeof(MongoDB.Bson.Serialization.Serializers.ObjectSerializer))]
    public object? Result { get; set; }

    [BsonElement("workflowId")]
    [JsonPropertyName("workflowId")]
    public required string WorkflowId { get; set; }

    [BsonElement("workflowType")]
    [JsonPropertyName("workflowType")]
    public required string WorkflowType { get; set; }

    [BsonElement("taskQueue")]
    [JsonPropertyName("taskQueue")]
    public required string TaskQueue { get; set; }

    [BsonElement("agentNames")]
    [JsonPropertyName("agentNames")]
    public List<string> AgentNames { get; set; } = [];

    [BsonElement("instructionIds")]
    [JsonPropertyName("instructionIds")]
    public List<string> InstructionIds { get; set; } = [];
} 