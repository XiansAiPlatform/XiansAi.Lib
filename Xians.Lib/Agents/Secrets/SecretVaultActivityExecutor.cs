using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Secrets.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Secrets;
using Xians.Lib.Temporal.Workflows.Secrets.Models;

namespace Xians.Lib.Agents.Secrets;

/// <summary>
/// Context-aware executor for Secret Vault CRUD.
/// In a workflow the call is stubbed to <see cref="SecretVaultActivities"/>;
/// in an activity it uses <see cref="SecretVaultClient"/> HTTP directly.
/// </summary>
internal sealed class SecretVaultActivityExecutor : ContextAwareActivityExecutor<SecretVaultActivities, SecretVaultClient>
{
    private readonly XiansAgent _agent;
    private readonly SecretVaultScopePayload _scope;

    public SecretVaultActivityExecutor(XiansAgent agent, SecretVaultScopePayload scope, ILogger logger)
        : base(logger)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    protected override SecretVaultClient CreateService() => new(_agent);

    public Task<SecretVaultGetResponse> CreateAsync(
        string key,
        string value,
        object? additionalData,
        CancellationToken cancellationToken = default)
    {
        var request = new SecretVaultCreateRequest
        {
            Key = key,
            Value = value,
            TenantId = _scope.TenantId,
            AgentId = _scope.AgentId,
            UserId = _scope.UserId,
            ActivationName = _scope.ActivationName,
            AdditionalData = additionalData
        };

        return ExecuteAsync(
            act => act.CreateSecretAsync(request),
            svc => svc.CreateAsync(request, cancellationToken),
            operationName: "CreateSecret");
    }

    public Task<SecretVaultFetchResponse?> FetchByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var request = new SecretVaultFetchActivityRequest { Key = key, Scope = _scope };
        return ExecuteAsync(
            act => act.FetchSecretByKeyAsync(request),
            svc => svc.FetchByKeyAsync(request, cancellationToken),
            operationName: "FetchSecretByKey");
    }

    public Task<List<SecretVaultListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        var request = new SecretVaultListActivityRequest { Scope = _scope };
        return ExecuteAsync(
            act => act.ListSecretsAsync(request),
            svc => svc.ListAsync(request.Scope, cancellationToken),
            operationName: "ListSecrets");
    }

    public Task<SecretVaultGetResponse?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var request = new SecretVaultIdActivityRequest { Id = id, Scope = _scope };
        return ExecuteAsync(
            act => act.GetSecretByIdAsync(request),
            svc => svc.GetByIdAsync(request, cancellationToken),
            operationName: "GetSecretById");
    }

    public Task<SecretVaultGetResponse> UpdateAsync(
        string id,
        string? value,
        object? additionalData,
        string? tenantId,
        string? agentId,
        string? userId,
        string? activationName,
        CancellationToken cancellationToken = default)
    {
        var request = new SecretVaultUpdateActivityRequest
        {
            Id = id,
            Value = value,
            AdditionalData = additionalData,
            Scope = new SecretVaultScopePayload
            {
                TenantId = tenantId ?? _scope.TenantId,
                AgentId = agentId ?? _scope.AgentId,
                UserId = userId ?? _scope.UserId,
                ActivationName = activationName ?? _scope.ActivationName
            }
        };

        return ExecuteAsync(
            act => act.UpdateSecretAsync(request),
            svc => svc.UpdateAsync(request, cancellationToken),
            operationName: "UpdateSecret");
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var request = new SecretVaultIdActivityRequest { Id = id, Scope = _scope };
        return ExecuteAsync(
            act => act.DeleteSecretAsync(request),
            svc => svc.DeleteAsync(request, cancellationToken),
            operationName: "DeleteSecret");
    }
}
