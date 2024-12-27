
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AgentAttribute: Attribute
{
    public string Name { get; private set; }
    public string? Settings { get; private set; } = null;

    public AgentAttribute(string name, string? settings = null) {
        Name = name;
        Settings = settings;
    }

}
