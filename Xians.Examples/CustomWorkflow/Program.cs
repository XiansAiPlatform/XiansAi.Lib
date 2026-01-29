using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Workflows.Models;
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
    Name = "System Order Manager Agent",
    Description = "An order management agent that automates the complete order lifecycle from submission to fulfillment. The agent processes customer orders, validates order details, and implements human-in-the-loop (HITL) approval workflows for high-value transactions exceeding $100. It features automated approval for standard orders, timeout handling for pending decisions, and flexible action options (approve, reject, hold) for manual review. The agent integrates conversational AI capabilities for customer interactions, supports custom workflow definitions, and provides supervisor and integrator workflows for comprehensive order orchestration. Built on the Xians platform with Temporal workflow engine, it ensures reliable, scalable, and transparent order processing with full audit trails and task management.",    
    Summary = "An intelligent order management agent that automates the complete order lifecycle from submission to fulfillment. ",
    Version = "1.0.0",
    Author = "99x",
    SystemScoped = true  // See important notes below
});

// Define a custom workflow
//var orderWorkflow = xiansAgent.Workflows.DefineCustom<OrderWorkflow>(new WorkflowOptions { Activable = true });

// // Define a custom workflow
// var scheduleWorkflow = xiansAgent.Workflows.DefineCustom<OrderExtractionWorkflow>(new WorkflowOptions { Activable = false });

// // A sub workflow that will be invoked by another. This will not be activated by default.
// var urlReaderWorkflow = xiansAgent.Workflows.DefineCustom<UrlReaderWorkflow>(new WorkflowOptions { Activable = false });

// var caseWorkflow = xiansAgent.Workflows.DefineCustom<CaseWorkflow>(new WorkflowOptions { Activable = true });

// Define a built-in workflow
var supervisorWorkflow = xiansAgent.Workflows.DefineSupervisor();

// Define a built-in workflow
var integratorWorkflow = xiansAgent.Workflows.DefineIntegrator();

// Create your MAF agent instance
var mafSubAgent = new MafSubAgent(openAiApiKey);

// Handle incoming user messages
supervisorWorkflow.OnUserChatMessage(async (context) =>
{
    var response = await mafSubAgent.RunAsync(context);
    await context.ReplyAsync(response);
});

// Start the agent and all workflows
await xiansAgent.RunAllAsync();
