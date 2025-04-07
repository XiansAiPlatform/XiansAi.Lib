namespace XiansAi.Activity;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class KnowledgeAttribute: Attribute
{
    public string[] Knowledge { get; private set; } = [];

    public KnowledgeAttribute(params string[] knowledge) {
        Knowledge = knowledge;
    }

}
