using Xians.Lib.Agents.Core;
using Xians.Lib.Common.Usage;
using Xians.Lib.Agents.Messaging;
using System.Diagnostics;

namespace Xians.Lib.Examples;

/*
 * Example: Usage Tracking in Xians.Lib Agents
 * 
 * This example demonstrates how to track LLM token usage in your agents.
 * Since Xians.Lib is a framework where you make your own LLM calls,
 * usage tracking is provided as a utility that you call after making LLM calls.
 * 
 * Examples included:
 * 1. Basic usage tracking with extension method
 * 2. Using UsageTracker for automatic timing
 * 2b. Tracking with conversation history
 * 3. Multiple LLM calls with separate tracking
 * 4. Advanced usage with custom metadata
 * 5. Error handling and resilience
 * 6. Using Microsoft Agents Framework (MAF) with UsageTrackingHelper
 * 
 * Note: This example assumes environment variables are set. In a real application,
 * you might use DotNetEnv or another configuration method to load .env files.
 */
public class UsageTrackingExample
{
    public static async Task RunAsync()
    {
        // Note: In a real application, you might load environment variables from a .env file
        // using DotNetEnv.Env.Load() or another configuration method.

        // Get configuration from environment
        var serverUrl = Environment.GetEnvironmentVariable("XIANS_SERVER_URL") 
            ?? throw new InvalidOperationException("XIANS_SERVER_URL environment variable is not set");
        var xiansApiKey = Environment.GetEnvironmentVariable("XIANS_API_KEY") 
            ?? throw new InvalidOperationException("XIANS_API_KEY environment variable is not set");

        // Initialize Xians Platform
        var platform = await XiansPlatform.InitializeAsync(new XiansOptions
        {
            ServerUrl = serverUrl,
            ApiKey = xiansApiKey
        });

        // Register your agent
        var agent = platform.Agents.Register(new XiansAgentRegistration
        {
            Name = "UsageTrackingDemo",
            SystemScoped = false
        });

        // Create a built-in workflow
        var workflow = agent.Workflows.DefineBuiltIn(name: "Conversational", maxConcurrent: 1);

        // Example 1: Basic usage tracking with extension method
        workflow.OnUserChatMessage(async (context) => 
        {
            Console.WriteLine($"Received: {context.Message.Text}");
            
            // Simulate an LLM call (replace with your actual LLM SDK)
            var response = await SimulateOpenAICall(context.Message.Text);
            
            // Track usage - the easy way!
            await context.ReportUsageAsync(
                model: "gpt-4",
                promptTokens: response.PromptTokens,
                completionTokens: response.CompletionTokens,
                totalTokens: response.TotalTokens
            );
            
            await context.ReplyAsync(response.Text);
        });

        // Example 2: Using UsageTracker for automatic timing
        var workflow2 = agent.Workflows.DefineBuiltIn(name: "TimedWorkflow", maxConcurrent: 1);

        workflow2.OnUserChatMessage(async (context) => 
        {
            // UsageTracker automatically measures response time
            using var tracker = new UsageTracker(context, "gpt-4");
            
            var response = await SimulateOpenAICall(context.Message.Text);
            
            // Report includes automatic timing
            await tracker.ReportAsync(
                response.PromptTokens,
                response.CompletionTokens
            );
            
            await context.ReplyAsync(response.Text);
        });

        // Example 2b: Tracking with conversation history
        var workflow2b = agent.Workflows.DefineBuiltIn(name: "HistoryTrackingWorkflow", maxConcurrent: 1);

        workflow2b.OnUserChatMessage(async (context) => 
        {
            // Get conversation history
            var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
            Console.WriteLine($"Including {history.Count} messages from history");
            
            // Create tracker with message count (history + current)
            var messageCount = history.Count + 1;
            using var tracker = new UsageTracker(
                context, 
                "gpt-4",
                messageCount: messageCount
            );
            
            // Simulate LLM call with history
            var prompt = $"History: {history.Count} messages. Current: {context.Message.Text}";
            var response = await SimulateOpenAICall(prompt);
            
            // Report includes message count and timing
            await tracker.ReportAsync(
                response.PromptTokens,
                response.CompletionTokens
            );
            
            await context.ReplyAsync(response.Text);
        });

        // Example 3: Multiple LLM calls with separate tracking
        var workflow3 = agent.Workflows.DefineBuiltIn(name: "MultiStepWorkflow", maxConcurrent: 1);

        workflow3.OnUserChatMessage(async (context) => 
        {
            // Step 1: Analyze sentiment
            using (var tracker = new UsageTracker(context, "gpt-3.5-turbo", source: "SentimentAnalysis"))
            {
                var sentiment = await SimulateOpenAICall($"Analyze sentiment: {context.Message.Text}");
                await tracker.ReportAsync(sentiment.PromptTokens, sentiment.CompletionTokens);
                Console.WriteLine($"Sentiment: {sentiment.Text}");
            }
            
            // Step 2: Generate response based on sentiment
            using (var tracker = new UsageTracker(context, "gpt-4", source: "ResponseGeneration"))
            {
                var response = await SimulateOpenAICall($"Generate response: {context.Message.Text}");
                await tracker.ReportAsync(response.PromptTokens, response.CompletionTokens);
                await context.ReplyAsync(response.Text);
            }
        });

        // Example 4: Advanced usage with custom metadata
        var workflow4 = agent.Workflows.DefineBuiltIn(name: "DetailedTracking", maxConcurrent: 1);

        workflow4.OnUserChatMessage(async (context) => 
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Get conversation history to include in metadata
            var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
            
            var response = await SimulateOpenAICall(context.Message.Text);
            stopwatch.Stop();
            
            // Create metadata with additional context
            var metadata = new Dictionary<string, string>
            {
                ["conversation_length"] = history.Count.ToString(),
                ["message_length"] = context.Message.Text.Length.ToString(),
                ["temperature"] = "0.7",
                ["max_tokens"] = "2000",
                ["user_type"] = "premium"
            };
            
            // Manual usage reporting with full control
            var record = new UsageEventRecord(
                TenantId: context.Message.TenantId,
                UserId: context.Message.ParticipantId,
                Model: "gpt-4",
                PromptTokens: response.PromptTokens,
                CompletionTokens: response.CompletionTokens,
                TotalTokens: response.TotalTokens,
                MessageCount: history.Count + 1,
                WorkflowId: XiansContext.WorkflowId,
                RequestId: context.Message.RequestId,
                Source: "UsageTrackingDemo.DetailedTracking",
                Metadata: metadata,
                ResponseTimeMs: stopwatch.ElapsedMilliseconds
            );
            
            await UsageEventsClient.Instance.ReportAsync(record);
            
            await context.ReplyAsync(response.Text);
        });

