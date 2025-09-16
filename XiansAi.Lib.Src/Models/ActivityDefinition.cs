namespace Agentri.Models;
using System.Text.Json.Serialization;

public class ActivityDefinition
{
    [JsonPropertyName("activityName")]
    public required string ActivityName { get; set; }
    [JsonPropertyName("agentToolNames")]
    public required List<string> AgentToolNames { get; set; } = [];
    [JsonPropertyName("knowledgeIds")]
    public required List<string> KnowledgeIds { get; set; } = [];
    [JsonPropertyName("parameterDefinitions")]
    public required List<ParameterDefinition> ParameterDefinitions { get; set; } = [];
}