namespace Xians.Lib.Agents.Core;

/// <summary>
/// Registration information for creating a new agent.
/// </summary>
public class XiansAgentRegistration
{
    /// <summary>
    /// Gets or sets the name of the agent to register.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether the agent is a template.
    /// Template agents can be used as templates for creating new agent instances.
    /// </summary>
    public bool IsTemplate { get; set; }

    /// <summary>
    /// Gets or sets whether the agent is system-scoped.
    /// System-scoped agents are shared across all users.
    /// </summary>
    [Obsolete("Use IsTemplate property instead. This property will be removed in a future version.")]
    public bool SystemScoped 
    { 
        get => IsTemplate; 
        set => IsTemplate = value; 
    }

    /// <summary>
    /// Gets or sets the description of the agent.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the summary of the agent.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets the version of the agent.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the author of the agent.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets whether task workflows should be automatically enabled for this agent.
    /// When true, the agent will automatically configure task workflow support during registration.
    /// When null, inherits from the global platform setting.
    /// </summary>
    public bool? EnableTasks { get; set; }
}

