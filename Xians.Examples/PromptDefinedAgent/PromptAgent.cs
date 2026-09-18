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

    public PromptAgent(string apiKey)
    {
        _chatClient = new OpenAIClient(apiKey).GetChatClient("gpt-4o-mini");
    }

    public Task<string> RunAsync(UserMessageContext context) => RunAsync(context.Message.Text, context);

    public Task<string> RunScheduledAsync(string prompt) => RunAsync(prompt, null);

    private async Task<string> RunAsync(string prompt, UserMessageContext? context)
    {
        var configuredPrompt = await XiansContext.CurrentAgent.Knowledge.GetAsync("system-prompt");
        var rules = await RulesConfig.LoadAsync();
        await using var mcpTools = await McpToolProvider.LoadAsync(rules);
        var tools = new List<AITool>(mcpTools.Tools);

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

}
