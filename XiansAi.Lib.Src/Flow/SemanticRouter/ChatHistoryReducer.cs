using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;

namespace XiansAi.Flow.Router;

/// <summary>
/// Provides chat history reduction strategies to keep conversations within token limits.
/// Implements truncation with optional summarization to preserve important context.
/// </summary>
public class ChatHistoryReducer
{
    private readonly ILogger<ChatHistoryReducer> _logger;
    private readonly IChatCompletionService? _chatService;
    private readonly RouterOptions _options;
    
    /// <summary>
    /// Initializes a new instance of the ChatHistoryReducer class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output</param>
    /// <param name="options">Router options containing token limits</param>
    /// <param name="chatService">Optional chat service for summarization. If null, only truncation is available.</param>
    public ChatHistoryReducer(
        ILogger<ChatHistoryReducer> logger,
        RouterOptions options,
        IChatCompletionService? chatService = null)
    {
        _logger = logger;
        _options = options;
        _chatService = chatService;
    }

    /// <summary>
    /// Reduces chat history to stay within token limits using the configured strategy.
    /// Always preserves system messages and prioritizes recent messages.
    /// </summary>
    /// <param name="chatHistory">The chat history to reduce</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reduced chat history or original if within limits</returns>
    public async Task<ChatHistory> ReduceAsync(
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default)
    {
        if (chatHistory.Count == 0)
        {
            return chatHistory;
        }

        // Estimate current token count
        var currentTokenCount = EstimateTokenCount(chatHistory);
        
        _logger.LogDebug("Current chat history token count: {TokenCount}, Limit: {TokenLimit}", 
            currentTokenCount, _options.TokenLimit);

        // If within limits, return as-is
        if (currentTokenCount <= _options.TokenLimit)
        {
            return chatHistory;
        }

        _logger.LogWarning("Chat history exceeds token limit ({Current} > {Limit}). Reducing...", 
            currentTokenCount, _options.TokenLimit);

        // First, try truncating large function results (this is often the main culprit)
        var historyWithTruncatedFunctions = TruncateLargeFunctionResults(chatHistory, _options.MaxTokensPerFunctionResult);
        var tokensAfterFunctionTruncation = EstimateTokenCount(historyWithTruncatedFunctions);
        
        _logger.LogDebug("After function result truncation: {TokenCount} tokens", tokensAfterFunctionTruncation);

        // If still over limit, perform message truncation
        ChatHistory finalHistory;
        if (tokensAfterFunctionTruncation > _options.TokenLimit)
        {
            finalHistory = await ReduceWithTruncationAsync(
                historyWithTruncatedFunctions, 
                _options.TargetTokenCount,
                cancellationToken);
        }
        else
        {
            finalHistory = historyWithTruncatedFunctions;
        }

        var newTokenCount = EstimateTokenCount(finalHistory);
        _logger.LogInformation("Reduced chat history from {Original} to {Reduced} tokens", 
            currentTokenCount, newTokenCount);

        return finalHistory;
    }

    /// <summary>
    /// Reduces chat history by truncating older messages while preserving system messages.
    /// Optionally summarizes truncated messages if a chat service is available.
    /// </summary>
    private async Task<ChatHistory> ReduceWithTruncationAsync(
        ChatHistory chatHistory,
        int targetTokenCount,
        CancellationToken cancellationToken)
    {
        var reducedHistory = new ChatHistory();
        var systemMessages = new List<ChatMessageContent>();
        var conversationMessages = new List<ChatMessageContent>();
        
        // Separate system messages from conversation
        foreach (var message in chatHistory)
        {
            if (message.Role == AuthorRole.System)
            {
                systemMessages.Add(message);
            }
            else
            {
                conversationMessages.Add(message);
            }
        }

        // Always preserve system messages
        foreach (var systemMessage in systemMessages)
        {
            reducedHistory.Add(systemMessage);
        }

        // Calculate tokens available for conversation
        var systemTokens = systemMessages.Sum(m => EstimateTokenCount(m));
        var availableTokens = targetTokenCount - systemTokens;
        
        if (availableTokens <= 0)
        {
            _logger.LogError("System messages alone exceed target token count");
            return reducedHistory;
        }

        // Find how many recent messages we can keep
        var messagesToKeep = new List<ChatMessageContent>();
        var currentTokens = 0;
        
        // Iterate from most recent to oldest
        for (int i = conversationMessages.Count - 1; i >= 0; i--)
        {
            var messageTokens = EstimateTokenCount(conversationMessages[i]);
            if (currentTokens + messageTokens > availableTokens)
            {
                break;
            }
            
            messagesToKeep.Insert(0, conversationMessages[i]);
            currentTokens += messageTokens;
        }

        // Calculate how many messages were truncated
        var truncatedCount = conversationMessages.Count - messagesToKeep.Count;
        
        // If we have a chat service and messages were truncated, try to summarize them
        if (_chatService != null && truncatedCount > 0)
        {
            var truncatedMessages = conversationMessages.Take(truncatedCount).ToList();
            var summary = await SummarizeTruncatedMessagesAsync(
                truncatedMessages, 
                cancellationToken);
            
            if (!string.IsNullOrEmpty(summary))
            {
                // Add summary as a system message to preserve context
                reducedHistory.Add(new ChatMessageContent
                {
                    Role = AuthorRole.System,
                    Content = $"Previous conversation summary ({truncatedCount} messages): {summary}"
                });
            }
        }
        else if (truncatedCount > 0)
        {
            // Add a simple truncation notice
            reducedHistory.Add(new ChatMessageContent
            {
                Role = AuthorRole.System,
                Content = $"[Note: {truncatedCount} older messages were truncated to stay within token limits]"
            });
        }

        // Add the kept messages
        foreach (var message in messagesToKeep)
        {
            reducedHistory.Add(message);
        }

        return reducedHistory;
    }

