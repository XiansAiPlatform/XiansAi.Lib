using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Secrets.Models;
using Xians.Lib.Temporal;
using Xians.Lib.Temporal.Workflows.Secrets;
using Xians.Lib.Temporal.Workflows.Secrets.Models;

namespace Xians.Lib.Agents.Secrets;

/// <summary>
/// Context-aware executor for Secret Vault CRUD.
/// </summary>
/// <remarks>
/// Only the operations that carry no secret material - <see cref="ListAsync"/> and
/// <see cref="DeleteAsync"/> - are stubbed to <see cref="SecretVaultActivities"/> for workflow use.
/// The rest refuse to run in a workflow; see <see cref="EnsureNotInWorkflow"/>.
/// </remarks>
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

    /// <summary>
    /// Refuses operations that would move a secret value across an activity boundary.
    /// </summary>
    /// <remarks>
    /// Temporal durably records activity arguments in <c>ActivityTaskScheduled</c> and results in
    /// <c>ActivityTaskCompleted</c>, so routing a plaintext secret through an activity publishes it to
    /// everyone with read access to the namespace, for the full retention period. There is no way to
    /// call the vault from workflow code without crossing that boundary, so these operations belong in
    /// an activity or a message handler, where the value never enters history.
    /// </remarks>
    private static void EnsureNotInWorkflow(string operation)
    {
        if (!Workflow.InWorkflow)
            return;

        throw new ApplicationFailureException(
            $"Secret Vault {operation} cannot run inside a workflow: the secret value would be written " +
            "to Temporal workflow history, readable by anyone with access to the namespace. " +
            "Call it from an activity or a message handler and use the value there. " +
            "ListAsync and DeleteAsync carry no secret values and remain workflow-safe.",
            nonRetryable: true);
    }

    public Task<SecretVaultGetResponse> CreateAsync(
        string key,
        string value,
        object? additionalData,
        CancellationToken cancellationToken = default)
    {
        EnsureNotInWorkflow("CreateAsync");

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

        return CreateService().CreateAsync(request, cancellationToken);
    }

    public Task<SecretVaultFetchResponse?> FetchByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        EnsureNotInWorkflow("FetchByKeyAsync");

        var request = new SecretVaultFetchActivityRequest { Key = key, Scope = _scope };
        return CreateService().FetchByKeyAsync(request, cancellationToken);
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
        EnsureNotInWorkflow("GetByIdAsync");

        var request = new SecretVaultIdActivityRequest { Id = id, Scope = _scope };
        return CreateService().GetByIdAsync(request, cancellationToken);
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
        EnsureNotInWorkflow("UpdateAsync");

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

        return CreateService().UpdateAsync(request, cancellationToken);
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
