namespace XiansAi.Flow.Router.Plugins;

/// <summary>
/// Marks a method as a capability so that it will be registered automatically.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class CapabilityAttribute : Attribute
{
    /// <summary>
    /// A short description of what the capability does.
    /// </summary>
    public string Description { get; set; }

    public CapabilityAttribute(string description)
    {
        Description = description;
    }
}