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

    private class LLMResponse
    {
        public string Text { get; set; } = string.Empty;
        public long PromptTokens { get; set; }
        public long CompletionTokens { get; set; }
        public long TotalTokens { get; set; }
    }
}


