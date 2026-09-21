using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Core.Activations;
using Xians.Lib.Agents.Workflows;
using Xians.Lib.Temporal.Workflows.Activations.Models;

namespace Xians.Lib.Temporal.Workflows.Activations;

/// <summary>
/// System activities for agent/activation inspection and management from within workflows.
/// Automatically registered with all workflows (workflows cannot make HTTP calls directly).
/// </summary>
public class ActivationActivities
{
    private readonly XiansAgent? _owner;

    /// <summary>
    /// Parameterless constructor for unit tests of <see cref="ValidateActivationAsync"/>.
    /// Production workers pass the owning agent so HTTP uses that agent's client.
    /// </summary>
    public ActivationActivities()
    {
    }

    /// <summary>
    /// Creates activities bound to the worker's agent (HTTP client and tenant-header behavior).
    /// </summary>
    public ActivationActivities(XiansAgent owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>
    /// Checks that the given activation exists and is active for the target agent
    /// in the current tenant. Used before starting a child workflow under an explicit
    /// activation, so Temporal does not create an orphaned workflow on a task queue
    /// no worker listens on.
    /// Definitive negative results (not found / deactivated) are returned as a
    /// <see cref="ActivationCheckStatus"/> value rather than thrown, so the activity
    /// completes successfully and Temporal does not log a failed-activity warning for an
    /// expected outcome. The workflow-side caller maps the status to the typed exceptions.
    /// </summary>
    /// <param name="agentName">The target agent name (owner of the activation).</param>
    /// <param name="activationName">The activation name to validate.</param>
    /// <returns>The activation check status.</returns>
    /// <exception cref="ApplicationFailureException">Non-retryable, for invalid validation
    /// requests (e.g. server-side bad request). Transient errors (network, 5xx) propagate
    /// as-is and are retried per the activity's retry policy.</exception>
    [Activity]
    public async Task<ActivationCheckStatus> ValidateActivationAsync(string agentName, string activationName)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Validating activation '{ActivationName}' for agent '{AgentName}'",
            activationName,
            agentName);

        try
        {
            await ActivationValidationService.EnsureActivationActiveAsync(agentName, activationName);
            return ActivationCheckStatus.Active;
        }
        catch (ActivationNotFoundException ex)
        {
            ActivityExecutionContext.Current.Logger.LogDebug(
                "Activation '{ActivationName}' for agent '{AgentName}' not found: {Message}",
                activationName, agentName, ex.Message);
            return ActivationCheckStatus.NotFound;
        }
        catch (ActivationDeactivatedException ex)
        {
            ActivityExecutionContext.Current.Logger.LogDebug(
                "Activation '{ActivationName}' for agent '{AgentName}' is deactivated: {Message}",
                activationName, agentName, ex.Message);
            return ActivationCheckStatus.Deactivated;
        }
        catch (InvalidOperationException ex)
        {
            // Invalid request (e.g. server-side 400) - a genuine error, retrying won't help.
            throw new ApplicationFailureException(ex.Message, errorType: ex.GetType().Name, nonRetryable: true);
        }
        // HttpRequestException and other transient errors propagate and are retried per the activity's retry policy.
    }

    /// <summary>
    /// Checks whether the named agent exists in the current tenant.
    /// </summary>
    [Activity]
    public Task<bool> AgentExistsAsync(string agentName)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Checking agent existence for '{AgentName}'",
            agentName);
        return ClientFor(agentName).ExistsAsync();
    }

    /// <summary>
    /// Lists activations for the named agent in the current tenant.
    /// </summary>
    [Activity]
    public Task<List<ActivationInfo>> ListActivationsAsync(string agentName)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Listing activations for agent '{AgentName}'",
            agentName);
        return ClientFor(agentName).ListAsync();
    }

    /// <summary>
    /// Creates an inactive activation for the named agent.
    /// </summary>
    [Activity]
    public Task<ActivationInfo> CreateActivationAsync(ActivationCreateActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Creating activation '{ActivationName}' for agent '{AgentName}'",
            request.Name,
            request.AgentName);
        return ClientFor(request.AgentName).CreateAsync(
            request.Name,
            request.Description,
            request.ParticipantId,
            request.Workflows);
    }

    /// <summary>
    /// Activates an activation by id (starts its workflows).
    /// </summary>
    [Activity]
    public Task<ActivationInfo> ActivateActivationAsync(ActivationActivateActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Activating activation '{ActivationId}' for agent '{AgentName}'",
            request.ActivationId,
            request.AgentName);
        return ClientFor(request.AgentName).ActivateAsync(request.ActivationId, request.Workflows);
    }

    /// <summary>
    /// Deactivates an activation by id (cancels its workflows).
    /// </summary>
    [Activity]
    public Task<ActivationInfo> DeactivateActivationAsync(string agentName, string activationId)
    {
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Deactivating activation '{ActivationId}' for agent '{AgentName}'",
            activationId,
            agentName);
        return ClientFor(agentName).DeactivateAsync(activationId);
    }

    private AgentActivationClient ClientFor(string agentName)
    {
        var owner = _owner ?? XiansContext.CurrentAgent;
        return new AgentActivationClient(owner, agentName);
    }
}
