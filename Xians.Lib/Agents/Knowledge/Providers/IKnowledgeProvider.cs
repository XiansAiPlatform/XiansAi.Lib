namespace Xians.Lib.Agents.Knowledge.Providers;

/// <summary>
/// Abstraction for knowledge retrieval and management.
/// Implementations can resolve from server (HTTP) or embedded resources (local mode).
/// </summary>
internal interface IKnowledgeProvider
{
    /// <summary>
    /// Retrieves knowledge by name.
    /// </summary>
    Task<Models.Knowledge?> GetAsync(
        string knowledgeName,
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves system-scoped knowledge (no tenant isolation).
    /// </summary>
    Task<Models.Knowledge?> GetSystemAsync(
        string knowledgeName,
        string agentName,
        string? activationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates or creates knowledge.
    /// </summary>
    Task<bool> UpdateAsync(
        string knowledgeName,
        string content,
        string? type,
        string agentName,
        string? tenantId,
        bool systemScoped = false,
        string? activationName = null,
        string? description = null,
        bool visible = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes knowledge.
    /// </summary>
    Task<bool> DeleteAsync(
        string knowledgeName,
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all knowledge for an agent.
    /// </summary>
    Task<List<Models.Knowledge>> ListAsync(
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default);
}
