using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;
using Xians.Lib.Agents.Workflows;

namespace Xians.Lib.Temporal.Workflows.Activations;

/// <summary>
/// System activity for validating activations from within workflows.
/// Automatically registered with all workflows (workflows cannot make HTTP calls directly).
/// </summary>
public class ActivationActivities
{
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
}
