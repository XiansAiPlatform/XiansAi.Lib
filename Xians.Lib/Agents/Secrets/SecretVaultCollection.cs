using Xians.Lib.Agents.Core;

namespace Xians.Lib.Agents.Secrets;

/// <summary>
/// Collection for Secret Vault operations.
/// <para>
/// Secrets are stored under up to four scope dimensions: tenant, agent, participant (user), and activation.
/// Most secrets are scoped to the **tenant** only; you opt into narrower scopes by chaining setters.
/// Each chained method has a no-arg overload that auto-resolves the value from <see cref="XiansContext"/>,
/// and an overload that takes an explicit value.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // Tenant-only (most common). Tenant id auto-resolved from XiansContext / certificate.
/// var scoped = agent.Secrets.TenantScope();
///
/// // Narrow further: tenant + current agent
/// var perAgent = agent.Secrets.TenantScope().AgentScope();
///
/// // Narrow further: tenant + current agent + current participant
/// var perUser = agent.Secrets.TenantScope().AgentScope().ParticipantScope();
///
/// // Narrow further still: tenant + current agent + current participant + current activation
/// var perActivation = agent.Secrets.TenantScope().AgentScope().ParticipantScope().ActivationScope();
///
/// // Override any dimension with an explicit value
/// var otherTenant = agent.Secrets.TenantScope("tenant-2").AgentScope("agent-x");
///
/// // Admin / cross-tenant flows: no scope at all
/// var all = await agent.Secrets.ScopeUnbound().ListAsync();
/// </code>
/// </example>
public class SecretVaultCollection
{
    private readonly XiansAgent _agent;

    internal SecretVaultCollection(XiansAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    /// <summary>
    /// Returns a tenant-scoped builder using the tenant id from <see cref="XiansContext.SafeTenantId"/>
    /// (falling back to the agent's certificate tenant). This is the recommended starting point for the
    /// common case where a secret is scoped only to the current tenant; chain
    /// <see cref="SecretVaultScopeBuilder.AgentScope()"/>,
    /// <see cref="SecretVaultScopeBuilder.ParticipantScope()"/>, and
    /// <see cref="SecretVaultScopeBuilder.ActivationScope()"/> to narrow further.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no tenant id can be resolved from context or agent options. Use
    /// <see cref="TenantScope(string)"/> with an explicit value or <see cref="ScopeUnbound"/> in that case.
    /// </exception>
    public SecretVaultScopeBuilder TenantScope()
    {
        var tenantId = ResolveTenantId();
        if (string.IsNullOrEmpty(tenantId))
        {
            throw new InvalidOperationException(
                "Cannot resolve tenant id from XiansContext or agent options. " +
                "Call TenantScope(tenantId) with an explicit tenant, or ScopeUnbound() for cross-tenant flows.");
        }
        return new SecretVaultScopeBuilder(_agent, tenantId, null, null, null);
    }

    /// <summary>
    /// Returns a tenant-scoped builder with an explicit tenant id (skips context auto-resolution).
    /// </summary>
    public SecretVaultScopeBuilder TenantScope(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
            throw new ArgumentException("Tenant id must be non-empty. Use ScopeUnbound() for cross-tenant flows.", nameof(tenantId));
        return new SecretVaultScopeBuilder(_agent, tenantId, null, null, null);
    }

    /// <summary>
    /// Returns a builder with no scope pre-set (tenant, agent, participant, and activation are all null).
    /// Use this for admin / cross-tenant flows where you explicitly want to operate across dimensions,
    /// or for tests where no execution context exists. Prefer <see cref="TenantScope()"/> in ordinary flows.
    /// </summary>
    public SecretVaultScopeBuilder ScopeUnbound()
    {
        return new SecretVaultScopeBuilder(_agent, null, null, null, null);
    }

    /// <summary>
    /// Alias for <see cref="TenantScope()"/> — returns a tenant-scoped builder using the tenant from context.
    /// Kept for backwards compatibility; prefer <see cref="TenantScope()"/> in new code.
    /// </summary>
    public SecretVaultScopeBuilder Scope() => TenantScope();

    internal string? TryResolveTenantId() => ResolveTenantId();

    private string? ResolveTenantId()
    {
        var fromContext = XiansContext.SafeTenantId;
        if (!string.IsNullOrEmpty(fromContext))
            return fromContext;
        if (!_agent.SystemScoped)
            return _agent.Options?.CertificateTenantId;
        return null;
    }
}
