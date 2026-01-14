using DotNetEnv;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge;
using Xians.Agent.Sample;
using Xians.Agent.Sample.WebAgent;

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
var agentName = Constants.AgentName;

// Register agent and define workflow
var agent = xiansPlatform.Agents.Register(new XiansAgentRegistration
{
    Name = agentName,
    Description= "A lead discovery agent that can discover leads from a given company. It uses the content processing and content discovery workflows to discover leads.",
    Summary= "Discovers leads from companies using content processing workflows",
    Version= "1.0.0",
    Author= "99x",
    SystemScoped = true
});

// Upload embedded knowledge resources to the server
await agent.Knowledge.UploadEmbeddedResourceAsync(
    resourcePath: "WebAgent/web-agent-prompt.md",
    knowledgeName: "web-agent-prompt",
    knowledgeType: "markdown"
);

// Define a content processing workflow to handle content processing
var contentProcessingWorkflow = agent.Workflows.DefineCustom<ContentProcessingWorkflow>();
 
// Define a content discovery workflow to handle content discovery
var contentDiscoveryWorkflow = agent.Workflows.DefineCustom<ContentDiscoveryWorkflow>();

// Define a conversational workflow to handle user messages and conversations
var conversationalWorkflow = agent.Workflows.DefineBuiltIn(name: Constants.ConversationalWorkflowName);
var conversationalAgent = new ConversationalAgent(openAiApiKey);
conversationalWorkflow.OnUserChatMessage(async (context) =>
{
    try {
        var response = await conversationalAgent.ProcessMessageAsync(context);
        await context.ReplyAsync(response);
    } catch (Exception ex) {
        Console.WriteLine($"Error processing conversational request: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        await context.ReplyAsync($"Error processing conversational request: {ex.Message}");
    }
});

// Register handler for web workflow
var webWorkflow = agent.Workflows.DefineBuiltIn(name: Constants.WebWorkflowName);
webWorkflow.OnUserChatMessage(async (context) =>
{
    try {
        var response = await WebAgent.ProcessMessageAsync(context, openAiApiKey);
        await context.ReplyAsync(response);
    } catch (Exception ex) {
        Console.WriteLine($"Error processing web request: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        await context.ReplyAsync($"Error processing web request: {ex.Message}");
    }

});

// Optional: Enable human-in-the-loop (HITL) tasks
await agent.Workflows.WithTasks();  // Uses default max concurrent (100)

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