using System.Text.Json.Serialization;

namespace Xians.Lib.Agents.Workflows.Models;

/// <summary>
/// Represents a workflow definition to be registered with the server.
/// </summary>
public class WorkflowDefinition
{
    [JsonPropertyName("agent")]
    public required string Agent { get; set; }
    
    [JsonPropertyName("workflowType")]
    public required string WorkflowType { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
    
    [JsonPropertyName("source")]
    public string? Source { get; set; } = string.Empty;
    
    [JsonPropertyName("activityDefinitions")]
    public ActivityDefinition[] ActivityDefinitions { get; set; } = [];
    
    [JsonPropertyName("parameterDefinitions")]
    public List<ParameterDefinition> ParameterDefinitions { get; set; } = [];
    
    [JsonPropertyName("systemScoped")]
    public bool SystemScoped { get; set; } = false;
    
    [JsonPropertyName("workers")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Always)]
    public int Workers { get; set; } = 1;
    
    [JsonPropertyName("activable")]
    public bool Activable { get; set; } = true;
}

public class ParameterDefinition
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("optional")]
    public bool Optional { get; set; }
}

public class ActivityDefinition
{
    [JsonPropertyName("activityName")]
    public required string ActivityName { get; set; }
    
    [JsonPropertyName("agentToolNames")]
    public List<string> AgentToolNames { get; set; } = [];
    
    [JsonPropertyName("knowledgeIds")]
    public List<string> KnowledgeIds { get; set; } = [];
    
    [JsonPropertyName("parameterDefinitions")]
    public List<ParameterDefinition> ParameterDefinitions { get; set; } = [];
}

