using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Secrets.Models;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Temporal.Workflows.Secrets.Models;

namespace Xians.Lib.Agents.Secrets;

/// <summary>
/// Fluent scope builder for Secret Vault operations.
/// <para>
/// Built up by chaining narrowing setters: <see cref="TenantScope"/>, <see cref="AgentScope()"/>,
/// <see cref="ParticipantScope()"/>, and <see cref="ActivationScope()"/>. Each scope setter has a
/// **no-arg overload** that auto-resolves the value from <see cref="XiansContext"/>, and an explicit
/// overload that takes a value. Then perform CRUD: <see cref="CreateAsync"/>, <see cref="FetchByKeyAsync"/>,
/// <see cref="ListAsync"/>, <see cref="GetByIdAsync"/>, <see cref="UpdateAsync"/>, <see cref="DeleteAsync"/>.
/// </para>
/// Safe to call from Temporal workflows (HTTP is stubbed to Secret Vault activities)
/// and from activities (direct HTTP).
/// </summary>
public class SecretVaultScopeBuilder
{
    private readonly XiansAgent _agent;
    private string? _tenantId;
    private string? _agentId;
    private string? _userId;
    private string? _activationName;

    internal SecretVaultScopeBuilder(XiansAgent agent, string? tenantId, string? agentId, string? userId, string? activationName = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _tenantId = tenantId;
        _agentId = agentId;
        _userId = userId;
        _activationName = activationName;
    }

    /// <summary>
    /// Overrides the tenant scope for subsequent operations. Pass <c>null</c> to broaden to cross-tenant.
    /// </summary>
    public SecretVaultScopeBuilder TenantScope(string? tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    /// <summary>
    /// Narrows the scope to the **current agent**, resolved from <see cref="XiansContext.SafeAgentName"/>
    /// (falling back to the registered agent's <see cref="XiansAgent.Name"/>). Always succeeds because
    /// the agent name is known at registration time.
    /// </summary>
    public SecretVaultScopeBuilder AgentScope()
    {
        _agentId = XiansContext.SafeAgentName ?? _agent.Name;
        return this;
    }

    /// <summary>
    /// Sets the agent scope to an explicit value. Pass <c>null</c> to broaden to all agents.
    /// </summary>
    public SecretVaultScopeBuilder AgentScope(string? agentId)
    {
        _agentId = agentId;
        return this;
    }

    /// <summary>
    /// Narrows the scope to the **current participant**, resolved from
    /// <see cref="XiansContext.SafeParticipantId"/>. Throws if no participant is in context — call
    /// <see cref="ParticipantScope(string?)"/> with an explicit value (or <c>null</c> to broaden) instead.
    /// </summary>
    public SecretVaultScopeBuilder ParticipantScope()
    {
        var participantId = XiansContext.SafeParticipantId;
        if (string.IsNullOrEmpty(participantId))
        {
            throw new InvalidOperationException(
                "No participant id is available in the current XiansContext. " +
                "Call ParticipantScope(participantId) with an explicit value, or omit the participant scope.");
        }
        _userId = participantId;
        return this;
    }

    /// <summary>
    /// Sets the participant (user) scope to an explicit value. Pass <c>null</c> to broaden to any participant.
    /// </summary>
    public SecretVaultScopeBuilder ParticipantScope(string? participantId)
    {
        _userId = participantId;
        return this;
    }

    /// <summary>
    /// Sets the user (participant) scope. Kept for backwards compatibility; new code should prefer
    /// <see cref="ParticipantScope(string?)"/> / <see cref="ParticipantScope()"/>.
    /// Null = any user may access.
    /// </summary>
    public SecretVaultScopeBuilder UserScope(string? userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Narrows the scope to the **current activation**, resolved from
    /// <see cref="XiansContext.SafeIdPostfix"/>. Throws if no activation is in context — call
    /// <see cref="ActivationScope(string?)"/> with an explicit value (or <c>null</c>) instead.
    /// </summary>
    public SecretVaultScopeBuilder ActivationScope()
    {
        var activationName = XiansContext.SafeIdPostfix;
        if (string.IsNullOrEmpty(activationName))
        {
            throw new InvalidOperationException(
                "No activation (idPostfix) is available in the current XiansContext. " +
                "Call ActivationScope(activationName) with an explicit value, or omit the activation scope.");
        }
        _activationName = activationName;
        return this;
    }

    /// <summary>
    /// Sets the activation scope to an explicit value. Null = any activation of the agent may access;
    /// when set, only that agent activation (by name) may access.
    /// </summary>
    public SecretVaultScopeBuilder ActivationScope(string? activationName)
    {
        _activationName = activationName;
        return this;
    }

    /// <summary>
    /// Creates a secret with the current scope. Key must be unique.
    /// </summary>
    /// <param name="key">Unique secret key.</param>
    /// <param name="value">Secret value (encrypted at rest by the server).</param>
    /// <param name="additionalData">Optional flat key-value metadata (string, number, or boolean values only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created secret (with decrypted value).</returns>
    public Task<SecretVaultGetResponse> CreateAsync(
        string key,
        string value,
        object? additionalData = null,
        CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequiredWithMaxLength(key, nameof(key), 512);
        ValidationHelper.ValidateRequired(value, nameof(value));
        ValidateScopeAgainstMessageContext();

        return GetExecutor().CreateAsync(key, value, additionalData, cancellationToken);
    }

    /// <summary>
    /// Fetches a secret by key with strict scope match. Returns decrypted value and optional additionalData only.
    /// </summary>
    /// <param name="key">Secret key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Value and additionalData, or null if not found or access denied.</returns>
    public Task<SecretVaultFetchResponse?> FetchByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequiredWithMaxLength(key, nameof(key), 512);
        ValidateScopeAgainstMessageContext();

        return GetExecutor().FetchByKeyAsync(key, cancellationToken);
    }

    /// <summary>
    /// Lists secrets with optional tenant/agent filter (current scope values).
    /// </summary>
    public Task<List<SecretVaultListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        ValidateScopeAgainstMessageContext();
        return GetExecutor().ListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a secret by id (full record including decrypted value).
    /// </summary>
    public Task<SecretVaultGetResponse?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
        ValidateScopeAgainstMessageContext();
        return GetExecutor().GetByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Updates a secret by id. Omitted properties leave existing values unchanged.
    /// </summary>
    public Task<SecretVaultGetResponse> UpdateAsync(
        string id,
        string? value = null,
        object? additionalData = null,
        string? tenantId = null,
        string? agentId = null,
        string? userId = null,
        string? activationName = null,
        CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
        ValidateScopeAgainstMessageContext();
        return GetExecutor().UpdateAsync(
            id, value, additionalData, tenantId, agentId, userId, activationName, cancellationToken);
    }

    /// <summary>
    /// Deletes a secret by id.
    /// </summary>
    /// <returns>True if deleted, false if not found.</returns>
    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ValidationHelper.ValidateRequired(id, nameof(id));
        ValidateScopeAgainstMessageContext();
        return GetExecutor().DeleteAsync(id, cancellationToken);
    }

