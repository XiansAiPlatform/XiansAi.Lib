namespace XiansAi.Activity;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
public sealed class KnowledgeAttribute: Attribute
{
    public string[] Knowledge { get; private set; } = [];

    public KnowledgeAttribute(params string[] knowledge) {
        if (knowledge.Length == 0)
        {
            throw new ArgumentException("At least one knowledge key is required for KnowledgeAttribute.");
        }
        Knowledge = knowledge;
    }

}
