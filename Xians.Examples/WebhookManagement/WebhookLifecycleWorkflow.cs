using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace Xians.Examples.WebhookManagement;

/// <summary>
/// Custom Temporal workflow that is started by the agent's Default webhook.
/// It delegates all HTTP-backed webhook management to <see cref="WebhookManagementActivities"/>
/// (activities can perform I/O; deterministic workflow code cannot), then returns the outcome so the
/// triggering webhook can report what happened.
/// </summary>
[Description("Creates, lists and deletes inbound webhooks for the calling agent using the self-service SDK")]
[Workflow("Webhook Lifecycle Agent:Webhook Lifecycle Workflow")]
public class WebhookLifecycleWorkflow
{
    // Durable delay between creating/listing the webhooks and deleting them.
    private static readonly TimeSpan DeleteDelay = TimeSpan.FromMinutes(1);

    [WorkflowRun]
    public async Task<WebhookLifecycleResult> RunAsync()
    {
        var activityOptions = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(2),
            RetryPolicy = new Temporalio.Common.RetryPolicy
            {
                MaximumAttempts = 3
            }
        };

        Workflow.Logger.LogInformation("WebhookLifecycleWorkflow started; creating and listing webhooks via activity.");

        // 1) Create a couple of webhooks and list them.
        var result = await Workflow.ExecuteActivityAsync(
            (WebhookManagementActivities a) => a.CreateAndListWebhooksAsync(),
            activityOptions);

        // 2) Wait one minute before cleaning up, using a durable Temporal timer (survives worker
        //    restarts and does not tie up an activity/worker slot).
        Workflow.Logger.LogInformation("Waiting {Delay} before deleting the created webhooks.", DeleteDelay);
        await Workflow.DelayAsync(DeleteDelay);

        // 3) Delete the webhooks we created.
        var createdIds = result.Created.Select(w => w.Id).ToList();
        result.DeletedIds = await Workflow.ExecuteActivityAsync(
            (WebhookManagementActivities a) => a.DeleteWebhooksAsync(createdIds),
            activityOptions);

        Workflow.Logger.LogInformation(
            "WebhookLifecycleWorkflow completed. Created={Created}, Listed={Listed}, Deleted={Deleted}",
            result.Created.Count,
            result.Listed.Count,
            result.DeletedIds.Count);

        return result;
    }
}
