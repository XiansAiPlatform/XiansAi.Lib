using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using PromptDefinedAgent.Configuration;
using PromptDefinedAgent.Mcp;
using PromptDefinedAgent.Scheduling;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;

public sealed class PromptAgent
{
    private readonly ChatClient _chatClient;
    private readonly WebTools _webTools;

    public PromptAgent(string apiKey, string webSearchApiKey, string model = "gpt-4o-mini")
    {
        _chatClient = new OpenAIClient(apiKey).GetChatClient(model);
        _webTools = new WebTools(webSearchApiKey);
    }

    public Task<string> RunAsync(UserMessageContext context) => RunAsync(context.Message.Text, context);

    public Task<string> RunScheduledAsync(string prompt) => RunAsync(prompt, null);

    private async Task<string> RunAsync(string prompt, UserMessageContext? context)
    {
        var configuredPrompt = await XiansContext.CurrentAgent.Knowledge.GetAsync("system-prompt");
        var rules = await RulesConfig.LoadAsync();
        await using var mcpTools = await McpToolProvider.LoadAsync(rules);
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(GetCurrentDateTime),
            AIFunctionFactory.Create(_webTools.SearchWeb),
            AIFunctionFactory.Create(_webTools.ReadWebPage)
        };
        tools.AddRange(mcpTools.Tools);

        if (context is not null)
        {
            var scheduleTools = new ScheduleTools(context);
            tools.Add(AIFunctionFactory.Create(scheduleTools.CreateSchedule));
            tools.Add(AIFunctionFactory.Create(scheduleTools.ListSchedules));
            tools.Add(AIFunctionFactory.Create(scheduleTools.UpdateScheduleTiming));
            tools.Add(AIFunctionFactory.Create(scheduleTools.DeleteSchedule));
        }

        var options = new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = configuredPrompt?.Content ?? "You are a helpful assistant.",
                Tools = tools
            }
        };
        if (context is not null) options.ChatMessageStoreFactory = _ => new ConversationStore(context);
        var agent = _chatClient.CreateAIAgent(options);

        return (await agent.RunAsync(prompt)).Text;
    }

    private static string GetCurrentDateTime() => DateTimeOffset.Now.ToString("O");
}
