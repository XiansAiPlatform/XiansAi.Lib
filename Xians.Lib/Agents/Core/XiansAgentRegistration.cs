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
    /// Gets or sets whether the agent is system-scoped.
    /// System-scoped agents are shared across all users.
    /// </summary>
    public bool SystemScoped { get; set; } = false;

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
}