        // Example 5: Error handling and resilience
        var workflow5 = agent.Workflows.DefineBuiltIn(name: "ResilientTracking", maxConcurrent: 1);

        workflow5.OnUserChatMessage(async (context) => 
        {
            try
            {
                var response = await SimulateOpenAICall(context.Message.Text);
                
                // Try to report usage, but don't fail if it doesn't work
                try
                {
                    await context.ReportUsageAsync(
                        model: "gpt-4",
                        promptTokens: response.PromptTokens,
                        completionTokens: response.CompletionTokens,
                        totalTokens: response.TotalTokens
                    );
                }
                catch (Exception ex)
                {
                    // Log but continue - usage tracking is best-effort
                    Console.WriteLine($"Warning: Failed to report usage: {ex.Message}");
                }
                
                await context.ReplyAsync(response.Text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
                await context.ReplyAsync("Sorry, I encountered an error processing your request.");
            }
        });

        // Example 6: Using Microsoft Agents Framework (MAF) with UsageTrackingHelper
        var workflow6 = agent.Workflows.DefineBuiltIn(name: "MAFWorkflow", maxConcurrent: 1);

        workflow6.OnUserChatMessage(async (context) => 
        {
            // Get conversation history for message count
            var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
            var messageCount = history.Count + 1;
            
            var modelName = "gpt-4o-mini";
            using var tracker = new UsageTracker(context, modelName, messageCount, source: "MAF Agent");
            
            // In a real application, you would use actual MAF agent here:
            // var chatClient = new OpenAIClient(apiKey).GetChatClient(modelName);
            // var mafAgent = chatClient.CreateAIAgent(new ChatClientAgentOptions
            // {
            //     ChatOptions = new ChatOptions { Instructions = "You are a helpful assistant." },
            //     ChatMessageStoreFactory = ctx => new XiansChatMessageStore(context)
            // });
            // var response = await mafAgent.RunAsync(context.Message.Text);
            
            // For this example, we simulate a MAF response
            var response = await SimulateMafAgentResponse(context.Message.Text);
            
            // Extract token usage from MAF response using reflection helper
            // This is necessary because MAF responses have complex nested Usage properties
            var (promptTokens, completionTokens, totalTokens, _) = 
                UsageTrackingHelper.ExtractUsageFromResponse(response, modelName);
            
            // Report usage with automatic timing and A2A context awareness
            await tracker.ReportAsync(promptTokens, completionTokens, totalTokens);
            
            await context.ReplyAsync(GetResponseText(response));
        });

        Console.WriteLine("Usage Tracking Demo Agent started. Press Ctrl+C to stop.");

        // Run the agent
        await agent.RunAllAsync();
    }

    // ============================================================================
    // Helper Methods (Simulate LLM calls - replace with real LLM SDK in production)
    // ============================================================================

    private static async Task<LLMResponse> SimulateOpenAICall(string prompt)
    {
        // Simulate API latency
        await Task.Delay(100);
        
        // Simulate token usage calculation (in reality, this comes from the LLM provider)
        var promptTokens = EstimateTokens(prompt);
        var completionTokens = 50; // Simulated
        
        return new LLMResponse
        {
            Text = $"This is a simulated response to: {prompt}",
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens
        };
    }

