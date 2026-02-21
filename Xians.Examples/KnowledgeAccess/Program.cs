using Xians.Lib.Agents.Core;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Knowledge;  // For UploadEmbeddedResourceAsync extension

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL") 
    ?? throw new InvalidOperationException("XIANS_SERVER_URL environment variable is not set"); 
var xiansAgentCertificate = Environment.GetEnvironmentVariable("XIANS_AGENT_CERTIFICATE") 
    ?? throw new InvalidOperationException("XIANS_AGENT_CERTIFICATE environment variable is not set");

// Initialize Xians Platform with optional logging configuration
var xiansPlatform = await XiansPlatform.InitializeAsync(new ()
{
    ServerUrl = serverUrl,
     ApiKey = xiansAgentCertificate,
    // Optional: Configure log levels programmatically (overrides environment variables)
    ConsoleLogLevel = LogLevel.Debug,  // What shows in console
    ServerLogLevel = LogLevel.Warning         // What gets uploaded to server
});

// Register a new agent with Xians
var xiansAgent = xiansPlatform.Agents.Register(new ()
{
    Name = "Knowledge Access Agent",
    IsTemplate = true  
});

// Upload embedded knowledge resources
await xiansAgent.Knowledge.UploadEmbeddedResourceAsync(
    resourcePath: "knowledge/system-prompt.md",
    knowledgeName: "system prompt",
    knowledgeType: "markdown"
);


// Define a built-in conversational workflow
var conversationalWorkflow = xiansAgent.Workflows.DefineSupervisor();

// Handle incoming user messages
conversationalWorkflow.OnUserChatMessage(async (context) =>
{
    var knowledge = await XiansContext.CurrentAgent.Knowledge.GetAsync("system prompt");

    await context.ReplyAsync("system prompt:" + (knowledge?.Content ?? "no system prompt found"));


    context.SkipResponse = true;
});

// Start the agent and all workflows
await xiansAgent.RunAllAsync();
