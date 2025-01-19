namespace XiansAi.Activity;


[AttributeUsage(AttributeTargets.Interface, Inherited = false)]
public sealed class AgentAttribute : Attribute
{
    public string Name { get; private set; }
    public AgentType Type { get; private set; }
    public AgentAttribute(string name, AgentType type = AgentType.Custom)
    {
        Name = name;
        Type = type;
    }

    public override string ToString()
    {
        return $"{Name} [{Type}]";
    }

}

public enum AgentType
{
    Package,
    Docker,
    Custom
}