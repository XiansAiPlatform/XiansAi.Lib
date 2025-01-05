namespace XiansAi.Models;

public class ActivityDefinition
{
    public required string DockerImage { get; set; }
    public required string[] Instructions { get; set; }
    public required string ActivityName { get; set; }
    public required string ClassName {get; set;}
}