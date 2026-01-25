using Xians.Lib.Agents.Core;
using Xians.Lib.Common.Usage;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Workflows.Models;
using System.Diagnostics;
using Temporalio.Workflows;

namespace Xians.Lib.Examples;

/*
 * Example: Usage Tracking in Xians.Lib Agents
 * 
 * This example demonstrates how to track LLM token usage and custom metrics in your agents.
 * 
 * SINGLE UNIFIED API:
 * 
 * XiansContext.Metrics.Track() - For all contexts (workflows, message handlers, activities)
 *    - Context-aware: Automatically uses activities in workflows, direct HTTP outside
 *    - A2A-aware: Pass UserMessageContext to get correct workflow ID in A2A scenarios
 *    - Example: await XiansContext.Metrics.Track(context).WithMetric(...).ReportAsync()
 *    - Example (workflow): await XiansContext.Metrics.Track().WithMetric(...).ReportAsync()
 * 
 * Examples included:
 * 1. Custom labels without predefined constants (complete flexibility)
 * 2. Basic usage tracking with fluent builder
 * 3. Tracking with manual timing (using Stopwatch)
 * 4. Multiple LLM calls with separate tracking
 * 5. Custom metrics tracking (flexible array format)
 * 6. Fluent builder pattern for convenient tracking
 * 7. Advanced usage with custom metadata
 * 8. Error handling and resilience
 * 9. Using Microsoft Agents Framework (MAF) with UsageTrackingHelper
 * 10. XiansContext.Metrics examples (workflow-focused API)
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
        var workflow = agent.Workflows.DefineBuiltIn(name: "Conversational", options: new WorkflowOptions { MaxConcurrent = 1 });

        // Example 1: Custom labels without predefined constants (complete flexibility)
        // This shows you can use ANY labels you want - not limited to MetricCategories or MetricTypes
        workflow.OnUserChatMessage(async (context) => 
        {
            Console.WriteLine($"Received: {context.Message.Text}");
            
            // Simulate some business activity
            var response = await SimulateOpenAICall(context.Message.Text);
            await SendEmailsAsync(2);
            
            // Track metrics with completely custom labels - no predefined constants needed!
            await XiansContext.Metrics.Track(context)
                .ForModel("gpt-4")
                .WithCustomIdentifier($"msg-{Guid.NewGuid()}")
                .WithMetrics(
                    // Use your own category and type names - total flexibility!
                    ("llm", "tokens_used", (double)response.TotalTokens, "tokens"),
                    ("business", "emails_dispatched", 2.0, "count"),
                    ("business", "customer_interactions", 1.0, "count"),
                    ("cost", "api_cost_usd", 0.0025, "usd"),
                    ("performance", "response_time_seconds", 1.23, "seconds")
                )
                .WithMetadata("customer_tier", "premium")
                .ReportAsync();
            
            await context.ReplyAsync($"{response.Text}\n\n(2 emails sent)");
        });

        // Example 2: Basic usage tracking with fluent builder (using predefined constants)
        var workflow2 = agent.Workflows.DefineBuiltIn(name: "StandardTracking", options: new WorkflowOptions { MaxConcurrent = 1 });
        
        workflow2.OnUserChatMessage(async (context) => 
        {
            Console.WriteLine($"Received: {context.Message.Text}");
            
            // Simulate an LLM call (replace with your actual LLM SDK)
            var response = await SimulateOpenAICall(context.Message.Text);
            
            // Track token usage using fluent builder
            await XiansContext.Metrics.Track(context)
                .ForModel("gpt-4")
                .WithMetrics(
                    (MetricCategories.Tokens, MetricTypes.PromptTokens, response.PromptTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.CompletionTokens, response.CompletionTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.TotalTokens, response.TotalTokens, "tokens")
                )
                .ReportAsync();
            
            await context.ReplyAsync(response.Text);
        });

        // Example 3: Manual timing with Stopwatch
        var workflow3 = agent.Workflows.DefineBuiltIn(name: "TimedWorkflow", options: new WorkflowOptions { MaxConcurrent = 1 });

        workflow3.OnUserChatMessage(async (context) => 
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await SimulateOpenAICall(context.Message.Text);
            stopwatch.Stop();
            
            // Track with manual timing using fluent builder
            await XiansContext.Metrics.Track(context)
                .ForModel("gpt-4")
                .WithMetrics(
                    (MetricCategories.Tokens, MetricTypes.PromptTokens, response.PromptTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.CompletionTokens, response.CompletionTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.TotalTokens, response.TotalTokens, "tokens"),
                    (MetricCategories.Performance, MetricTypes.ResponseTimeMs, stopwatch.ElapsedMilliseconds, "ms")
                )
                .ReportAsync();
            
            await context.ReplyAsync(response.Text);
        });

        // Example 4: Multiple LLM calls with separate tracking
        var workflow4 = agent.Workflows.DefineBuiltIn(name: "MultiStepWorkflow", options: new WorkflowOptions { MaxConcurrent = 1 });

        workflow4.OnUserChatMessage(async (context) => 
        {
            // Step 1: Analyze sentiment
            var sentiment = await SimulateOpenAICall($"Analyze sentiment: {context.Message.Text}");
            await XiansContext.Metrics.Track(context)
                .ForModel("gpt-3.5-turbo")
                .FromSource("SentimentAnalysis")
                .WithMetrics(
                    (MetricCategories.Tokens, MetricTypes.PromptTokens, sentiment.PromptTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.CompletionTokens, sentiment.CompletionTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.TotalTokens, sentiment.TotalTokens, "tokens")
                )
                .ReportAsync();
            Console.WriteLine($"Sentiment: {sentiment.Text}");
            
            // Step 2: Generate response based on sentiment
            var response = await SimulateOpenAICall($"Generate response: {context.Message.Text}");
            await XiansContext.Metrics.Track(context)
                .ForModel("gpt-4")
                .FromSource("ResponseGeneration")
                .WithMetrics(
                    (MetricCategories.Tokens, MetricTypes.PromptTokens, response.PromptTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.CompletionTokens, response.CompletionTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.TotalTokens, response.TotalTokens, "tokens")
                )
                .ReportAsync();
            await context.ReplyAsync(response.Text);
        });

        // Example 5: Custom metrics tracking with flexible array format
        var workflow5 = agent.Workflows.DefineBuiltIn(name: "CustomMetricsWorkflow", options: new WorkflowOptions { MaxConcurrent = 1 });

        workflow5.OnUserChatMessage(async (context) => 
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simulate sending emails as part of the workflow
            var response = await SimulateOpenAICall(context.Message.Text);
            await SendEmailsAsync(3); // Simulate sending 3 emails
            
            stopwatch.Stop();
            
            // Track both LLM usage AND custom metrics using fluent builder
            await XiansContext.Metrics.Track(context)
                .ForModel("gpt-4")
                .WithMetrics(
                    // Standard token metrics
                    (MetricCategories.Tokens, MetricTypes.PromptTokens, response.PromptTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.CompletionTokens, response.CompletionTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.TotalTokens, response.TotalTokens, "tokens"),
                    // Standard activity metrics
                    (MetricCategories.Activity, MetricTypes.MessageCount, 1, "count"),
                    // Custom metrics - emails sent by this workflow
                    ("activity", "email_sent", 3, "count"),
                    // Custom metrics - workflow completion
                    ("activity", "workflow_completed", 1, "count"),
                    // Performance metrics
                    (MetricCategories.Performance, MetricTypes.ResponseTimeMs, stopwatch.ElapsedMilliseconds, "ms")
                )
                .ReportAsync();
            
            await context.ReplyAsync($"{response.Text}\n\n(3 emails sent successfully)");
        });

        // Example 6: Fluent builder pattern for convenient tracking
        var workflow6 = agent.Workflows.DefineBuiltIn(name: "FluentBuilderWorkflow", options: new WorkflowOptions { MaxConcurrent = 1 });

        workflow6.OnUserChatMessage(async (context) => 
        {
            // Simple token tracking with fluent API
            var response = await SimulateOpenAICall(context.Message.Text);
            
            await XiansContext.Metrics.Track(context)
                .ForModel("gpt-4")
                .WithMetrics(
                    (MetricCategories.Tokens, MetricTypes.PromptTokens, response.PromptTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.CompletionTokens, response.CompletionTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.TotalTokens, response.TotalTokens, "tokens")
                )
                .ReportAsync();
            
            await context.ReplyAsync(response.Text);
        });

        // Example 6b: Fluent builder with incremental metric building
        var workflow6b = agent.Workflows.DefineBuiltIn(name: "IncrementalFluentWorkflow", options: new WorkflowOptions { MaxConcurrent = 1 });

        workflow6b.OnUserChatMessage(async (context) => 
        {
            // Start tracking
            var tracker = XiansContext.Metrics.Track(context)
                .ForModel("gpt-4")
                .FromSource("EmailWorkflow");
            
            // Step 1: LLM call
            var response = await SimulateOpenAICall(context.Message.Text);
            tracker.WithMetric(MetricCategories.Tokens, MetricTypes.TotalTokens, response.TotalTokens, "tokens");
            
            // Step 2: Send emails
            await SendEmailsAsync(3);
            tracker.WithMetric("activity", "email_sent", 3, "count");
            
            // Step 3: Complete workflow
            tracker.WithMetric("activity", "workflow_completed", 1, "count");
            
            // Report all metrics at once
            await tracker.ReportAsync();
            
            await context.ReplyAsync($"{response.Text}\n\n(Workflow completed: 3 emails sent)");
        });

        // Example 7: Advanced usage with custom metadata
        var workflow7 = agent.Workflows.DefineBuiltIn(name: "DetailedTracking", options: new WorkflowOptions { MaxConcurrent = 1 });

        workflow7.OnUserChatMessage(async (context) => 
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
            
            // Track usage with custom metadata using fluent builder
            await XiansContext.Metrics.Track(context)
                .ForModel("gpt-4")
                .FromSource("UsageTrackingDemo.DetailedTracking")
                .WithMetrics(
                    (MetricCategories.Tokens, MetricTypes.PromptTokens, response.PromptTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.CompletionTokens, response.CompletionTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.TotalTokens, response.TotalTokens, "tokens"),
                    (MetricCategories.Activity, MetricTypes.MessageCount, history.Count + 1, "count"),
                    (MetricCategories.Performance, MetricTypes.ResponseTimeMs, stopwatch.ElapsedMilliseconds, "ms")
                )
                .WithMetadata(metadata)
                .ReportAsync();
            
            await context.ReplyAsync(response.Text);
        });

        // Example 8: Error handling and resilience
        var workflow8 = agent.Workflows.DefineBuiltIn(name: "ResilientTracking", options: new WorkflowOptions { MaxConcurrent = 1 });

        workflow8.OnUserChatMessage(async (context) => 
        {
            try
            {
                var response = await SimulateOpenAICall(context.Message.Text);
                
                // Try to report usage, but don't fail if it doesn't work
                try
                {
                    await XiansContext.Metrics.Track(context)
                        .ForModel("gpt-4")
                        .WithMetrics(
                            (MetricCategories.Tokens, MetricTypes.PromptTokens, response.PromptTokens, "tokens"),
                            (MetricCategories.Tokens, MetricTypes.CompletionTokens, response.CompletionTokens, "tokens"),
                            (MetricCategories.Tokens, MetricTypes.TotalTokens, response.TotalTokens, "tokens")
                        )
                        .ReportAsync();
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

        // Example 9: Using Microsoft Agents Framework (MAF) with UsageTrackingHelper
        var workflow9 = agent.Workflows.DefineBuiltIn(name: "MAFWorkflow", options: new WorkflowOptions { MaxConcurrent = 1 });

        workflow9.OnUserChatMessage(async (context) => 
        {
            // Get conversation history for message count
            var history = await context.GetChatHistoryAsync(page: 1, pageSize: 10);
            var messageCount = history.Count + 1;
            
            var modelName = "gpt-4o-mini";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
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
            stopwatch.Stop();
            
            // Extract token usage from MAF response using reflection helper
            // This is necessary because MAF responses have complex nested Usage properties
            var (promptTokens, completionTokens, totalTokens, _) = 
                UsageTrackingHelper.ExtractUsageFromResponse(response, modelName);
            
            // Report usage with manual timing and A2A context awareness using fluent builder
            await XiansContext.Metrics.Track(context)
                .ForModel(modelName)
                .FromSource("MAF Agent")
                .WithMetrics(
                    (MetricCategories.Tokens, MetricTypes.PromptTokens, promptTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.CompletionTokens, completionTokens, "tokens"),
                    (MetricCategories.Tokens, MetricTypes.TotalTokens, totalTokens, "tokens"),
                    (MetricCategories.Activity, MetricTypes.MessageCount, messageCount, "count"),
                    (MetricCategories.Performance, MetricTypes.ResponseTimeMs, stopwatch.ElapsedMilliseconds, "ms")
                )
                .ReportAsync();
            
            await context.ReplyAsync(GetResponseText(response));
        });

        Console.WriteLine("Usage Tracking Demo Agent started. Press Ctrl+C to stop.");

        // Run the agent
        await agent.RunAllAsync();
    }

    //
    // ============================================================================
    // XiansContext.Metrics Examples (Workflow-focused API)
    // ============================================================================
    // The following examples demonstrate XiansContext.Metrics.Track() API which is:
    // - Context-aware: Automatically uses activities in workflows, direct HTTP outside
    // - Ideal for workflows where you need automatic activity handling
    // - Can also be used in message handlers as an alternative to XiansContext.Metrics.Track(context)
    //

    /// <summary>
    /// Example: Tracking workflow metrics with XiansContext.Metrics
    /// This API automatically handles workflow context (uses activities under the hood)
    /// </summary>
    [Workflow]
    public class DataProcessingWorkflow
    {
        [WorkflowRun]
        public async Task<string> RunAsync(string dataId)
        {
            // Track workflow start
            await XiansContext.Metrics
                .Track()
                .WithMetric("workflow_processing", "started", 1, "count")
                .ReportAsync();

            // Simulate processing
            await Workflow.DelayAsync(TimeSpan.FromSeconds(5));

            // Track workflow completion with metadata
            await XiansContext.Metrics
                .Track()
                .WithMetric("workflow_processing", "completed", 1, "count")
                .WithMetric("workflow_processing", "duration", 5000, "ms")
                .WithMetadata("data_id", dataId)
                .ReportAsync();

            return "Processed";
        }
    }

    /// <summary>
    /// Example: Tracking with explicit overrides when you need full control
    /// </summary>
    public static async Task TrackWithExplicitValues()
    {
        await XiansContext.Metrics
            .Track()
            .WithTenantId("custom-tenant-123")
            .WithUserId("user-456")
            .WithWorkflowId("workflow-789")
            .WithRequestId("request-abc")
            .FromSource("CustomService")
            .ForModel("gpt-4")
            .WithMetric("api_calls", "external_api", 1, "count")
            .WithMetric("api_calls", "response_time", 250, "ms")
            .WithMetadata("api_endpoint", "/v1/chat/completions")
            .ReportAsync();
    }

    /// <summary>
    /// Example: Direct ReportAsync when you already have a UsageReportRequest
    /// Useful for advanced scenarios or when building requests programmatically
    /// </summary>
    public static async Task DirectReportExample()
    {
        var request = new UsageReportRequest
        {
            TenantId = XiansContext.TenantId,
            WorkflowId = XiansContext.WorkflowId,
            Source = "BatchProcessor",
            Metrics = new List<MetricValue>
            {
                new() { Category = "batch_processing", Type = "records_processed", Value = 1000, Unit = "count" },
                new() { Category = "batch_processing", Type = "batch_size", Value = 1000, Unit = "count" },
                new() { Category = "batch_processing", Type = "duration", Value = 45000, Unit = "ms" }
            }
        };

        // Auto-detects context (workflow vs agent)
        await XiansContext.Metrics.ReportAsync(request);
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

    private static async Task SendEmailsAsync(int count)
    {
        // Simulate sending emails
        await Task.Delay(50 * count);
        Console.WriteLine($"Sent {count} emails");
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


