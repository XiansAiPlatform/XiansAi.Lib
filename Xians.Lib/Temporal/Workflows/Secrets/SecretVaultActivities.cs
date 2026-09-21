using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Secrets;
using Xians.Lib.Agents.Secrets.Models;
using Xians.Lib.Temporal.Workflows.Secrets.Models;

namespace Xians.Lib.Temporal.Workflows.Secrets;

/// <summary>
/// System activities for Secret Vault CRUD from within workflows.
/// Automatically registered with all workflows (workflows cannot make HTTP calls directly).
/// </summary>
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
    public Task<SecretVaultGetResponse> CreateSecretAsync(SecretVaultCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Creating secret '{Key}'",
            request.Key);
        return Client().CreateAsync(request);
    }

    [Activity]
    public Task<SecretVaultFetchResponse?> FetchSecretByKeyAsync(SecretVaultFetchActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Fetching secret by key '{Key}'",
            request.Key);
        return Client().FetchByKeyAsync(request);
    }

    [Activity]
    public Task<List<SecretVaultListItem>> ListSecretsAsync(SecretVaultListActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug("Listing secrets");
        return Client().ListAsync(request.Scope);
    }

    [Activity]
    public Task<SecretVaultGetResponse?> GetSecretByIdAsync(SecretVaultIdActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug("Getting secret '{SecretId}'", request.Id);
        return Client().GetByIdAsync(request);
    }

    [Activity]
    public Task<SecretVaultGetResponse> UpdateSecretAsync(SecretVaultUpdateActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug("Updating secret '{SecretId}'", request.Id);
        return Client().UpdateAsync(request);
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
