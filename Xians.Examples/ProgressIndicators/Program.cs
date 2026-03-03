using Xians.Lib.Agents.Core;
using DotNetEnv;
using Microsoft.Extensions.Logging;

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL")
    ?? throw new InvalidOperationException("XIANS_SERVER_URL environment variable is not set");
var xiansAgentCertificate = Environment.GetEnvironmentVariable("XIANS_AGENT_CERTIFICATE")
    ?? throw new InvalidOperationException("XIANS_AGENT_CERTIFICATE environment variable is not set");
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set");

// Initialize Xians Platform with optional logging configuration
var xiansPlatform = await XiansPlatform.InitializeAsync(new()
{
    ServerUrl = serverUrl,
    ApiKey = xiansAgentCertificate,
    ConsoleLogLevel = LogLevel.Debug,
    ServerLogLevel = LogLevel.Warning
});

// Register a new agent with Xians
var xiansAgent = xiansPlatform.Agents.Register(new()
{
    Name = "Progress Indicators Agent",
    IsTemplate = true
});

// Create MAF agent instance
var mafAgent = new MafSubAgent(openAiApiKey);

// Define a built-in conversational workflow
var conversationalWorkflow = xiansAgent.Workflows.DefineSupervisor();

// Handle incoming user messages with MAF agent and progress indicators
conversationalWorkflow.OnUserChatMessage(async (context) =>
{
    await context.SendReasoningAsync("Connecting to AI assistant to generate a response.");

    // Run MAF agent - tools will emit SendToolExecAsync when invoked
    var response = await mafAgent.RunAsync(context);

    await context.ReplyAsync(response);
});

// Start the agent and all workflows
await xiansAgent.RunAllAsync();
