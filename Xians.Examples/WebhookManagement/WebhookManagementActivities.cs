using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Webhooks.Models;

namespace Xians.Examples.WebhookManagement;

/// <summary>
/// Activities that exercise the new self-service webhook SDK methods.
/// <para>
/// The webhook SDK methods (<c>agent.Webhooks.CreateAsync/ListAsync/DeleteAsync</c>) perform real HTTP
/// calls to the Xians server, so they must run inside a Temporal <b>activity</b> - never directly in
/// deterministic workflow code. The workflow (<see cref="WebhookLifecycleWorkflow"/>) invokes these
/// activities via <c>Workflow.ExecuteActivityAsync</c> and controls the delay between create and delete
/// with a durable workflow timer.
/// </para>
/// <para>
/// Notice that the agent and activation are never passed in explicitly - they are resolved
/// automatically from the current <see cref="XiansContext"/> (the activity inherits the activation of
/// the workflow that started it).
/// </para>
/// </summary>
public class WebhookManagementActivities
{
    /// <summary>
    /// Creates a couple of webhooks and lists them. Returns the outcome so the workflow can wait and
    /// then delete the created webhooks in a later activity.
    /// </summary>
    [Activity]
    public async Task<WebhookLifecycleResult> CreateAndListWebhooksAsync()
    {
        var logger = ActivityExecutionContext.Current.Logger;

        // The agent is resolved from the workflow/activity context (no need to pass it around).
        var agent = XiansContext.CurrentAgent;
        var activation = XiansContext.SafeIdPostfix ?? "(default activation)";
        var result = new WebhookLifecycleResult { Activation = activation };

        logger.LogInformation(
            "Managing webhooks for agent '{Agent}', activation '{Activation}'",
            agent.Name,
            activation);

        // 0) Self-info: confirm this activation actually exists/active in the tenant.
        result.ActivationExists = await agent.ActivationExistsAsync();
        logger.LogInformation("ActivationExists = {Exists}", result.ActivationExists);

        // 1) Create a couple of webhooks (agent + activation resolved automatically).
        foreach (var name in new[] { "OrderReceived", "PaymentReceived" })
        {
            var webhook = await agent.Webhooks.CreateAsync(
                webhookName: name,
                name: $"{name} (created by lifecycle demo)");

            // Do NOT log or persist webhook.WebhookUrl: it embeds an apikeyId that functions as the
            // credential to invoke the webhook. Persisting it into durable workflow history/logs would
            // leak a callable credential. Only the id/name are safe to keep.
            logger.LogInformation(
                "Created webhook '{Name}' -> id={Id}",
                name,
                webhook.Id);

            result.Created.Add(new WebhookSummary(webhook.Id, webhook.WebhookName ?? name));
        }

        // 2) List the webhooks currently registered for this agent/activation.
        var listed = await agent.Webhooks.ListAsync();
        logger.LogInformation("Listed {Count} webhook(s) for this activation", listed.Count);
        foreach (var webhook in listed)
        {
            result.Listed.Add(new WebhookSummary(webhook.Id, webhook.WebhookName ?? webhook.Name));
        }

        return result;
    }

    /// <summary>
    /// Deletes the given webhooks (by id), leaving the environment clean.
    /// </summary>
    /// <param name="webhookIds">Ids of the webhooks to delete.</param>
    /// <returns>The ids that were actually deleted.</returns>
    [Activity]
    public async Task<List<string>> DeleteWebhooksAsync(List<string> webhookIds)
    {
        var logger = ActivityExecutionContext.Current.Logger;
        var agent = XiansContext.CurrentAgent;
        var deletedIds = new List<string>();

        foreach (var id in webhookIds)
        {
            var deleted = await agent.Webhooks.DeleteAsync(id);
            logger.LogInformation("Deleted webhook id={Id} -> {Deleted}", id, deleted);
            if (deleted)
            {
                deletedIds.Add(id);
            }
        }

        return deletedIds;
    }
}

/// <summary>Result of the webhook create/list/delete lifecycle, returned by the workflow.</summary>
public class WebhookLifecycleResult
{
    public string Activation { get; set; } = string.Empty;
    public bool ActivationExists { get; set; }
    public List<WebhookSummary> Created { get; set; } = new();
    public List<WebhookSummary> Listed { get; set; } = new();
    public List<string> DeletedIds { get; set; } = new();
}

/// <summary>
/// Minimal, serializable view of a webhook for the workflow result. Intentionally omits the webhook
/// URL, which embeds a callable credential (apikeyId) and must not be persisted into workflow history.
/// </summary>
public record WebhookSummary(string Id, string Name);
