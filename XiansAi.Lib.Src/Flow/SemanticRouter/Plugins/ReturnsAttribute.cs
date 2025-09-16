namespace Agentri.Flow.Router.Plugins;

/// <summary>
/// Marks a method as a return value for a capability.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ReturnsAttribute : Attribute
{
    /// <summary>
    /// A short description of what the return value is.
    /// </summary>
    public string Description { get; set; }

    public ReturnsAttribute(string description)
    {
        Description = description;
    }
}