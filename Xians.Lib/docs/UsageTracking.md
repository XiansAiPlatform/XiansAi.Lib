# Usage Tracking

The Xians.Lib framework provides built-in utilities for tracking LLM token usage and reporting it to the platform. This allows you to monitor costs, usage patterns, and performance metrics across your AI agents.

## Overview

Since Xians.Lib is a framework where you implement your own message handlers and make your own LLM calls, usage tracking is provided as a **utility service** that you can call after making LLM calls.

## Quick Start

### Basic Usage with Extension Method

The simplest way to track usage is using the extension method on `UserMessageContext`:

```csharp
workflow.OnUserChatMessage(async (context) => 
{
    // Your LLM call
    var response = await OpenAIClient.GetChatCompletionAsync(prompt);
    
    // Track usage
    await context.ReportUsageAsync(
        model: response.Model,
        promptTokens: response.Usage.PromptTokens,
        completionTokens: response.Usage.CompletionTokens,
        totalTokens: response.Usage.TotalTokens
    );
    
    // Send response
    await context.ReplyAsync(response.Content);
});
```

### Tracking with Conversation History

When including conversation history in your LLM call, specify the message count:

```csharp
workflow.OnUserChatMessage(async (context) => 
{
    // Get conversation history
    var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
    
    // Build messages array including history
    var messages = BuildMessagesWithHistory(history, context.Message.Text);
    
    // Call LLM with history
    var response = await OpenAIClient.GetChatCompletionAsync(messages);
    
    // Track usage - include message count (history + current message)
    await context.ReportUsageAsync(
        model: response.Model,
        promptTokens: response.Usage.PromptTokens,
        completionTokens: response.Usage.CompletionTokens,
        totalTokens: response.Usage.TotalTokens,
        messageCount: history.Count + 1  // History + current message
    );
    
    await context.ReplyAsync(response.Content);
});
```

### Using UsageTracker for Automatic Timing

The `UsageTracker` class automatically measures response time:

```csharp
workflow.OnUserChatMessage(async (context) => 
{
    using var tracker = new UsageTracker(context, "gpt-4");
    
    // Make your LLM call
    var response = await OpenAIClient.GetChatCompletionAsync(prompt);
    
    // Report usage (includes automatic timing)
    await tracker.ReportAsync(
        response.Usage.PromptTokens,
        response.Usage.CompletionTokens
    );
    
    await context.ReplyAsync(response.Content);
});
```

### UsageTracker with Conversation History

When including history, pass the message count to the tracker:

```csharp
workflow.OnUserChatMessage(async (context) => 
{
    // Get history
    var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
    var messageCount = history.Count + 1;
    
    // Create tracker with message count
    using var tracker = new UsageTracker(
        context, 
        "gpt-4",
        messageCount: messageCount
    );
    
    var messages = BuildMessagesWithHistory(history, context.Message.Text);
    var response = await OpenAIClient.GetChatCompletionAsync(messages);
    
    await tracker.ReportAsync(
        response.Usage.PromptTokens,
        response.Usage.CompletionTokens
    );
    
    await context.ReplyAsync(response.Content);
});
```

## Advanced Usage

### Manual Usage Reporting with Full Control

For complete control over what gets reported:

```csharp
workflow.OnUserChatMessage(async (context) => 
{
    var stopwatch = Stopwatch.StartNew();
    
    // Your LLM call
    var response = await CallCustomLLM(prompt);
    stopwatch.Stop();
    
    // Manual usage reporting
    var record = new UsageEventRecord(
        TenantId: context.TenantId,
        UserId: context.ParticipantId,
        Model: "custom-model-v1",
        PromptTokens: response.InputTokens,
        CompletionTokens: response.OutputTokens,
        TotalTokens: response.TotalTokens,
        MessageCount: 1,
        WorkflowId: XiansContext.WorkflowId,
        RequestId: context.RequestId,
        Source: "MyAgent.CustomLLM",
        Metadata: new Dictionary<string, string>
        {
            ["temperature"] = "0.7",
            ["max_tokens"] = "2000",
            ["custom_field"] = "value"
        },
        ResponseTimeMs: stopwatch.ElapsedMilliseconds
    );
    
    await XiansContext.Metrics.ReportAsync(record);
    
    await context.ReplyAsync(response.Text);
});
```

### Tracking Multiple LLM Calls

When making multiple LLM calls in a single message handler:

```csharp
workflow.OnUserChatMessage(async (context) => 
{
    // First LLM call - sentiment analysis
    using (var tracker1 = new UsageTracker(context, "gpt-3.5-turbo", "SentimentAnalysis"))
    {
        var sentiment = await AnalyzeSentiment(context.Message.Text);
        await tracker1.ReportAsync(sentiment.PromptTokens, sentiment.CompletionTokens);
    }
    
    // Second LLM call - response generation
    using (var tracker2 = new UsageTracker(context, "gpt-4", "ResponseGeneration"))
    {
        var response = await GenerateResponse(context.Message.Text, sentiment);
        await tracker2.ReportAsync(response.PromptTokens, response.CompletionTokens);
        
        await context.ReplyAsync(response.Text);
    }
});
```

