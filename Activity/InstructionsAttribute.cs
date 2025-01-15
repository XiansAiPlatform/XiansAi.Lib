namespace XiansAi.Activity;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class InstructionsAttribute: Attribute
{
    public string[] Instructions { get; private set; } = [];

    public InstructionsAttribute(params string[] instructions) {
        Instructions = instructions;
    }

}
