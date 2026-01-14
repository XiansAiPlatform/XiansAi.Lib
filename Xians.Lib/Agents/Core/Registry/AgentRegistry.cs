using System.Collections.Concurrent;
using System.Linq;

namespace Xians.Lib.Agents.Core.Registry;

/// <summary>
/// Thread-safe registry for managing XiansAgent instances.
/// Extracted from XiansContext for better separation of concerns.
/// </summary>
internal class AgentRegistry : IAgentRegistry
{
    private sealed record AgentEntry(XiansAgent? SystemAgent, XiansAgent? TenantAgent)
    {
        public AgentEntry WithSystemAgent(XiansAgent agent) => new(agent, TenantAgent);
        public AgentEntry WithTenantAgent(XiansAgent agent) => new(SystemAgent, agent);
    }

    private readonly ConcurrentDictionary<string, AgentEntry> _agents = new();

    /// <inheritdoc/>
    public void Register(XiansAgent agent)
    {
        if (agent == null)
        {
            throw new ArgumentNullException(nameof(agent));
        }

        // Allow idempotent registration of the same agent and simultaneous
        // registration of system-scoped and tenant-scoped variants that share
        // the same name.
        _agents.AddOrUpdate(
            agent.Name,
            _ => CreateEntry(agent),
            (_, existing) => UpdateEntry(existing, agent));
    }

    /// <inheritdoc/>
    public XiansAgent Get(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentNullException(nameof(agentName), "Agent name cannot be null or empty.");
        }

        if (_agents.TryGetValue(agentName, out var entry))
        {
            var agent = entry.TenantAgent ?? entry.SystemAgent;
            if (agent != null)
            {
                return agent;
            }
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

        var found = _agents.TryGetValue(agentName, out var entry);
        agent = entry?.TenantAgent ?? entry?.SystemAgent;
        return found && agent != null;
    }

    /// <inheritdoc/>
    public IEnumerable<XiansAgent> GetAll()
    {
        return _agents.Values
            .SelectMany(entry => new[] { entry.SystemAgent, entry.TenantAgent })
            .OfType<XiansAgent>();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _agents.Clear();
    }

    private static AgentEntry CreateEntry(XiansAgent agent)
    {
        return agent.SystemScoped
            ? new AgentEntry(agent, null)
            : new AgentEntry(null, agent);
    }

    private static AgentEntry UpdateEntry(AgentEntry existing, XiansAgent agent)
    {
        if (agent.SystemScoped)
        {
            // Idempotent for the same system-scoped agent name.
            if (existing.SystemAgent != null && ReferenceEquals(existing.SystemAgent, agent))
            {
                return existing;
            }

            return existing.WithSystemAgent(agent);
        }

        // Tenant-scoped registration is idempotent as well.
        if (existing.TenantAgent != null && ReferenceEquals(existing.TenantAgent, agent))
        {
            return existing;
        }

        return existing.WithTenantAgent(agent);
    }
}
