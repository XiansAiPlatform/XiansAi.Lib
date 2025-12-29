using DotNetEnv;
using Xians.Lib.Agents.Core;
using Xians.Agent.Sample;
using Xians.Agent.Sample.ConversationalAgent;
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
    SystemScoped = true
});

// Define a supervisor workflow to handle user messages and conversations
var conversationalWorkflow = agent.Workflows.DefineBuiltIn(name: "Conversational", workers: 1);

// Define a web workflow to handle web interactions
var webWorkflow = agent.Workflows.DefineBuiltIn(name: "Web", workers: 1);

// Define a company research workflow to handle company research
var companyResearchWorkflow = agent.Workflows.DefineCustom<CompanyResearchWorkflow>(workers: 1);
 
// Define a scheduled wash workflow that creates its own recurring schedule
// ScheduleActivities is automatically registered - no setup needed!
var scheduledWashWorkflow = agent.Workflows.DefineCustom<ScheduledWashWorkflow>(workers: 1);

// OPTION 1: Let the workflow create its own schedule (via system activity when it runs)
// - The workflow will call ScheduleActivities.CreateIntervalScheduleIfNotExists()
// - ScheduleActivities is a system activity - automatically registered with ALL workflows!
// - No manual setup or registration needed

// OPTION 2: Create the schedule directly using the Xians Schedule SDK (before workflow runs)
// Uncomment to create the schedule upfront:
// var washSchedule = await scheduledWashWorkflow.Schedules!
//     .Create("scheduled-wash-every-10sec")
//     .WithIntervalSchedule(TimeSpan.FromSeconds(10))
//     .WithInput($"wash-{DateTime.UtcNow:yyyyMMddHHmmss}")
//     .StartAsync();
// Console.WriteLine($"✅ Created wash schedule: {washSchedule.Id}");

// Register handler for conversational workflow
conversationalWorkflow.OnUserMessage(async (context) =>
{
    var response = await ConversationalAgent.ProcessMessageAsync(context, openAiApiKey);
    await context.ReplyAsync(response);
});

// Register handler for web workflow
webWorkflow.OnUserMessage(async (context) =>
{
    var response = await WebAgent.ProcessMessageAsync(context, openAiApiKey);
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