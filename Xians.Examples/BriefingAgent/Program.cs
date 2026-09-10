using DotNetEnv;
using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Workflows.Models;

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL")
    ?? throw new InvalidOperationException("XIANS_SERVER_URL not found in environment variables");

var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY")
    ?? throw new InvalidOperationException("XIANS_API_KEY not found in environment variables");

var anthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
var modelName = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-4-6";
BriefingLlm? briefingLlm = string.IsNullOrWhiteSpace(anthropicApiKey)
    ? null
    : new BriefingLlm(anthropicApiKey, modelName);

var xiansPlatform = await XiansPlatform.InitializeAsync(new()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    ConsoleLogLevel = LogLevel.Information,
    ServerLogLevel = LogLevel.Information
});

var xiansAgent = xiansPlatform.Agents.Register(new()
{
    Name = "Briefing Agent",
    Description = "A conversational check-in agent for Agent Studio, Slack, and Teams. Replies to questions and sends scheduled briefings.",
    SamplePrompts =
    [
        "What's on your mind today?",
        "Summarize this as a bullet list",
        "Give me a two-column status table",
        "Explain GitHub vs Slack markdown"
    ],
    IsTemplate = false
});

var conversationalWorkflow = xiansAgent.Workflows.DefineSupervisor();

xiansAgent.Workflows
    .DefineCustom<CheckInWorkflow>(new WorkflowOptions { Activable = true })
    .AddActivity<CheckInActivities>();

conversationalWorkflow.OnUserChatMessage(async (context) =>
{
    await RememberCheckInSubscriberAsync(context);
    await EnsureCheckInWorkflowAsync();

    var channel = MarkdownFormattingPrompt.FromScope(context.Message.Scope);
    var markdown = briefingLlm is not null
        ? await briefingLlm.RunAsync(context)
        : "I can chat once `ANTHROPIC_API_KEY` is set in `.env`. Check-ins still go out on the schedule.";

    Console.WriteLine(
        $"Replying with {channel} markdown (scope={context.Message.Scope ?? "(none)"}, llm={briefingLlm is not null})");

    await context.ReplyAsync(markdown);
});

Console.WriteLine("Briefing Agent is running and connected to Agent Studio.");
Console.WriteLine("Open Agent Studio: http://localhost:3001");
Console.WriteLine("Replies use MarkdownFormattingPrompt only — the LLM text is sent as-is to Studio / Slack / Teams.");
Console.WriteLine("Proactive check-ins are sent as the Supervisor on a schedule only — not right after a user message.");
Console.WriteLine("Press Ctrl+C to stop.");

await xiansAgent.RunAllAsync();

static async Task RememberCheckInSubscriberAsync(UserMessageContext context)
{
    try
    {
        var metadata = context.Metadata;
        Console.WriteLine(
            $"Remembering check-in subscriber ParticipantId={context.Message.ParticipantId} Scope={context.Message.Scope} ThreadId={context.Message.ThreadId} Metadata={FormatMetadata(metadata)}");

        await CheckInActivities.RememberParticipantAsync(
            context.Message.ParticipantId,
            context.Message.Scope,
            context.Message.ThreadId,
            context.Message.Authorization,
            metadata);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.WriteLine($"Could not remember participant for proactive check-ins: {ex}");
    }
}

static string FormatMetadata(IReadOnlyDictionary<string, string>? metadata)
{
    if (metadata is null || metadata.Count == 0)
    {
        return "(none)";
    }

    return string.Join(", ", metadata.Select(pair => $"{pair.Key}={pair.Value}"));
}

static async Task EnsureCheckInWorkflowAsync()
{
    try
    {
        await XiansContext.Workflows.StartAsync<CheckInWorkflow>(
            [false],
            uniqueKey: CheckInActivities.WorkflowUniqueKey);
    }
    catch (WorkflowAlreadyStartedException)
    {
        // Already running under this activation — the schedule will keep it going.
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.WriteLine($"Could not start the check-in workflow: {ex}");
    }
}
