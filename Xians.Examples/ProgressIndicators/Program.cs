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
    Name = "Progress Indicators Agent",
    IsTemplate = true  
});


// Define a built-in conversational workflow
var conversationalWorkflow = xiansAgent.Workflows.DefineSupervisor();

// Handle incoming user messages
conversationalWorkflow.OnUserChatMessage(async (context) =>
{
    await context.SendReasoningAsync("Analyzing the user's question to identify the core requirements...");
    await Task.Delay(1800);

    await context.SendReasoningAsync("Breaking down the problem into logical steps for a structured response.");
    await Task.Delay(1200);

    await context.SendToolExecAsync("search_knowledge_base(query=\"best practices\")");
    await Task.Delay(2400);

    await context.SendReasoningAsync("Synthesizing findings with relevant examples to provide a comprehensive answer.");
    await Task.Delay(1500);

    await context.SendToolExecAsync("format_response(template=\"user_friendly\")");
    await Task.Delay(900);

    await context.ReplyAsync("Here's my analysis: I've broken down your question into key components and researched the relevant best practices. The main points are: (1) Start with clear requirements, (2) Follow the established patterns in the codebase, and (3) Include error handling from the outset. Would you like me to elaborate on any specific aspect?");
});

// Start the agent and all workflows  
await xiansAgent.RunAllAsync();
