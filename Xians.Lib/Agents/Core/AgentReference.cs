using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core.Activations;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Common;
using Xians.Lib.Temporal.Workflows.Activations;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// A reference to an agent in the current tenant, obtained via
/// <see cref="TenantAgents.Agent(string)"/>. Lets the calling agent inspect whether that
/// agent (and a given activation of it) exists and is active, and manage its activations,
/// without requiring the target agent to be registered in this process.
/// Safe to call from Temporal workflows (HTTP is stubbed to <see cref="ActivationActivities"/>)
/// and from activities (direct HTTP).
/// </summary>
/// <example>
/// <code>
/// var other = agent.Tenant.Agent("Fraud Detection Agent");
/// bool agentExists = await other.ExistsAsync();
/// ActivationCheckStatus status = await other.GetActivationStatusAsync("fraud-eu");
/// bool activationActive = await other.ActivationExistsAsync("fraud-eu");
///
/// var created = await other.CreateActivationAsync(name: "fraud-eu");
/// await created.ActivateAsync();
/// await created.DeactivateAsync();
/// </code>
/// </example>
public class AgentReference
{
    private readonly ActivationActivityExecutor _executor;

    /// <summary>
    /// Gets the name of the referenced agent.
    /// </summary>
    public string Name { get; }

    internal AgentReference(XiansAgent owner, string agentName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Agent name is required.", nameof(agentName));

        Name = IdentifierSanitizer.SanitizeAndValidateAgentName(agentName, nameof(agentName));

        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<AgentReference>();
        _executor = new ActivationActivityExecutor(owner, Name, logger);
    }

    /// <summary>
    /// Checks whether the referenced agent exists in the current tenant
    /// (<c>GET /api/agent/agents/exists</c>).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the agent exists; false if the server reports 404.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is not available
    /// or the server rejects the request (400).</exception>
    /// <exception cref="HttpRequestException">Thrown for transient/server errors so retry policies can apply.</exception>
    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        return _executor.ExistsAsync(cancellationToken);
    }

    /// <summary>
    /// Checks whether the referenced agent has an activation of the given name in the current tenant.
    /// </summary>
    /// <param name="activationName">The activation name to check (required - there is no in-process
    /// context for another agent).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activation status.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the HTTP service is not available
    /// or the server rejects the request (400).</exception>
    /// <exception cref="HttpRequestException">Thrown for transient/server errors so retry policies can apply.</exception>
    public Task<ActivationCheckStatus> GetActivationStatusAsync(
        string activationName,
        CancellationToken cancellationToken = default)
    {
        var sanitizedActivationName = IdentifierSanitizer.SanitizeAndValidateActivationName(
            activationName, nameof(activationName));

        return _executor.GetActivationStatusAsync(sanitizedActivationName, cancellationToken);
    }

    /// <summary>
    /// Returns true when the referenced agent has an active activation of the given name.
    /// A missing or deactivated activation returns false.
    /// </summary>
    /// <param name="activationName">The activation name to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the activation exists and is active; otherwise false.</returns>
    public async Task<bool> ActivationExistsAsync(
        string activationName,
        CancellationToken cancellationToken = default)
    {
        return await GetActivationStatusAsync(activationName, cancellationToken) == ActivationCheckStatus.Active;
    }

    /// <summary>
    /// Lists activations for the referenced agent in the current tenant.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's activations (empty list when none exist).</returns>
    public async Task<List<ActivationInfo>> ListActivationsAsync(CancellationToken cancellationToken = default)
    {
        var list = await _executor.ListActivationsAsync(cancellationToken);
        BindAll(list);
        return list;
    }

    /// <summary>
    /// Creates a new (inactive) activation for the referenced agent in the current tenant.
    /// Call <see cref="ActivationInfo.ActivateAsync"/> on the returned handle to start its workflows.
    /// </summary>
    /// <param name="name">Activation name (idPostfix).</param>
    /// <param name="description">Optional description.</param>
    /// <param name="participantId">Optional participant id.</param>
    /// <param name="workflows">Optional workflow configurations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created activation (bound for further activate/deactivate calls).</returns>
    public async Task<ActivationInfo> CreateActivationAsync(
        string name,
        string? description = null,
        string? participantId = null,
        IEnumerable<WorkflowConfig>? workflows = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedName = IdentifierSanitizer.SanitizeAndValidateActivationName(name, nameof(name));

        var created = await _executor.CreateActivationAsync(
            sanitizedName,
            description,
            participantId,
            workflows,
            cancellationToken);
        created.Bind(this);
        return created;
    }

    /// <summary>
    /// Activates an activation by id (starts its workflows) in the current tenant.
    /// Prefer calling <see cref="ActivationInfo.ActivateAsync"/> on a handle from list/create when available.
    /// </summary>
    /// <param name="activationId">The activation id (see <see cref="ActivationInfo.Id"/>).</param>
    /// <param name="workflowConfiguration">Optional workflow configuration override for this activate call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated activation returned by the server.</returns>
    public async Task<ActivationInfo> ActivateAsync(
        string activationId,
        IEnumerable<WorkflowConfig>? workflowConfiguration = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(activationId))
            throw new ArgumentException("Activation id is required.", nameof(activationId));

        var activated = await _executor.ActivateAsync(
            activationId,
            workflowConfiguration,
            cancellationToken);
        activated.Bind(this);
        return activated;
    }

    /// <summary>
    /// Deactivates an activation by id (cancels its workflows) in the current tenant.
    /// Prefer calling <see cref="ActivationInfo.DeactivateAsync"/> on a handle from list/create when available.
    /// </summary>
    /// <param name="activationId">The activation id (see <see cref="ActivationInfo.Id"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated activation returned by the server.</returns>
    public async Task<ActivationInfo> DeactivateAsync(
        string activationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(activationId))
            throw new ArgumentException("Activation id is required.", nameof(activationId));

        var deactivated = await _executor.DeactivateAsync(activationId, cancellationToken);
        deactivated.Bind(this);
        return deactivated;
    }

    private void BindAll(IEnumerable<ActivationInfo> list)
    {
        foreach (var item in list)
        {
            item.Bind(this);
        }
    }
}
