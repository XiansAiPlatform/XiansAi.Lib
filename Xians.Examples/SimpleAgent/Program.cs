using Xians.Lib.Agents.Core;
using DotNetEnv;

Env.Load();

// Get OpenAI API key from environment variable
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set");

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
    Name = "My Simple Agent",
    SystemScoped = false  // See important notes below
});

// Define a built-in conversational workflow
var conversationalWorkflow = xiansAgent.Workflows.DefineBuiltIn(name: "Conversing Workflow");

// Create your MAF agent instance
var mafAgent = new MafSubAgent(openAiApiKey);

// Handle incoming user messages
conversationalWorkflow.OnUserChatMessage(async (context) =>
{
    var response = await mafAgent.RunAsync(context);
    await context.ReplyAsync(response);
});

// Start the agent and all workflows
await xiansAgent.RunAllAsync();
