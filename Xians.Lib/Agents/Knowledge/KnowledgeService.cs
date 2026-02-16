using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Knowledge.Providers;

namespace Xians.Lib.Agents.Knowledge;

/// <summary>
/// Core service for knowledge operations.
/// Delegates to a provider (server or local) based on configuration.
/// Shared by both KnowledgeCollection and KnowledgeActivities to avoid code duplication.
/// </summary>
internal class KnowledgeService
{
    private readonly IKnowledgeProvider _provider;

    public KnowledgeService(IKnowledgeProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <summary>
    /// Retrieves knowledge (delegates to provider).
    /// </summary>
    public Task<Models.Knowledge?> GetAsync(
        string knowledgeName,
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        return _provider.GetAsync(knowledgeName, agentName, tenantId, activationName, cancellationToken);
    }

    /// <summary>
    /// Retrieves system-scoped knowledge (delegates to provider).
    /// </summary>
    public Task<Models.Knowledge?> GetSystemAsync(
        string knowledgeName,
        string agentName,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        return _provider.GetSystemAsync(knowledgeName, agentName, activationName, cancellationToken);
    }

    /// <summary>
    /// Updates or creates knowledge (delegates to provider).
    /// </summary>
    /// <param name="knowledgeName">The name of the knowledge.</param>
    /// <param name="content">The knowledge content.</param>
    /// <param name="type">Optional knowledge type (e.g., "instruction", "document").</param>
    /// <param name="agentName">The agent name.</param>
    /// <param name="tenantId">The tenant ID for isolation.</param>
    /// <param name="systemScoped">Whether this knowledge is system-scoped (shared across tenants).</param>
    /// <param name="activationName">The activation name (ID postfix).</param>
    /// <param name="description">Optional description of the knowledge item.</param>
    /// <param name="visible">Whether the knowledge item is visible. Defaults to true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the operation succeeds.</returns>
    public Task<bool> UpdateAsync(
        string knowledgeName,
        string content,
        string? type,
        string agentName,
        string? tenantId,
        bool systemScoped = false,
        string? activationName = null,
        string? description = null,
        bool visible = true,
        CancellationToken cancellationToken = default)
    {
        return _provider.UpdateAsync(
            knowledgeName,
            content,
            type,
            agentName,
            tenantId,
            systemScoped,
            activationName,
            description,
            visible,
            cancellationToken);
    }

    /// <summary>
    /// Deletes knowledge (delegates to provider).
    /// </summary>
    public Task<bool> DeleteAsync(
        string knowledgeName,
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        return _provider.DeleteAsync(knowledgeName, agentName, tenantId, activationName, cancellationToken);
    }

    /// <summary>
    /// Lists all knowledge for an agent (delegates to provider).
    /// </summary>
    public Task<List<Models.Knowledge>> ListAsync(
        string agentName,
        string? tenantId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        return _provider.ListAsync(agentName, tenantId, activationName, cancellationToken);
    }
}