    private static long EstimateTokens(string text)
    {
        // Very rough estimation: ~4 characters per token
        return text.Length / 4;
    }

    // Simulate a MAF agent response with nested Usage property
    private static async Task<object> SimulateMafAgentResponse(string prompt)
    {
        await Task.Delay(100);
        
        var promptTokens = EstimateTokens(prompt);
        var completionTokens = 50L;
        
        // Simulate MAF response structure with nested Usage property
        return new MafAgentResponse
        {
            Text = $"This is a simulated MAF response to: {prompt}",
            Usage = new UsageInfo
            {
                InputTokenCount = promptTokens,
                OutputTokenCount = completionTokens,
                TotalTokenCount = promptTokens + completionTokens
            }
        };
    }

    private static string GetResponseText(object response)
    {
        return response is MafAgentResponse mafResponse ? mafResponse.Text : string.Empty;
    }

    private class LLMResponse
    {
        public string Text { get; set; } = string.Empty;
        public long PromptTokens { get; set; }
        public long CompletionTokens { get; set; }
        public long TotalTokens { get; set; }
    }

    // Simulates MAF agent response structure
    private class MafAgentResponse
    {
        public string Text { get; set; } = string.Empty;
        public UsageInfo? Usage { get; set; }
    }

    private class UsageInfo
    {
        public long InputTokenCount { get; set; }
        public long OutputTokenCount { get; set; }
        public long TotalTokenCount { get; set; }
    }
}

/// <summary>
/// Helper class to extract token usage information from MAF agent responses.
/// Use this when working with Microsoft Agents Framework (MAF) where usage
/// information is nested in complex response objects.
/// </summary>
internal static class UsageTrackingHelper
{
    /// <summary>
    /// Extracts token usage from a MAF agent response object using reflection.
    /// </summary>
    /// <param name="response">The response object from agent.RunAsync()</param>
    /// <param name="modelName">The model name to use if not found in response</param>
    /// <returns>Tuple containing (promptTokens, completionTokens, totalTokens, modelName)</returns>
    public static (long promptTokens, long completionTokens, long totalTokens, string model) 
        ExtractUsageFromResponse(object response, string modelName)
    {
        if (response == null)
        {
            return (0, 0, 0, modelName);
        }

        var responseType = response.GetType();
        
        // Try to get Usage property
        var usageProperty = responseType.GetProperty("Usage", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (usageProperty != null)
        {
            var usageObj = usageProperty.GetValue(response);
            if (usageObj != null)
            {
                // Try to get token count properties
                var promptTokens = TryGetPropertyValue(usageObj, "InputTokenCount", "PromptTokens", "InputTokens");
                var completionTokens = TryGetPropertyValue(usageObj, "OutputTokenCount", "CompletionTokens", "OutputTokens");
                var totalTokens = TryGetPropertyValue(usageObj, "TotalTokenCount", "TotalTokens");
                
                if (promptTokens.HasValue || completionTokens.HasValue || totalTokens.HasValue)
                {
                    return (
                        promptTokens ?? 0,
                        completionTokens ?? 0,
                        totalTokens ?? (promptTokens ?? 0) + (completionTokens ?? 0),
                        modelName
                    );
                }
            }
        }
        
        // Try to get Metadata property and look for Usage there
        var metadataProperty = responseType.GetProperty("Metadata", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (metadataProperty != null)
        {
            var metadata = metadataProperty.GetValue(response) as IReadOnlyDictionary<string, object?>;
            if (metadata != null && metadata.TryGetValue("Usage", out var usageObj) && usageObj != null)
            {
                var promptTokens = TryGetPropertyValue(usageObj, "InputTokenCount", "PromptTokens", "InputTokens");
                var completionTokens = TryGetPropertyValue(usageObj, "OutputTokenCount", "CompletionTokens", "OutputTokens");
                var totalTokens = TryGetPropertyValue(usageObj, "TotalTokenCount", "TotalTokens");
                
                if (promptTokens.HasValue || completionTokens.HasValue || totalTokens.HasValue)
                {
                    return (
                        promptTokens ?? 0,
                        completionTokens ?? 0,
                        totalTokens ?? (promptTokens ?? 0) + (completionTokens ?? 0),
                        modelName
                    );
                }
            }
        }
        
        // No usage data found - return zeros
        return (0, 0, 0, modelName);
    }
    
    /// <summary>
    /// Tries to get a property value from an object using reflection.
    /// </summary>
    private static long? TryGetPropertyValue(object obj, params string[] propertyNames)
    {
        var objType = obj.GetType();
        
        foreach (var propName in propertyNames)
        {
            var prop = objType.GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null)
            {
                var value = prop.GetValue(obj);
                if (value != null)
                {
                    try
                    {
                        return Convert.ToInt64(value);
                    }
                    catch
                    {
                        // Ignore conversion errors
                    }
                }
            }
        }
        
        return null;
    }
}