    private SecretVaultActivityExecutor GetExecutor()
    {
        var logger = Common.Infrastructure.LoggerFactory.CreateLogger<SecretVaultActivityExecutor>();
        var scope = new SecretVaultScopePayload
        {
            TenantId = _tenantId,
            AgentId = _agentId,
            UserId = _userId,
            ActivationName = _activationName
        };
        return new SecretVaultActivityExecutor(_agent, scope, logger);
    }

    /// <summary>
    /// Validates that the current scope (tenantId, userId, agentId, activationName) matches the message/workflow context.
    /// When in workflow or activity context, if a scope value is set it must equal the corresponding XiansContext value.
    /// Skips validation when not in workflow/activity (e.g. tests or local mode).
    /// </summary>
    private void ValidateScopeAgainstMessageContext()
    {
        if (!XiansContext.InWorkflowOrActivity)
            return;

        var contextTenantId = XiansContext.SafeTenantId;
        var contextUserId = XiansContext.SafeParticipantId;
        var contextAgentName = XiansContext.SafeAgentName ?? _agent.Name;
        var contextActivationName = XiansContext.SafeIdPostfix;

        if (!string.IsNullOrEmpty(_tenantId) && !string.IsNullOrEmpty(contextTenantId) &&
            !string.Equals(_tenantId, contextTenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Secret Vault tenantId scope '{_tenantId}' does not match message context tenant '{contextTenantId}'.");
        }

        if (!string.IsNullOrEmpty(_agentId) && !string.IsNullOrEmpty(contextAgentName) &&
            !string.Equals(_agentId, contextAgentName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Secret Vault agentId scope '{_agentId}' does not match message context agent '{contextAgentName}'.");
        }

        if (!string.IsNullOrEmpty(_userId) && !string.IsNullOrEmpty(contextUserId) &&
            !string.Equals(_userId, contextUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Secret Vault userId scope '{_userId}' does not match message context user '{contextUserId}'.");
        }

        if (!string.IsNullOrEmpty(_activationName) && !string.IsNullOrEmpty(contextActivationName) &&
            !string.Equals(_activationName, contextActivationName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Secret Vault activationName scope '{_activationName}' does not match message context activation '{contextActivationName}'.");
        }
    }
}
