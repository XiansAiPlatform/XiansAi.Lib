namespace XiansAi.Activity;
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DockerAgentsAttribute: Attribute
{
    public string[] Names { get; private set; }

    public DockerAgentsAttribute(params string[] names) {
        Names = names;
    }

}
