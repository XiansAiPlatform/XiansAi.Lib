using Xians.Lib.Agents.Core;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Knowledge;  // For UploadEmbeddedResourceAsync extension

Env.Load();

// Get OpenAI API key from environment variable
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set");

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL") 
    ?? throw new InvalidOperationException("XIANS_SERVER_URL environment variable is not set"); 
var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY") 
    ?? throw new InvalidOperationException("XIANS_API_KEY environment variable is not set");

// Initialize Xians Platform with optional logging configuration
var xiansPlatform = await XiansPlatform.InitializeAsync(new ()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    // Optional: Configure log levels programmatically (overrides environment variables)
    ConsoleLogLevel = LogLevel.Debug,  // What shows in console
    ServerLogLevel = LogLevel.Warning         // What gets uploaded to server
});

// Register a new agent with Xians
var xiansAgent = xiansPlatform.Agents.Register(new ()
{
    Name = "My Simple System Agent",
    IsTemplate = true  
});

// Upload embedded knowledge resources
await xiansAgent.Knowledge.UploadEmbeddedResourceAsync(
    resourcePath: "knowledge/system-prompt.md",
    knowledgeName: "system-prompt",
    knowledgeType: "markdown"
);

await xiansAgent.Knowledge.UploadEmbeddedResourceAsync(
    resourcePath: "knowledge/welcome-message.txt",
    knowledgeName: "Welcome Message"
);


await xiansAgent.Knowledge.UploadEmbeddedResourceAsync(
    resourcePath: "knowledge/what-to-extract.md",
    knowledgeName: "What to Extract"
);


await xiansAgent.Knowledge.UploadEmbeddedResourceAsync(
    resourcePath: "knowledge/configuration.json",
    knowledgeName: "Configuration"
);

// Define a built-in conversational workflow
var conversationalWorkflow = xiansAgent.Workflows.DefineBuiltIn(name: "Conversing Workflow");

// Create your MAF agent instance
var mafAgent = new MafSubAgent(openAiApiKey);

// Handle incoming user messages
conversationalWorkflow.OnUserChatMessage(async (context) =>
{
    var knowledge = await XiansContext.CurrentAgent.Knowledge.GetAsync("Welcome Message");

    await context.ReplyAsync("welcome msg:" + knowledge?.Content ?? "no welcome message found");


    context.SkipResponse = true;

    //var response = await mafAgent.RunAsync(context);
    //await context.ReplyAsync(response);


});


var webhookWorkflow = xiansAgent.Workflows.DefineBuiltIn(name: "Webhook Workflow");
webhookWorkflow.OnWebhook((context) =>
{
    // Your webhook processing logic here
    Console.WriteLine($"Received: {context.Webhook.Name}");
    context.Respond(new { status = "success" });
});


// Start the agent and all workflows
await xiansAgent.RunAllAsync();
