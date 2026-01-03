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

        // Create tools instance
        var tools = new MafSubAgentTools(context);

        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = "You are a helpful assistant.",
                Tools =
                [
                    AIFunctionFactory.Create(tools.GetCurrentDateTime),
                    AIFunctionFactory.Create(tools.GetTargetMarketDescription)
                ]
            },
            ChatMessageStoreFactory = ctx => new XiansChatMessageStore(context)
        });

        var response = await agent.RunAsync(context.Message.Text);
        return response.Text;
    }
}

/*
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Xians.Lib.Agents.Messaging;
using Xians.SimpleAgent.Utils;

namespace Xians.SimpleAgent;

/// <summary>
/// Simple MAF Agent that uses OpenAI with conversation history from Xians.
/// </summary>
public class MafAgent
{
    private readonly ChatClient _chatClient;

    public MafAgent(string openAiApiKey, string modelName = "gpt-4o-mini")
    {
        _chatClient = new OpenAIClient(openAiApiKey).GetChatClient(modelName);
    }

    public async Task<string> RunAsync(UserMessageContext context)
    {
        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = "You are a helpful assistant."
            },
            ChatMessageStoreFactory = ctx => new XiansChatMessageStore(
                context,
                ctx.SerializedState,
                ctx.JsonSerializerOptions)
        });

        var response = await agent.RunAsync(context.Message.Text);
        return response.Text;
    }
}
*/