
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AgentAttribute: Attribute
{
    public string Name { get; private set; }
    public string[] Instructions { get; private set; } = [];

    public AgentAttribute(string name, params string[] instructions) {
        Name = name;
        Instructions = instructions;
    }

}
