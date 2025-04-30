namespace XiansAi.Models;

public class ActivityDefinition
{
    public required List<string> AgentToolNames { get; set; } = [];
    public required List<string> KnowledgeIds { get; set; } = [];
    public required string ActivityName { get; set; }
    public required List<ParameterDefinition> ParameterDefinitions { get; set; } = [];
}