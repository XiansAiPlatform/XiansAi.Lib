namespace Xians.Lib.Agents;

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
}

