using Xians.Lib.Agents.Core;
using DotNetEnv;
using Xians.Examples.CustomWorkflow;

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL") 
    ?? throw new InvalidOperationException("XIANS_SERVER_URL environment variable is not set"); 
var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY") 
    ?? throw new InvalidOperationException("XIANS_API_KEY environment variable is not set");

// Initialize Xians Platform
var xiansPlatform = await XiansPlatform.InitializeAsync(new ()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey
});

// Register a new agent with Xians
var xiansAgent = xiansPlatform.Agents.Register(new ()
{
    Name = "Order Manager Agent",
    Description = "An agent that manages orders",
    Summary = "Manages orders from submission to completion",
    Version = "1.0.0",
    Author = "99x",
    SystemScoped = false  // See important notes below
});

// Define a custom workflow
var orderWorkflow = xiansAgent.Workflows.DefineCustom<OrderWorkflow>(new (){ Activable = true });

// Define a custom workflow
var scheduleWorkflow = xiansAgent.Workflows.DefineCustom<ScheduleWorkflow>(new (){ Activable = true });

// Define a built-in workflow
var builtInWorkflow = xiansAgent.Workflows.DefineBuiltIn("Conversational Workflow");

// Start the agent and all workflows
await xiansAgent.RunAllAsync();
