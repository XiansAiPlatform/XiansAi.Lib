using DotNetEnv;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Knowledge;

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

var promptAgent = new PromptAgent(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? throw new InvalidOperationException("OPENAI_API_KEY is not set"),
    Environment.GetEnvironmentVariable("TAVILY_API_KEY")
        ?? throw new InvalidOperationException("TAVILY_API_KEY is not set"));

var workflow = agent.Workflows.DefineSupervisor();
workflow.OnUserChatMessage(async context =>
{
    await context.ReplyAsync(await promptAgent.RunAsync(context));
    context.SkipResponse = true;
});

await agent.RunAllAsync();
