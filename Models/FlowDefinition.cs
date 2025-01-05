
using System.Reflection;

namespace XiansAi.Models;

public class FlowDefinition   
{
    public required string TypeName { get; set; }
    public required ActivityDefinition[] Activities { get; set; }
    public required string ClassName { get; set; }
    public required List<ParameterDefinition> Parameters { get; set; } = [];
    public string? Hash { get; set; }
    public string? Source { get; set; }
    public string? Markdown { get; set; }
}

public class ParameterDefinition
{
    public required string? Name { get; set; }
    public required string? Type { get; set; }
}