    /// <summary>
    /// Truncates very large function results to prevent token overflow.
    /// This is a more aggressive approach for handling extremely large content.
    /// </summary>
    public ChatHistory TruncateLargeFunctionResults(ChatHistory chatHistory, int maxTokensPerResult = 10000)
    {
        var truncatedHistory = new ChatHistory();
        
        foreach (var message in chatHistory)
        {
            var truncatedMessage = TruncateLargeFunctionResultsInMessage(message, maxTokensPerResult);
            truncatedHistory.Add(truncatedMessage);
        }
        
        return truncatedHistory;
    }

    /// <summary>
    /// Truncates large function results within a single message.
    /// </summary>
    private ChatMessageContent TruncateLargeFunctionResultsInMessage(ChatMessageContent message, int maxTokensPerResult)
    {
        if (message.Items == null || !message.Items.Any())
        {
            return message;
        }

        var hasLargeFunctionResults = message.Items.Any(item => 
            item is FunctionResultContent result && EstimateFunctionResultTokens(result) > maxTokensPerResult);

        if (!hasLargeFunctionResults)
        {
            return message;
        }

        // Create a new message with truncated function results
        var truncatedMessage = new ChatMessageContent
        {
            Role = message.Role,
            Content = message.Content,
            AuthorName = message.AuthorName
        };

        foreach (var item in message.Items)
        {
            if (item is FunctionResultContent functionResult)
            {
                var estimatedTokens = EstimateFunctionResultTokens(functionResult);
                if (estimatedTokens > maxTokensPerResult)
                {
                    var truncatedResult = TruncateFunctionResult(functionResult, maxTokensPerResult);
                    truncatedMessage.Items.Add(truncatedResult);
                    
                    _logger.LogWarning("Truncated large function result from {FunctionName} ({OriginalTokens} -> {TruncatedTokens} tokens)", 
                        functionResult.FunctionName, estimatedTokens, EstimateFunctionResultTokens(truncatedResult));
                }
                else
                {
                    truncatedMessage.Items.Add(item);
                }
            }
            else
            {
                truncatedMessage.Items.Add(item);
            }
        }

        return truncatedMessage;
    }

    /// <summary>
    /// Truncates a function result to fit within the specified token limit.
    /// </summary>
    private FunctionResultContent TruncateFunctionResult(FunctionResultContent original, int maxTokens)
    {
        if (original.Result == null)
        {
            return original;
        }

        var resultString = original.Result.ToString();
        if (string.IsNullOrEmpty(resultString))
        {
            return original;
        }

        // Reserve tokens for function metadata and truncation notice
        var metadataTokens = ((original.FunctionName?.Length ?? 0) + (original.PluginName?.Length ?? 0)) / 4 + 20;
        var availableTokens = maxTokens - metadataTokens;
        
        if (availableTokens <= 0)
        {
            availableTokens = maxTokens / 2; // Fallback
        }

        // Calculate max characters (4 chars per token)
        var maxChars = availableTokens * 4;
        
        if (resultString.Length <= maxChars)
        {
            return original;
        }

        // Truncate and add notice
        var truncatedContent = resultString.Substring(0, maxChars - 200) + 
            "\n\n[TRUNCATED: Content was reduced to stay within token limits. Original length: " + 
            resultString.Length + " characters]";

        return new FunctionResultContent(
            functionName: original.FunctionName,
            pluginName: original.PluginName,
            result: truncatedContent
        );
    }

