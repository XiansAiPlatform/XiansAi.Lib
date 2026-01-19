using Xians.Lib.Agents.Core;
using DotNetEnv;
using Xians.Examples.CustomWorkflow;

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
var scheduleWorkflow = xiansAgent.Workflows.DefineCustom<OrderExtractionWorkflow>(new (){ Activable = true });

// A sub workflow that will be invoked by another. This will not be activated by default.
var urlReaderWorkflow = xiansAgent.Workflows.DefineCustom<UrlReaderWorkflow>(new (){ Activable = false });

// Define a built-in workflow
var conversationalWorkflow = xiansAgent.Workflows.DefineBuiltIn("Conversational Workflow");

// Create your MAF agent instance
var mafSubAgent = new MafSubAgent(openAiApiKey);

// Handle incoming user messages
conversationalWorkflow.OnUserChatMessage(async (context) =>
{
    var response = await mafSubAgent.RunAsync(context);
    await context.ReplyAsync(response);
});

// Start the agent and all workflows
await xiansAgent.RunAllAsync();
