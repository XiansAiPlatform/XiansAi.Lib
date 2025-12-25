#pragma warning disable SKEXP0110 // Semantic Kernel Agents are experimental

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Xians.Lib.Agents;

namespace Xians.Agent.Sample;

/// <summary>
/// Semantic Kernel Agent that uses OpenAI with Xians chat message store for conversation history.
/// </summary>
internal static class SkAgent
{
    /// <summary>
    /// Processes a user message using Semantic Kernel's ChatCompletionAgent with Xians conversation history.
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
        // Create kernel with OpenAI chat completion
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: modelName,
            apiKey: openAiApiKey);
        
        Kernel kernel = builder.Build();

        // Get chat completion service
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        // Get chat history from Xians and convert to SK format
        var chatHistory = await ConvertXiansHistoryToSKAsync(context);

        var knowledge = await context.GetKnowledgeAsync("Invoice Approval Instructions");

        Console.WriteLine(knowledge);
        
        // Add system message with instructions
        chatHistory.Insert(0, new ChatMessageContent(AuthorRole.System, knowledge?.Content ?? "You are a helpful assistant"));
        Console.WriteLine($"[SkAgent] System message added: {knowledge?.Content}");
        // Add current user message
        chatHistory.AddUserMessage(context.Message.Text);

        Console.WriteLine($"[SkAgent] Sending {chatHistory.Count} messages to SK (including system message and current user message)");

        // Get response from chat completion
        var response = await chatCompletionService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings: new OpenAIPromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            },
            kernel: kernel);

        return response.Content ?? "";
    }

    /// <summary>
    /// Converts Xians chat history to Semantic Kernel ChatHistory format.
    /// </summary>
    private static async Task<ChatHistory> ConvertXiansHistoryToSKAsync(UserMessageContext context)
    {
        var chatHistory = new ChatHistory();
        
        // Retrieve chat history from Xians
        var xiansMessages = await context.GetChatHistoryAsync(page: 1, pageSize: 10);

        Console.WriteLine($"[SkAgent] Xians messages retrieved: {xiansMessages.Count}");
        
        // Reverse to get chronological order (oldest to newest)
        var messagesInOrder = xiansMessages.AsEnumerable().Reverse();
        
        int messageIndex = 0;
        foreach (var msg in messagesInOrder)
        {            
            if (string.IsNullOrEmpty(msg.Text))
            {
                messageIndex++;
                continue;
            }

            // Determine the role based on message direction
            var direction = msg.Direction.ToLowerInvariant();
            if (direction == "outgoing" || direction == "outbound")
            {
                chatHistory.AddAssistantMessage(msg.Text);
            }
            else if (direction == "incoming" || direction == "inbound")
            {
                chatHistory.AddUserMessage(msg.Text);
            }
            else
            {
                chatHistory.AddUserMessage(msg.Text);
            }
            messageIndex++;
        }

        Console.WriteLine($"[SkAgent] Total history messages added to ChatHistory: {chatHistory.Count}");
        return chatHistory;
    }
}

