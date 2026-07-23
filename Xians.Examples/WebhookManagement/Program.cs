using DotNetEnv;
using Microsoft.Extensions.Logging;
using Xians.Examples.WebhookManagement;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;

// -----------------------------------------------------------------------------
// Webhook Management example
//
// This example shows an agent that:
//   1. Exposes a Default (inbound) webhook on its Integrator workflow.
//   2. When that webhook is invoked, starts a custom Temporal workflow.
//   3. Inside that workflow (via an activity) uses the new self-service webhook
//      SDK methods to create a couple of webhooks, list them and delete them.
//
// The webhook management SDK (agent.Webhooks.*) resolves the agent name and
// activation name automatically from the runtime context, so the calling code
// never has to pass them in.
// -----------------------------------------------------------------------------

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL")
    ?? throw new InvalidOperationException("XIANS_SERVER_URL environment variable is not set");
var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY")
    ?? throw new InvalidOperationException("XIANS_API_KEY environment variable is not set");

// Initialize the Xians platform.
var xiansPlatform = await XiansPlatform.InitializeAsync(new()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    ServerLogLevel = LogLevel.Information,
});

// Register the agent.
var xiansAgent = xiansPlatform.Agents.Register(new()
{
    Name = "Webhook Lifecycle Agent",
    Description = "Demonstrates managing inbound webhooks (create/list/delete) from within a custom "
        + "Temporal workflow using the self-service webhook SDK.",
    Summary = "Agent that manages its own inbound webhooks.",
    Version = "1.0.0",
    Author = "99x",
    IsTemplate = false // System-scoped agents can receive webhooks across tenants.
});

// The Integrator workflow hosts the agent's inbound (Default) webhook endpoint.
var integratorWorkflow = xiansAgent.Workflows.DefineIntegrator();

// The custom workflow that performs the webhook lifecycle. It is not directly
// activable - it is started on demand by the webhook handler below.
var lifecycleWorkflow = xiansAgent.Workflows
    .DefineCustom<WebhookLifecycleWorkflow>(new() { Activable = false });

// Register the activity that performs the HTTP-backed webhook operations on the
// lifecycle workflow's worker.
lifecycleWorkflow.AddActivity(new WebhookManagementActivities());

// Handle the inbound (Default) webhook: start the lifecycle workflow and return its result.
integratorWorkflow.OnWebhook(async (context) =>
{
    try
    {
        Console.WriteLine($"Received webhook '{context.Webhook.Name}' with payload: {context.Webhook.Payload}");

        // Start the custom workflow WITHOUT waiting for its result. The workflow creates and lists the
        // webhooks, waits one minute (a durable timer), then deletes them - which is longer than the
        // webhook's synchronous response window, so we fire-and-forget here and return immediately.
        // A unique key keeps repeated invocations from colliding on the same workflow id.
        // NOTE: the OnWebhook handler runs inside an activity (not deterministic workflow code), so a
        // normal Guid.NewGuid() is fine here - Workflow.NewGuid() would throw "Not in workflow".
        var workflowKey = Guid.NewGuid().ToString();
        await XiansContext.Workflows
            .StartAsync<WebhookLifecycleWorkflow>(args: [], uniqueKey: workflowKey);

        context.Respond(new
        {
            message = "Webhook lifecycle workflow started. It will create + list webhooks, wait 1 minute, then delete them.",
            workflowKey
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing webhook: {ex.Message}");
        context.Response = WebhookResponse.InternalServerError($"Failed to process webhook: {ex.Message}");
    }
});

// Start the agent and all its workflows.
await xiansAgent.RunAllAsync();
