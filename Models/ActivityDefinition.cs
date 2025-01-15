namespace XiansAi.Models;

public class ActivityDefinition
{
    public required List<string> AgentNames { get; set; } = [];
    public required List<string> Instructions { get; set; } = [];
    public required string ActivityName { get; set; }
    public required List<ParameterDefinition> Parameters { get; set; } = [];
}