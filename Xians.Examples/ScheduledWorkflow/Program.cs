using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;
using DotNetEnv;
using Xians.Examples.CustomWorkflow;
using Microsoft.Extensions.Logging;

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL") 
    ?? throw new InvalidOperationException("XIANS_SERVER_URL environment variable is not set"); 
var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY") 
    ?? throw new InvalidOperationException("XIANS_API_KEY environment variable is not set");
// Get OpenAI API key (replace with your actual key or use environment variable)
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set");

// Initialize Xians Platform
var xiansPlatform = await XiansPlatform.InitializeAsync(new ()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    ServerLogLevel = LogLevel.Debug,
});

// Register a new agent with Xians
var xiansAgent = xiansPlatform.Agents.Register(new ()
{
    Name = "Scheduled Workflow Agent",
    Description = "A scheduled workflow agent that can schedule workflows to run at a specific time. It uses the scheduled workflow workflow to schedule workflows to run at a specific time.",
    Summary = "A scheduled workflow agent that can schedule workflows to run at a specific time.",
    Version = "1.0.0",
    Author = "99x",
    IsTemplate = true
});

// Define a custom workflow
var orderWorkflow = xiansAgent.Workflows.DefineCustom<OrderExtractionWorkflow>(new WorkflowOptions { Activable = true });

// Start the agent and all workflows
await xiansAgent.RunAllAsync();
