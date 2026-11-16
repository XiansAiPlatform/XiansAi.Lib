using DotNetEnv;
using Microsoft.Extensions.Logging;
using Xians.Examples.HITLAgent;
using Xians.Lib.Agents.Core;

Env.Load();

var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL")
    ?? throw new InvalidOperationException("XIANS_SERVER_URL environment variable is not set");

var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY")
    ?? throw new InvalidOperationException("XIANS_API_KEY environment variable is not set");

var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set");

var modelName = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";

var xiansPlatform = await XiansPlatform.InitializeAsync(new()
{
    ServerUrl = serverUrl,
    ApiKey = xiansApiKey,
    ConsoleLogLevel = LogLevel.Information,
    ServerLogLevel = LogLevel.Information
});

var hitlAgent = xiansPlatform.Agents.Register(new()
{
    Name = "HITL Agent",
    Description = "A conversational agent. Ask it to create a human-in-the-loop task and it will open one.",
    Summary = "Chat normally, or ask the agent to create a HITL task",
    SamplePrompts =
    [
        "What can you help me with?",
        "Create a HITL task to approve the Q4 budget",
        "Please ask someone to review the vendor contract"
    ],
    IsTemplate = true,
    EnableTasks = true
});

// Started by the CreateHitlTask tool. Not activable from the studio on its own.
hitlAgent.Workflows.DefineCustom<HitlTaskWorkflow>(new() { Activable = false });

var chatAgent = new ChatAgent(openAiApiKey, modelName);
var conversationalWorkflow = hitlAgent.Workflows.DefineSupervisor();
conversationalWorkflow.OnUserChatMessage(async (context) =>
{
    Console.WriteLine($"User: {context.Message.Text}");

    try
    {
        await context.ReplyAsync(await chatAgent.RunAsync(context));
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.WriteLine($"Error: {ex.Message}");
        await context.ReplyAsync($"I couldn't answer that: {ex.Message}");
    }
});

Console.WriteLine($"Registered agent: {hitlAgent.Name}");
Console.WriteLine($"Starting HITL Agent with model {modelName}...");
Console.WriteLine("Chat normally, or ask it to create a HITL task.");
Console.WriteLine("Press Ctrl+C to stop the agent.");

try
{
    await hitlAgent.RunAllAsync();
}
catch (OperationCanceledException)
{
    Console.WriteLine("HITL Agent stopped.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
