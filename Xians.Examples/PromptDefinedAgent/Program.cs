using DotNetEnv;
using PromptDefinedAgent.Scheduling;
using PromptDefinedAgent.Webhooks;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge;
using Xians.Lib.Agents.Messaging;

Env.Load();

var platform = await XiansPlatform.InitializeAsync(new()
{
    ServerUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL")
        ?? throw new InvalidOperationException("XIANS_SERVER_URL is not set"),
    ApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY")
        ?? throw new InvalidOperationException("XIANS_API_KEY is not set")
});

var agent = platform.Agents.Register(new()
{
    Name = "Prompt Defined Agent",
    IsTemplate = true,
    SamplePrompts = ["Research the latest developments in renewable energy"]
});

await agent.Knowledge.UploadEmbeddedResourceAsync(
    "knowledge/system-prompt.md",
    "system-prompt",
    "markdown");

await agent.Knowledge.UploadEmbeddedResourceAsync(
    "knowledge/rules.json",
    "Rules",
    "json");

await using var promptAgent = new PromptAgent(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? throw new InvalidOperationException("OPENAI_API_KEY is not set"));

var workflow = agent.Workflows.DefineSupervisor();
workflow.OnUserChatMessage(async context =>
{
    await context.ReplyAsync(await promptAgent.RunAsync(context));
    context.SkipResponse = true;
});

agent.Workflows.DefineCustom<ScheduledPromptWorkflow>(new() { Activable = false })
    .AddActivity(new ScheduledPromptActivities(promptAgent));

agent.Workflows.DefineBuiltIn("Prompt Webhook Workflow").OnWebhook(async context =>
{
    try
    {
        context.Respond(new { result = await promptAgent.RunWebhookAsync(context) });
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Prompt webhook failed: {exception.GetType().Name}");
        context.Response = WebhookResponse.InternalServerError("Prompt webhook execution failed.");
    }
});

await agent.RunAllAsync();
