using DotNetEnv;
using Xians.Lib.Agents;
using Xians.Agent.Sample;

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL");
var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY");
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(xiansApiKey) || string.IsNullOrEmpty(openAiApiKey))
{
    Console.WriteLine("XIANS_SERVER_URL or XIANS_API_KEY or OPENAI_API_KEY is not set");
    return;
}

// Initialize Xians platform
var xiansPlatform = await XiansPlatform.InitializeAsync(new XiansOptions
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey
    // TenantId is automatically extracted from the ApiKey certificate
});

// Generate unique agent name to avoid conflicts
var agentName = $"XiansTestAgent V3";

// Register agent and define workflow
var agent = xiansPlatform.Agents.Register(new XiansAgentRegistration
{
    Name = agentName,
    SystemScoped = true
});

// Define a default workflow  to handle super user messages
var workflowA = await agent.Workflows.DefineBuiltIn(name: "Conversational", workers: 1);

// Define another default workflow to handle webhook messages
var workflowB = await agent.Workflows.DefineBuiltIn(name: "Webhooks", workers: 1);

// Define custom workflow
var customWorkflow = await agent.Workflows.DefineCustom<CustomWorkflow>(workers: 1);

// Register handler for workflowA
workflowA.OnUserMessage(async (context) =>
{
    var response = await MafAgent.ProcessMessageAsync(context, openAiApiKey);
    await context.ReplyAsync(response);
});

// Register handler for workflowB using Semantic Kernel agent
workflowB.OnUserMessage(async (context) =>
{
    var response = await SkAgent.ProcessMessageAsync(context, openAiApiKey);
    await context.ReplyAsync(response);
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