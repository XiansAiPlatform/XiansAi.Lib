using Microsoft.Agents.AI;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Workflows.Messaging.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Xians.Lib.Agents.Core;
using Xians.Agent.Sample.Utils;
using Xians.Agent.Sample.SupervisorAgent;

namespace Xians.Agent.Sample.ConversationalAgent;

/// <summary>
/// MAF Agent that uses OpenAI with Xians chat message store for conversation history.
/// </summary>
internal static class ConversationalAgent
{

    /// <summary>
    /// Processes a user message using OpenAI's chat model with Xians conversation history.
    /// </summary>
    /// <param name="context">The Xians user message context containing the message and chat history</param>
    /// <param name="openAiApiKey">OpenAI API key for authentication</param>
    /// <param name="modelName">OpenAI model to use (defaults to gpt-4o-mini)</param>
    /// <returns>The AI agent's response text</returns>
    public static async Task<string> ProcessMessageAsync(
        UserMessageContext context,
        string openAiApiKey,
        string modelName = "gpt-4o-mini")
    {
        // Create AI agent with custom Xians chat message store and tools
        AIAgent mafAgent = new OpenAIClient(openAiApiKey)
            .GetChatClient(modelName)
            .CreateAIAgent(new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions
                {
                    Instructions = "You are a helpful assistant.",
                    Tools =
                    [
                        AIFunctionFactory.Create(SupervisorAgentTools.ResearchCompany)
                    ]
                },
                ChatMessageStoreFactory = ctx =>
                {
                    // Create a new chat message store that reads from Xians platform
                    return new XiansChatMessageStore(
                        context,
                        ctx.SerializedState,
                        ctx.JsonSerializerOptions);
                }
            });

        var response = await mafAgent.RunAsync(context.Message.Text);
        return response.Text;
    }
}

