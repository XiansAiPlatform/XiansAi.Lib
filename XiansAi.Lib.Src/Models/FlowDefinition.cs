
using System.Reflection;
using System.Text.Json.Serialization;

namespace Agentri.Models;

public class FlowDefinition
{
    [JsonPropertyName("agent")]
    public required string Agent { get; set; }
    [JsonPropertyName("workflowType")]
    public required string WorkflowType { get; set; }
    
    [JsonPropertyName("source")]
    public string? Source { get; set; } = string.Empty;
    [JsonPropertyName("activityDefinitions")]
    public required ActivityDefinition[] ActivityDefinitions { get; set; } = [];
    [JsonPropertyName("parameterDefinitions")]
    public required List<ParameterDefinition> ParameterDefinitions { get; set; } = [];
}

public class ParameterDefinition
{
    public required string? Name { get; set; }
    public required string? Type { get; set; }
}