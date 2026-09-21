using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Secrets;
using Xians.Lib.Agents.Secrets.Models;
using Xians.Lib.Temporal.Workflows.Secrets.Models;

namespace Xians.Lib.Temporal.Workflows.Secrets;

/// <summary>
/// System activities for the Secret Vault operations that workflows may perform.
/// Automatically registered with all workflows (workflows cannot make HTTP calls directly).
/// </summary>
/// <remarks>
/// Deliberately limited to listing and deleting. Create, fetch-by-key, get-by-id and update all carry
/// a plaintext secret as an activity argument or result, and Temporal records both in workflow
/// history for the namespace's full retention period. Exposing them here would make that leak
/// reachable from workflow code, so they are available only through
/// <see cref="Xians.Lib.Agents.Secrets.SecretVaultScopeBuilder"/> outside a workflow.
/// </remarks>
public class SecretVaultActivities
{
    private readonly XiansAgent? _owner;

    /// <summary>
    /// Parameterless constructor for unit tests. Production workers pass the owning agent.
    /// </summary>
    public SecretVaultActivities()
    {
    }

    /// <summary>
    /// Creates activities bound to the worker's agent (HTTP client and tenant-header behavior).
    /// </summary>
    public SecretVaultActivities(XiansAgent owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    [Activity]
    public Task<List<SecretVaultListItem>> ListSecretsAsync(SecretVaultListActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug("Listing secrets");
        return Client().ListAsync(request.Scope);
    }

    [Activity]
    public Task<bool> DeleteSecretAsync(SecretVaultIdActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug("Deleting secret '{SecretId}'", request.Id);
        return Client().DeleteAsync(request);
    }

    private SecretVaultClient Client()
    {
        var owner = _owner ?? XiansContext.CurrentAgent;
        return new SecretVaultClient(owner);
    }
}
