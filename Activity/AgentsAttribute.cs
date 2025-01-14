namespace XiansAi.Activity;
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AgentsAttribute: Attribute
{
    public string[] Names { get; private set; }

    public AgentsAttribute(params string[] names) {
        Names = names;
    }

}