### With Semantic Kernel

If you're using Microsoft Semantic Kernel, you can extract usage from ChatMessageContent:

```csharp
workflow.OnUserChatMessage(async (context) => 
{
    var kernel = CreateKernel();
    var responses = new List<ChatMessageContent>();
    
    var stopwatch = Stopwatch.StartNew();
    
    // Invoke Semantic Kernel
    await foreach (var item in agent.InvokeAsync(userMessage, chatHistory))
    {
        responses.Add(item);
    }
    
    stopwatch.Stop();
    
    // Extract usage from responses (use your own extraction logic based on your LLM SDK)
    var promptTokens = 100; // Extract from your response
    var completionTokens = 50; // Extract from your response
    var totalTokens = 150; // Extract from your response
    
    // Report usage
    await XiansContext.Metrics
        .Track(context)
        .ForModel("gpt-4")
        .WithMetrics(
            ("tokens", "prompt_tokens", promptTokens, "tokens"),
            ("tokens", "completion_tokens", completionTokens, "tokens"),
            ("tokens", "total_tokens", totalTokens, "tokens"),
            ("performance", "response_time_ms", stopwatch.ElapsedMilliseconds, "ms")
        )
        .ReportAsync();
    
    var responseText = string.Join(" ", responses.Select(r => r.Content));
    await context.ReplyAsync(responseText);
});
```

### Adding Custom Metadata

You can add custom metadata to track additional context:

```csharp
workflow.OnUserChatMessage(async (context) => 
{
    var metadata = new Dictionary<string, string>
    {
        ["intent"] = detectedIntent,
        ["language"] = detectedLanguage,
        ["conversation_length"] = conversationHistory.Count.ToString(),
        ["use_case"] = "customer_support"
    };
    
    await context.ReportUsageAsync(
        model: "gpt-4",
        promptTokens: 150,
        completionTokens: 75,
        totalTokens: 225,
        metadata: metadata
    );
});
```

## Integration with Different LLM Providers

### OpenAI SDK

```csharp
using OpenAI.Chat;

workflow.OnUserChatMessage(async (context) => 
{
    var client = new ChatClient("gpt-4", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
    
    using var tracker = new UsageTracker(context, "gpt-4");
    
    var completion = await client.CompleteChatAsync(messages);
    
    await tracker.ReportAsync(
        completion.Usage.InputTokenCount,
        completion.Usage.OutputTokenCount,
        completion.Usage.TotalTokenCount
    );
    
    await context.ReplyAsync(completion.Content[0].Text);
});
```

### Azure OpenAI

```csharp
workflow.OnUserChatMessage(async (context) => 
{
    var client = new AzureOpenAIClient(
        new Uri(endpoint),
        new AzureKeyCredential(apiKey)
    );
    
    var chatClient = client.GetChatClient("gpt-4");
    
    using var tracker = new UsageTracker(context, "gpt-4", "AzureOpenAI");
    
    var completion = await chatClient.CompleteChatAsync(messages);
    
    await tracker.ReportAsync(
        completion.Value.Usage.InputTokenCount,
        completion.Value.Usage.OutputTokenCount,
        completion.Value.Usage.TotalTokenCount
    );
    
    await context.ReplyAsync(completion.Value.Content[0].Text);
});
```

### Anthropic Claude

```csharp
workflow.OnUserChatMessage(async (context) => 
{
    var client = new AnthropicClient(apiKey);
    
    using var tracker = new UsageTracker(context, "claude-3-opus-20240229");
    
    var response = await client.Messages.CreateAsync(new MessageRequest
    {
        Model = "claude-3-opus-20240229",
        Messages = messages,
        MaxTokens = 1024
    });
    
    await tracker.ReportAsync(
        response.Usage.InputTokens,
        response.Usage.OutputTokens
    );
    
    await context.ReplyAsync(response.Content[0].Text);
});
```

## Best Practices

### 1. Always Report Usage

Track usage for every LLM call to get accurate cost and usage metrics:

```csharp
// ✅ Good - usage is tracked
using var tracker = new UsageTracker(context, "gpt-4");
var response = await CallLLM();
await tracker.ReportAsync(response.PromptTokens, response.CompletionTokens);

// ❌ Bad - usage not tracked
var response = await CallLLM();
// Missing usage tracking!
```

### 2. Track Message Count Accurately

The `messageCount` parameter represents how many messages were sent to the LLM (including conversation history). This is important for understanding context usage:

```csharp
// ✅ Good - includes conversation history count
var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
var messages = BuildMessagesWithHistory(history, context.Message.Text);
var response = await CallLLM(messages);

await context.ReportUsageAsync(
    model: "gpt-4",
    promptTokens: response.PromptTokens,
    completionTokens: response.CompletionTokens,
    totalTokens: response.TotalTokens,
    messageCount: history.Count + 1  // History + current
);

// ⚠️ Acceptable - defaults to 1 for single message
await context.ReportUsageAsync(
    model: "gpt-4",
    promptTokens: response.PromptTokens,
    completionTokens: response.CompletionTokens,
    totalTokens: response.TotalTokens
    // messageCount defaults to 1
);

// ❌ Bad - wrong count when using history
var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
var messages = BuildMessagesWithHistory(history, context.Message.Text);
var response = await CallLLM(messages);

await context.ReportUsageAsync(
    model: "gpt-4",
    promptTokens: response.PromptTokens,
    completionTokens: response.CompletionTokens,
    totalTokens: response.TotalTokens
    // Missing messageCount - will default to 1 but sent 11 messages!
);
```

### 3. Use Try-Catch for Resilience

Usage reporting failures shouldn't break your agent:

```csharp
try
{
    await context.ReportUsageAsync(model, promptTokens, completionTokens, totalTokens);
}
catch (Exception ex)
{
    // Usage reporting is best-effort - log but don't fail
    _logger.LogWarning(ex, "Failed to report usage");
}
```

Note: The `UsageEventsClient` already handles exceptions internally, so explicit try-catch is optional.

### 4. Include Source Information

Use meaningful source identifiers to track different parts of your agent:

```csharp
await context.ReportUsageAsync(
    model: "gpt-4",
    promptTokens: 100,
    completionTokens: 50,
    totalTokens: 150,
    source: "CustomerSupportAgent.InitialClassification"
);
```

### 5. Track Response Time

Use `UsageTracker` or manual timing to capture performance metrics:

```csharp
var stopwatch = Stopwatch.StartNew();
var response = await CallLLM();
stopwatch.Stop();

await context.ReportUsageAsync(
    model: "gpt-4",
    promptTokens: response.PromptTokens,
    completionTokens: response.CompletionTokens,
    totalTokens: response.TotalTokens,
    responseTimeMs: stopwatch.ElapsedMilliseconds
);
```

### 6. Add Meaningful Metadata

Include context that helps analyze usage patterns:

```csharp
var metadata = new Dictionary<string, string>
{
    ["user_tier"] = "premium",
    ["request_type"] = "complex_query",
    ["language"] = "en",
    ["retry_count"] = retryCount.ToString()
};

await context.ReportUsageAsync(
    model: "gpt-4",
    promptTokens: 200,
    completionTokens: 150,
    totalTokens: 350,
    metadata: metadata
);
```

## Viewing Usage Data

Usage data is sent to the Xians platform server and stored in MongoDB. You can view:

- **Token counts** per request, per user, per agent
- **Cost estimates** based on model pricing
- **Performance metrics** (response times)
- **Usage trends** over time
- **Model distribution** across your agents

## API Reference

### UsageEventsClient

Singleton utility for reporting usage events.

- `static Instance` - Gets the singleton instance
- `Task ReportAsync(UsageEventRecord, CancellationToken)` - Reports a usage event
- `ExtractUsageFromSemanticKernelResponses(IEnumerable<object>)` - Extracts usage from SK responses

### UserMessageContext Extension Methods

- `Task ReportUsageAsync(model, promptTokens, completionTokens, totalTokens, messageCount = 1, source?, metadata?, responseTimeMs?)` - Simple usage reporting with automatic context
- `Task ReportUsageAsync(UsageEventRecord)` - Full control usage reporting

### UsageTracker

Automatic timing and usage tracking helper.

- `UsageTracker(context, model, messageCount = 1, source?, metadata?)` - Constructor
- `Task ReportAsync(promptTokens, completionTokens, totalTokens?)` - Report with timing
- `Dispose()` - Clean up (warns if not reported)

### UsageEventRecord

Data model for usage events.

- `TenantId` - Tenant identifier
- `UserId` - User/participant identifier
- `Model` - LLM model name
- `PromptTokens` - Input token count
- `CompletionTokens` - Output token count
- `TotalTokens` - Total token count
- `MessageCount` - Number of messages
- `WorkflowId` - Workflow identifier
- `RequestId` - Request identifier
- `Source` - Source/component name
- `Metadata` - Custom metadata dictionary
- `ResponseTimeMs` - Response time in milliseconds

## Troubleshooting

### Usage Not Showing in Dashboard

1. Check that the HTTP service is configured properly
2. Verify the server endpoint `/api/agent/usage/report` is accessible
3. Check logs for warning messages about failed reporting
4. Ensure tenant ID is correctly set in the context

### Zero Token Counts

If token extraction returns zeros:

1. Verify your LLM provider returns usage information
2. Check the response metadata format
3. Consider manual token counting as fallback
4. Use `ExtractUsageFromSemanticKernelResponses()` for SK integration

### Performance Impact

Usage tracking is designed to be lightweight:

- HTTP calls are fire-and-forget (no blocking)
- Failures are logged but don't throw exceptions
- Minimal memory overhead
- No impact on message processing latency

## Examples

See the `/docs/Examples/` folder for complete working examples:

- `UsageTrackingExample.cs` - Basic usage tracking
- `MultiLLMUsageExample.cs` - Multiple LLM calls
- `SemanticKernelUsageExample.cs` - Semantic Kernel integration


