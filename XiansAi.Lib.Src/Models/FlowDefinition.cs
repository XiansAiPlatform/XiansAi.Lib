
using System.Reflection;

namespace XiansAi.Models;

public class FlowDefinition
{
    public required string Agent { get; set; }
    public required string WorkflowType { get; set; }
    public required ActivityDefinition[] ActivityDefinitions { get; set; } = [];
    public required List<ParameterDefinition> ParameterDefinitions { get; set; } = [];
    public string? Source { get; set; } = string.Empty;
}

public class ParameterDefinition
{
    public required string? Name { get; set; }
    public required string? Type { get; set; }
}