
// using Microsoft.Agents.AI;
// using Microsoft.Extensions.AI;
// using OpenAI;
// using OpenAI.Chat;
// using Xians.Lib.Agents.Core;
// using Xians.Lib.Agents.Messaging;

// public class MafSubAgent
// {
//     private readonly ChatClient _chatClient;
//     public MafSubAgent(string openAiApiKey, string modelName = "gpt-4o-mini")
//     {
//         _chatClient = new OpenAIClient(openAiApiKey).GetChatClient(modelName);
//     }

//     private async Task<string> GetSystemPromptAsync(UserMessageContext context)
//     {
//         // You need to create a KnowledgeItem with the name "System Prompt" in the Xians platform.
//         var systemPrompt = await XiansContext.CurrentAgent.Knowledge.GetAsync("System Prompt");
//         return systemPrompt?.Content ?? "You are a helpful assistant.";
//     }

//     public async Task<string> RunAsync(UserMessageContext context)
//     {
//         if (string.IsNullOrWhiteSpace(context.Message.Text))
//         {
//             return "I didn't receive any message. Please send a message.";
//         }

//         // Create tools instance with the UserMessageContext
//         var tools = new MafSubAgentTools(context);

//         // Configure the AI agent with tools
//         var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
//         {
//             ChatOptions = new ChatOptions
//             {
//                 Instructions = await GetSystemPromptAsync(context),
//                 Tools =
//                 [
//                     AIFunctionFactory.Create(tools.GetCurrentDateTime),
//                     AIFunctionFactory.Create(tools.GetTargetMarketDescription)
//                 ]
//             },
//             // Use Xians chat message store for conversation history
//             ChatMessageStoreFactory = ctx => new XiansChatMessageStore(context)
//         });

//         // Run the agent and return the response
//         var response = await agent.RunAsync(context.Message.Text);
//         return response.Text;
//     }
// }

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Messaging;

public class MafSubAgent
{
    private readonly ChatClient _chatClient;
    public MafSubAgent(string openAiApiKey, string modelName = "gpt-4o-mini")
    {
        _chatClient = new OpenAIClient(openAiApiKey).GetChatClient(modelName);
    }

    private async Task<string> GetSystemPromptAsync(UserMessageContext context)
    {
        // You need to create a KnowledgeItem with the name "System Prompt" in the Xians platform.
        var systemPrompt = await XiansContext.CurrentAgent.Knowledge.GetAsync("System Prompt");
        return systemPrompt?.Content ?? "You are a helpful assistant.";
    }

    public async Task<string> RunAsync(UserMessageContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Message.Text))
        {
            return "I didn't receive any message. Please send a message.";
        }

        // Create tools instance with the UserMessageContext
        var tools = new MafSubAgentTools(context);

        // Configure the AI agent with tools
        var agent = _chatClient.CreateAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = await GetSystemPromptAsync(context),
                Tools =
                [
                    AIFunctionFactory.Create(tools.GetCurrentDateTime),
                    AIFunctionFactory.Create(tools.GetOrderData)
                ]
            },
            // Use Xians chat message store for conversation history
            ChatMessageStoreFactory = ctx => new XiansChatMessageStore(context)
        });

        // Run the agent and return the response
        var response = await agent.RunAsync(context.Message.Text);
        return response.Text;
    }
}