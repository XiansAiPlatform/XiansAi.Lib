using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Xians.Lib.Agents.Messaging;

namespace Xians.Examples.HITLAgent;

/// <summary>
/// Conversational agent. It answers normally, and calls <see cref="HitlTaskTools.CreateHitlTask"/>
/// only when the user asks for a human-in-the-loop task.
/// </summary>
public class ChatAgent
{
    private const string Instructions =
        """
        You are a helpful assistant. Reply to the user in a normal conversation.
        When the user asks you to create a human-in-the-loop task, an approval, or a review for a person to decide, call the CreateHitlTask tool.
        Do not call that tool for ordinary chat. After the tool succeeds, confirm what you opened in plain language and say they will hear back when someone completes it.
        If the tool returns an error, explain it and ask for anything that is missing.
        """;

    private readonly ChatClient _chatClient;

    public ChatAgent(string openAiApiKey, string modelName = "gpt-4o-mini")
    {
        _chatClient = new OpenAIClient(openAiApiKey).GetChatClient(modelName);
    }

    public async Task<string> RunAsync(UserMessageContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Message.Text))
        {
            return "I didn't receive any message. Please send a message.";
        }

        var tools = new HitlTaskTools(context);
        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = Instructions,
                Tools = [AIFunctionFactory.Create(tools.CreateHitlTask)]
            },
            ChatMessageStoreFactory = _ => new XiansChatMessageStore(context)
        });

        var response = await agent.RunAsync(context.Message.Text);
        return response.Text;
    }
}
