using System.Collections.Concurrent;

namespace Xians.Lib.Agents.Core.Registry;

/// <summary>
/// Thread-safe registry for managing XiansAgent instances.
/// Extracted from XiansContext for better separation of concerns.
/// </summary>
internal class AgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, XiansAgent> _agents = new();

    /// <inheritdoc/>
    public void Register(XiansAgent agent)
    {
        if (agent == null)
        {
            throw new ArgumentNullException(nameof(agent));
        }

        if (!_agents.TryAdd(agent.Name, agent))
        {
            throw new InvalidOperationException(
                $"Agent '{agent.Name}' is already registered. Each agent must have a unique name.");
        }
    }

    /// <inheritdoc/>
    public XiansAgent Get(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentNullException(nameof(agentName), "Agent name cannot be null or empty.");
        }

        if (_agents.TryGetValue(agentName, out var agent))
        {
            return agent;
        }

        throw new KeyNotFoundException(
            $"Agent '{agentName}' not found. Available agents: {string.Join(", ", _agents.Keys)}");
    }

    /// <inheritdoc/>
    public bool TryGet(string agentName, out XiansAgent? agent)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            agent = null;
            return false;
        }

        return _agents.TryGetValue(agentName, out agent);
    }

    /// <inheritdoc/>
    public IEnumerable<XiansAgent> GetAll()
    {
        return _agents.Values;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _agents.Clear();
    }
}
