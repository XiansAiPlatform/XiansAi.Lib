namespace XiansAi.Activity;


[AttributeUsage(AttributeTargets.Interface, Inherited = false)]
public sealed class AgentToolAttribute : Attribute
{
    public string Name { get; private set; }
    public AgentToolType Type { get; private set; }
    public AgentToolAttribute(string name, AgentToolType type = AgentToolType.Custom)
    {
        Name = name;
        Type = type;
    }

    public override string ToString()
    {
        return $"{Name} [{Type}]";
    }

}

public enum AgentToolType
{
    Package,
    Docker,
    Custom
}