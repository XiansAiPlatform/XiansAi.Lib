using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using PromptDefinedAgent.Configuration;
using PromptDefinedAgent.Mcp;
using PromptDefinedAgent.Messaging;
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

    public Task<string> RunAsync(UserMessageContext context) =>
        RunAsync(context.Message.Text, context, (data, text) => context.SendToolExecAsync(data, text));

    public Task<string> RunScheduledAsync(ScheduledPromptRequest request) =>
        RunAsync(request.ToAgentPrompt(), null, (data, text) =>
            XiansContext.Messaging.SendToolCallMessageAsSupervisorAsync(
                text, data, scope: request.Scope, participantId: request.ParticipantId));

    private async Task<string> RunAsync(string prompt, UserMessageContext? context, Func<object, string, Task> sendToolEvent)
    {
        var configuredPrompt = await XiansContext.CurrentAgent.Knowledge.GetAsync("system-prompt");
        var rules = await RulesConfig.LoadAsync();
        await using var mcpTools = await McpToolProvider.LoadAsync(rules);
        var instructions = configuredPrompt?.Content ?? "You are a helpful assistant.";
        if (context is not null) instructions += "\n\n" + SchedulingInstructions.ForChat(context);

        var options = new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = instructions,
                Tools = new List<AITool>(mcpTools.Tools)
            }
        };
        if (context is not null) options.ChatMessageStoreFactory = _ => new ConversationStore(context);
        var agent = _chatClient.CreateAIAgent(options);

        return await ToolCallTracker.RunAsync(agent.RunStreamingAsync(prompt), sendToolEvent);
    }

}
