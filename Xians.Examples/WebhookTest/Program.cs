using DotNetEnv;
using System.Net;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;

// Load environment variables from .env file
Env.Load();

// Get configuration from environment variables
var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL") 
    ?? throw new InvalidOperationException("XIANS_SERVER_URL not found in environment variables");
var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY") 
    ?? throw new InvalidOperationException("XIANS_API_KEY not found in environment variables");


// Initialize Xians Platform
var xiansPlatform = await XiansPlatform.InitializeAsync(new ()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey
});

// Register a new agent with Xians
var xiansAgent = xiansPlatform.Agents.Register(new ()
{
    Name = "WebhookTestAgent",
    IsTemplate = true  // System-scoped agents can handle webhooks across all tenants
});

// Define a built-in conversational workflow
var integratorWorkflow = xiansAgent.Workflows.DefineBuiltIn(name: "Integrator");

// Handle incoming webhooks
integratorWorkflow.OnWebhook(async (context) =>
{
    try
    {
        // Access webhook properties
        var webhookName = context.Webhook.Name;
        var payload = context.Webhook.Payload; // Now a string
        var participantId = context.Webhook.ParticipantId;
        
        // Process the webhook
        Console.WriteLine($"Received webhook: {webhookName}");
        Console.WriteLine($"Payload: {payload}");
        Console.WriteLine($"ParticipantId: {participantId}");
        
        // Process webhook logic here...
        // await ProcessWebhookAsync(webhookName, payload);
        
        // Success response - Option 1: Set the response directly with full control
        context.Response = new WebhookResponse
        {
            StatusCode = HttpStatusCode.OK,
            Content = "{\"message\": \"Success\", \"data\": {\"agent\": \"" + xiansAgent.Name + "\"}}",
            ContentType = "application/json",
            Headers = new Dictionary<string, string[]>
            {
                ["X-Custom-Header"] = new[] { "CustomValue" },
                ["X-Webhook-Processed"] = new[] { "true" }
            }
        };
        
        // Option 2: Use helper methods for simpler responses
        // context.Respond(new { message = "Success", webhookName, processedAt = DateTime.UtcNow });
        
        // Option 3: Use static factory methods
        // context.Response = WebhookResponse.Ok(new { message = "Success", webhookName });
    }
    catch (Exception ex)
    {
        // Error responses are automatically handled, but you can also set custom error responses
        Console.WriteLine($"Error processing webhook: {ex.Message}");
        context.Response = WebhookResponse.InternalServerError($"Failed to process webhook: {ex.Message}");
    }
    
    await Task.CompletedTask;
});

// Start the agent and all workflows
await xiansAgent.RunAllAsync();