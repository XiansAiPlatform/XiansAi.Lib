
namespace XiansAi.Flow.Router.Plugins;

/// <summary>
/// Marks a parameter with a name and description for use in kernel functions.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class ParameterAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the parameter.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description of the parameter.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterAttribute"/> class.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="description">The description of the parameter.</param>
    public ParameterAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
} 