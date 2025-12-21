using DotNetEnv;
using Xians.Lib.Agents;

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("SERVER_URL");
var apiKey = Environment.GetEnvironmentVariable("API_KEY");

if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("SERVER_URL or API_KEY is not set");
    return;
}

var xiansPlatform = await XiansPlatform.InitializeAsync(new XiansOptions
{
    ServerUrl = serverUrl,
    ApiKey = apiKey
    // TenantId is automatically extracted from the ApiKey certificate
});


// Generate unique agent name to avoid conflicts
var agentName = $"XiansTestAgent";

// Act - Register agent and define workflow (same as AzureAIExample.csx)
var agent = xiansPlatform.Agents.Register(new XiansAgentRegistration
{
    Name = agentName,
    SystemScoped = false
});

// Define a default workflow (this should trigger upload to server)
var conversationalWorkflow = await agent.Workflows.DefineDefault(name: "Conversational", workers: 1);

// Define another default workflow (this should trigger upload to server)
var webhooksWorkflow = await agent.Workflows.DefineDefault(name: "Webhooks", workers: 1);

// Register handler for Conversational workflow
conversationalWorkflow.OnUserMessage(async (context) =>
{
    await context.ReplyAsync("[CONVERSATIONAL] Responding to: " + context.Message.Text);
});

// Register handler for Webhooks workflow
webhooksWorkflow.OnUserMessage(async (context) =>
{
    await context.ReplyAsync("[WEBHOOKS] Received webhook: " + context.Message.Text);
});

// Run all workflows
try
{
    await agent.RunAllAsync();
}
catch (TaskCanceledException)
{
    // Graceful shutdown - no need to log exception
}
catch (OperationCanceledException)
{
    // Graceful shutdown - no need to log exception
}