using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Xians.Lib.Agents;
using Xians.Agent.Sample.Utils;

namespace Xians.Agent.Sample.WebAgent;

/// <summary>
/// MAF Agent that uses OpenAI with Xians chat message store for conversation history.
/// </summary>
internal static class WebAgent
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
        var instructions = await context.GetKnowledgeAsync("Web Agent Instructions");

        if (instructions == null)
        {
            throw new Exception("'Web Agent Instructions' knowledge not found. Please set the instructions on the Xians portal.");
        }

        // Create AI agent with custom Xians chat message store and tools
        AIAgent mafAgent = new OpenAIClient(openAiApiKey)
            .GetChatClient(modelName)
            .CreateAIAgent(new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions
                {
                    Instructions = instructions.Content,
                    Tools =
                    [
                        AIFunctionFactory.Create(GoogleSearchCapability.WebSearch),
                        AIFunctionFactory.Create(FirecrawlCapability.WebScrape),
                        AIFunctionFactory.Create(FirecrawlCapability.ScrapeLinksFromWebpage),
                        AIFunctionFactory.Create(FirecrawlCapability.ExtractDataFromWebpage)
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

