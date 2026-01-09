namespace Xians.Lib.Agents.Core.Registry;

/// <summary>
/// Interface for managing agent registration and retrieval.
/// Enables testability and dependency injection.
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// Registers an agent in the registry.
    /// </summary>
    /// <param name="agent">The agent to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when agent is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an agent with the same name already exists.</exception>
    void Register(XiansAgent agent);

    /// <summary>
    /// Gets a registered agent by name.
    /// </summary>
    /// <param name="agentName">The name of the agent to retrieve.</param>
    /// <returns>The agent instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when agentName is null or empty.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the agent is not found.</exception>
    XiansAgent Get(string agentName);

    /// <summary>
    /// Tries to get a registered agent by name.
    /// </summary>
    /// <param name="agentName">The name of the agent to retrieve.</param>
    /// <param name="agent">The agent instance if found, null otherwise.</param>
    /// <returns>True if the agent was found, false otherwise.</returns>
    bool TryGet(string agentName, out XiansAgent? agent);

    /// <summary>
    /// Gets all registered agents.
    /// </summary>
    /// <returns>Enumerable of all registered agent instances.</returns>
    IEnumerable<XiansAgent> GetAll();

    /// <summary>
    /// Clears all registered agents.
    /// For testing purposes only.
    /// </summary>
    void Clear();
}
