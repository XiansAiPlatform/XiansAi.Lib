using Xians.Lib.Agents.Core;

namespace Xians.Lib.Agents.Secrets;

/// <summary>
/// Collection for Secret Vault operations. Use the builder pattern to set scope then perform CRUD.
/// </summary>
/// <example>
/// <code>
/// // Scope by tenant, agent, and user then create/fetch/list
/// var scoped = agent.Secrets.Scope()
///     .TenantScope("tenant-1")
///     .AgentScope("my-agent")
///     .UserScope("user-1");
/// await scoped.CreateAsync("api-key", "sk-xxx");
/// var fetched = await scoped.FetchByKeyAsync("api-key");
/// var list = await scoped.ListAsync();
///
/// // No scope (cross-tenant / any agent / any user)
/// var all = agent.Secrets.Scope().ListAsync();
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
    /// Returns a scope builder. Chain <see cref="SecretVaultScopeBuilder.TenantScope"/>, <see cref="SecretVaultScopeBuilder.AgentScope"/>,
    /// and <see cref="SecretVaultScopeBuilder.UserScope"/> to set scope, then call CreateAsync, FetchByKeyAsync, ListAsync, GetByIdAsync, UpdateAsync, or DeleteAsync.
    /// </summary>
    public SecretVaultScopeBuilder Scope()
    {
        var tenantId = ResolveTenantId();
        return new SecretVaultScopeBuilder(_agent, tenantId, null, null);
    }

    /// <summary>
    /// Convenience: scope with tenant only. Equivalent to Scope().TenantScope(tenantId).
    /// </summary>
    public SecretVaultScopeBuilder TenantScope(string? tenantId)
    {
        return new SecretVaultScopeBuilder(_agent, tenantId, null, null);
    }

    /// <summary>
    /// Convenience: scope with tenant and agent. Equivalent to Scope().TenantScope(tenantId).AgentScope(agentId).
    /// </summary>
    public SecretVaultScopeBuilder TenantScope(string? tenantId, string? agentId)
    {
        return new SecretVaultScopeBuilder(_agent, tenantId, agentId, null);
    }

    /// <summary>
    /// Convenience: full scope. Equivalent to Scope().TenantScope(tenantId).AgentScope(agentId).UserScope(userId).
    /// </summary>
    public SecretVaultScopeBuilder WithScope(string? tenantId, string? agentId, string? userId)
    {
        return new SecretVaultScopeBuilder(_agent, tenantId, agentId, userId);
    }

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
