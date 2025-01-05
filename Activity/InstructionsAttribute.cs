namespace XiansAi.Activity;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class InstructionsAttribute: Attribute
{
    public string[] Instructions { get; private set; } = [];

    public InstructionsAttribute(params string[] instructions) {
        Instructions = instructions;
    }

}
