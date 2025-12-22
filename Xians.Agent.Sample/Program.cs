using DotNetEnv;
using Xians.Lib.Agents;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL");
var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY");
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(xiansApiKey) || string.IsNullOrEmpty(openAiApiKey))
{
    Console.WriteLine("XIANS_SERVER_URL or XIANS_API_KEY or OPENAI_API_KEY is not set");
    return;
}

var xiansPlatform = await XiansPlatform.InitializeAsync(new XiansOptions
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey
    // TenantId is automatically extracted from the ApiKey certificate
});


// Generate unique agent name to avoid conflicts
var agentName = $"XiansTestAgent V2";

// Act - Register agent and define workflow (same as AzureAIExample.csx)
var agent = xiansPlatform.Agents.Register(new XiansAgentRegistration
{
    Name = agentName,
    SystemScoped = true
});

// Define a default workflow (this should trigger upload to server)
var conversationalWorkflow = await agent.Workflows.DefineDefault(name: "Conversational", workers: 1);

// Define another default workflow (this should trigger upload to server)
var webhooksWorkflow = await agent.Workflows.DefineDefault(name: "Webhooks", workers: 1);

// Define custom workflow
var customWorkflow = await agent.Workflows.DefineCustom<CustomWorkflow>(workers: 1);

AIAgent mafAgent = new OpenAIClient(openAiApiKey).GetChatClient("gpt-4o-mini").CreateAIAgent(instructions: "You are good at telling jokes.", name: "Joker");

// Register handler for Conversational workflow
conversationalWorkflow.OnUserMessage(async (context) =>
{
    var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
    Console.WriteLine("Chat history count: " + history.Count);
    foreach (var message in history)
    {
        Console.WriteLine($"{message.Direction}: {message.Text}");
    }
    var response = await mafAgent.RunAsync(context.Message.Text);
    await context.ReplyAsync(response.Text);
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