    /// <summary>
    /// Summarizes truncated messages to preserve important context.
    /// </summary>
    private async Task<string?> SummarizeTruncatedMessagesAsync(
        List<ChatMessageContent> truncatedMessages,
        CancellationToken cancellationToken)
    {
        if (_chatService == null || !truncatedMessages.Any())
        {
            return null;
        }

        try
        {
            var summaryPrompt = BuildSummaryPrompt(truncatedMessages);
            var summaryHistory = new ChatHistory();
            summaryHistory.AddSystemMessage("You are a helpful assistant that creates concise summaries of conversations.");
            summaryHistory.AddUserMessage(summaryPrompt);

            var response = await _chatService.GetChatMessageContentAsync(
                summaryHistory,
                cancellationToken: cancellationToken);

            return response?.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize truncated messages");
            return null;
        }
    }

    /// <summary>
    /// Builds a prompt for summarizing truncated messages.
    /// </summary>
    private string BuildSummaryPrompt(List<ChatMessageContent> messages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Please provide a concise summary of the following conversation, focusing on key points and context:");
        sb.AppendLine();
        
        foreach (var message in messages)
        {
            var role = message.Role == AuthorRole.User ? "User" : "Assistant";
            sb.AppendLine($"{role}: {message.Content}");
            sb.AppendLine();
        }
        
        sb.AppendLine("Summary:");
        return sb.ToString();
    }

    /// <summary>
    /// Estimates token count for the entire chat history.
    /// </summary>
    private int EstimateTokenCount(ChatHistory chatHistory)
    {
        return chatHistory.Sum(message => EstimateTokenCount(message));
    }

    /// <summary>
    /// Estimates token count for a single message.
    /// Uses a simple heuristic: ~4 characters per token (GPT-3/4 average).
    /// For production use, consider using a proper tokenizer.
    /// </summary>
    private int EstimateTokenCount(ChatMessageContent message)
    {
        if (message == null)
        {
            return 0;
        }

        var totalTokens = 0;
        
        // Count content tokens
        if (!string.IsNullOrEmpty(message.Content))
        {
            totalTokens += message.Content.Length / 4;
        }

        // Count tokens from function calls and results (these can be very large)
        if (message.Items != null)
        {
            foreach (var item in message.Items)
            {
                switch (item)
                {
                    case FunctionCallContent functionCall:
                        totalTokens += EstimateFunctionCallTokens(functionCall);
                        break;
                    case FunctionResultContent functionResult:
                        totalTokens += EstimateFunctionResultTokens(functionResult);
                        break;
                    case TextContent textContent:
                        totalTokens += (textContent.Text?.Length ?? 0) / 4;
                        break;
                    case ImageContent:
                        totalTokens += 85; // Standard image token cost for vision models
                        break;
                }
            }
        }

        // Add tokens for role and message structure
        totalTokens += 4;
        
        return totalTokens;
    }

    /// <summary>
    /// Estimates token count for function call content.
    /// </summary>
    private int EstimateFunctionCallTokens(FunctionCallContent functionCall)
    {
        var tokens = 0;
        
        // Function name and plugin name
        tokens += (functionCall.FunctionName?.Length ?? 0) / 4;
        tokens += (functionCall.PluginName?.Length ?? 0) / 4;
        
        // Arguments
        if (functionCall.Arguments != null)
        {
            foreach (var arg in functionCall.Arguments)
            {
                tokens += (arg.Key?.Length ?? 0) / 4;
                tokens += (arg.Value?.ToString()?.Length ?? 0) / 4;
            }
        }
        
        // Structure overhead
        tokens += 10;
        
        return tokens;
    }

    /// <summary>
    /// Estimates token count for function result content.
    /// Function results can be extremely large (like scraped web content).
    /// </summary>
    private int EstimateFunctionResultTokens(FunctionResultContent functionResult)
    {
        var tokens = 0;
        
        // Function name and plugin name
        tokens += (functionResult.FunctionName?.Length ?? 0) / 4;
        tokens += (functionResult.PluginName?.Length ?? 0) / 4;
        
        // Result content (this is often the largest part)
        if (functionResult.Result != null)
        {
            var resultString = functionResult.Result.ToString();
            tokens += (resultString?.Length ?? 0) / 4;
        }
        
        // Structure overhead
        tokens += 10;
        
        return tokens;
    }
}
