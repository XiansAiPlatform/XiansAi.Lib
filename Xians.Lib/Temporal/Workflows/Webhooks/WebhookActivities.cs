using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Webhooks;
using Xians.Lib.Agents.Webhooks.Models;
using Xians.Lib.Temporal.Workflows.Webhooks.Models;

namespace Xians.Lib.Temporal.Workflows.Webhooks;

/// <summary>
/// System activities for builtin webhook management from within workflows.
/// Automatically registered with all workflows (workflows cannot make HTTP calls directly).
/// </summary>
public class WebhookActivities
{
    private readonly XiansAgent? _owner;

    /// <summary>
    /// Parameterless constructor for unit tests. Production workers pass the owning agent.
    /// </summary>
    public WebhookActivities()
    {
    }

    /// <summary>
    /// Creates activities bound to the worker's agent (HTTP client and tenant-header behavior).
    /// </summary>
    public WebhookActivities(XiansAgent owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    [Activity]
    public Task<List<WebhookInfo>> ListWebhooksAsync(WebhookListActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Listing webhooks for agent '{AgentName}'",
            request.AgentName);
        return Client().ListAsync(request.AgentName, request.ActivationName);
    }

    [Activity]
    public Task<WebhookInfo> CreateWebhookAsync(WebhookCreateActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ActivityExecutionContext.Current.Logger.LogDebug(
            "Creating webhook for agent '{AgentName}', activation '{ActivationName}'",
            request.AgentName,
            request.ActivationName);
        return Client().CreateAsync(
            request.AgentName,
            request.ActivationName,
            request.WebhookName,
            request.WorkflowName,
            request.ParticipantId,
            request.TimeoutSeconds,
            request.Name);
    }

    [Activity]
    public Task<bool> DeleteWebhookAsync(string id)
    {
        ActivityExecutionContext.Current.Logger.LogDebug("Deleting webhook '{WebhookId}'", id);
        return Client().DeleteAsync(id);
    }

    private WebhookClient Client()
    {
        var owner = _owner ?? XiansContext.CurrentAgent;
        return new WebhookClient(owner);
    }
}
