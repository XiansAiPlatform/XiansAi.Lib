using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Xians.Lib.Agents.Messaging;

public class MafSubAgent
{
    private readonly ChatClient _chatClient;

    public MafSubAgent(string openAiApiKey, string modelName = "gpt-4o-mini")
    {
        _chatClient = new OpenAIClient(openAiApiKey).GetChatClient(modelName);
    }

    public async Task<string> RunAsync(UserMessageContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Message.Text))
        {
            return "I didn't receive any message. Please send a message.";
        }

        var tools = new MafSubAgentTools(context);

        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = "You are a helpful assistant. Use the available tools when relevant.",
                Tools =
                [
                    AIFunctionFactory.Create(tools.GetCurrentDateTime),
                    AIFunctionFactory.Create(tools.GetWeatherInfo)
                ]
            },
            ChatMessageStoreFactory = ctx => new XiansChatMessageStore(context)
        });
        await context.SendReasoningAsync("Analyzing the user's question to identify the core requirements...");
        var response = await Tracker.StreamAgentAndReturnTextAsync(agent.RunStreamingAsync(context.Message.Text), context);
        return response;
    }
}
