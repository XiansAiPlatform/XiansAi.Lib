using System.Reflection;

namespace XiansAi.Models;

public class ActivityDefinition
{
    public required string? DockerImage { get; set; }
    public required List<string> Instructions { get; set; } = [];
    public required string ActivityName { get; set; }
    public required List<ParameterDefinition> Parameters { get; set; } = [];
}