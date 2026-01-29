using Xians.Lib.Agents.Core;

namespace Xians.Lib.Examples;

/// <summary>
/// Advanced examples demonstrating full control over all UsageReportRequest fields
/// using the fluent builder API. These examples show how to override default values
/// when needed for special use cases.
/// </summary>
public static class UsageTrackingAdvancedExamples
{
    /// <summary>
    /// Example 1: Override all auto-populated fields
    /// Useful when reporting usage on behalf of another tenant/user or linking to different workflow/request
    /// </summary>
    public static void Example1_OverrideAllFields(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new XiansAgentRegistration { Name = "AdvancedUsageAgent" });
        var workflow = agent.Workflows.DefineBuiltIn("AdvancedWorkflow");

        workflow.OnUserChatMessage(async (context) =>
        {
            // Simulate API call
            var response = await CallExternalApiAsync();

            // Report usage with all fields explicitly set
            await XiansContext.Metrics.Track(context)
                .WithTenantId("custom-tenant-id")           // Override tenant
                .WithUserId("custom-user-id")               // Override user
                .WithWorkflowId("custom-workflow-id")       // Override workflow
                .WithRequestId("custom-request-id")         // Override request
                .ForModel("gpt-4")                          // Set model
                .FromSource("ExternalAPI")                  // Set source
                .WithCustomIdentifier("external-api-123")   // Link to external system
                .WithMetrics(
                    ("tokens", "total_tokens", (double)response.TotalTokens, "tokens")
                )
                .WithMetadata("external_api", "openai")
                .ReportAsync();

            await context.ReplyAsync($"Processed with {response.TotalTokens} tokens");
        });
    }

    /// <summary>
    /// Example 2: Report usage for a different tenant (multi-tenant scenario)
    /// Useful when an admin agent processes requests on behalf of multiple tenants
    /// </summary>
    public static void Example2_CrossTenantReporting(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new XiansAgentRegistration { Name = "AdminAgent" });
        var workflow = agent.Workflows.DefineBuiltIn("AdminWorkflow");

        workflow.OnUserChatMessage(async (context) =>
        {
            // Simulate processing for multiple tenants
            var tenants = new[] { "tenant-1", "tenant-2", "tenant-3" };

            foreach (var tenantId in tenants)
            {
                // Process work for each tenant
                var result = await ProcessForTenantAsync(tenantId);

                // Report usage under each tenant's account
                await XiansContext.Metrics.Track(context)
                    .WithTenantId(tenantId)  // Report to specific tenant
                    .ForModel("gpt-4")
                    .FromSource("AdminAgent.CrossTenantProcessor")
                    .WithMetric("tokens", "total_tokens", result.TokensUsed, "tokens")
                    .WithMetadata("processed_by", "admin_agent")
                    .ReportAsync();
            }

            await context.ReplyAsync("Processed for all tenants");
        });
    }

    /// <summary>
    /// Example 3: Link usage to a specific workflow execution (async processing scenario)
    /// Useful when reporting usage after the original message context is gone
    /// </summary>
    public static void Example3_LinkToSpecificWorkflow(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new XiansAgentRegistration { Name = "AsyncAgent" });
        var workflow = agent.Workflows.DefineBuiltIn("AsyncWorkflow");

        workflow.OnUserChatMessage(async (context) =>
        {
            // Start async processing - capture context info before going async
            var workflowId = $"{context.Message.TenantId}:AsyncAgent:AsyncWorkflow:{context.Message.ParticipantId}";
            var requestId = context.Message.RequestId;

            _ = Task.Run(async () =>
            {
                // This runs in background, original context might be gone
                await Task.Delay(5000); // Simulate long processing

                var result = await LongRunningProcessAsync();

                // Report usage linked to original workflow and request
                await XiansContext.Metrics.Track(context)
                    .WithWorkflowId(workflowId)     // Link to original workflow
                    .WithRequestId(requestId)        // Link to original request
                    .ForModel("gpt-4")
                    .FromSource("AsyncProcessor")
                    .WithMetric("tokens", "total_tokens", result.TokensUsed, "tokens")
                    .WithMetric("performance", "processing_time_seconds", result.ElapsedSeconds, "seconds")
                    .WithMetadata("processing_type", "background")
                    .ReportAsync();
            });

            await context.ReplyAsync("Started background processing");
        });
    }

    /// <summary>
    /// Example 4: Report aggregated usage for batch processing
    /// Useful when processing multiple items and reporting combined usage
    /// </summary>
    public static void Example4_AggregatedBatchUsage(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new XiansAgentRegistration { Name = "BatchAgent" });
        var workflow = agent.Workflows.DefineBuiltIn("BatchWorkflow");

        workflow.OnUserChatMessage(async (context) =>
        {
            // Process batch of items
            var items = new[] { "item1", "item2", "item3", "item4", "item5" };
            var totalTokens = 0.0;
            var batchRequestId = $"batch-{Guid.NewGuid()}";

            foreach (var item in items)
            {
                var result = await ProcessItemAsync(item);
                totalTokens += result.TokensUsed;

                // Report individual item usage with batch request ID
                await XiansContext.Metrics.Track(context)
                    .WithRequestId(batchRequestId)  // Group by batch ID
                    .ForModel("gpt-4")
                    .FromSource($"BatchProcessor.{item}")
                    .WithCustomIdentifier(item)
                    .WithMetric("tokens", "total_tokens", result.TokensUsed, "tokens")
                    .WithMetadata("batch_id", batchRequestId)
                    .WithMetadata("item_id", item)
                    .ReportAsync();
            }

            await context.ReplyAsync($"Processed {items.Length} items with {totalTokens} total tokens");
        });
    }

    /// <summary>
    /// Example 5: Report usage with different user context (delegation scenario)
    /// Useful when an agent acts on behalf of another user
    /// </summary>
    public static void Example5_DelegatedUserContext(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new XiansAgentRegistration { Name = "DelegationAgent" });
        var workflow = agent.Workflows.DefineBuiltIn("DelegationWorkflow");

        workflow.OnUserChatMessage(async (context) =>
        {
            // User A (current context) asks agent to do work for User B
            var delegatedUserId = "user-b-id";

            var result = await ProcessOnBehalfOfUserAsync(delegatedUserId);

            // Report usage under the delegated user's account
            await XiansContext.Metrics.Track(context)
                .WithUserId(delegatedUserId)  // Charge to delegated user
                .ForModel("gpt-4")
                .FromSource("DelegationAgent")
                .WithMetric("tokens", "total_tokens", result.TokensUsed, "tokens")
                .WithMetadata("delegated_by", context.Message.ParticipantId)
                .WithMetadata("delegation_reason", "authorized_access")
                .ReportAsync();

            await context.ReplyAsync($"Completed work on behalf of user {delegatedUserId}");
        });
    }

    /// <summary>
    /// Example 6: Mix auto-populated and custom fields
    /// Shows that you only need to override what's necessary
    /// </summary>
    public static void Example6_SelectiveOverrides(XiansPlatform platform)
    {
        var agent = platform.Agents.Register(new XiansAgentRegistration { Name = "SelectiveAgent" });
        var workflow = agent.Workflows.DefineBuiltIn("SelectiveWorkflow");

        workflow.OnUserChatMessage(async (context) =>
        {
            var response = await CallApiAsync();

            // Only override RequestId, let everything else auto-populate
            await XiansContext.Metrics.Track(context)
                .WithRequestId($"api-call-{Guid.NewGuid()}")  // Custom request ID for API correlation
                .ForModel("gpt-4")
                // TenantId, UserId, WorkflowId will be auto-populated from context
                .WithMetrics(
                    ("tokens", "input_tokens", response.InputTokens, "tokens"),
                    ("tokens", "output_tokens", response.OutputTokens, "tokens"),
                    ("tokens", "total_tokens", response.TotalTokens, "tokens")
                )
                .ReportAsync();

            await context.ReplyAsync("Done");
        });
    }

    // Helper method stubs for examples
    private static Task<ApiResponse> CallExternalApiAsync() => 
        Task.FromResult(new ApiResponse { TotalTokens = 250.0 });

    private static Task<ProcessResult> ProcessForTenantAsync(string tenantId) => 
        Task.FromResult(new ProcessResult { TokensUsed = 150.0 });

    private static Task<LongRunningResult> LongRunningProcessAsync() => 
        Task.FromResult(new LongRunningResult { TokensUsed = 500.0, ElapsedSeconds = 5.2 });

    private static Task<ProcessResult> ProcessItemAsync(string item) => 
        Task.FromResult(new ProcessResult { TokensUsed = 50.0 });

    private static Task<ProcessResult> ProcessOnBehalfOfUserAsync(string userId) => 
        Task.FromResult(new ProcessResult { TokensUsed = 200.0 });

    private static Task<ApiResponse> CallApiAsync() => 
        Task.FromResult(new ApiResponse { InputTokens = 100.0, OutputTokens = 75.0, TotalTokens = 175.0 });

    private class ApiResponse
    {
        public double TotalTokens { get; set; }
        public double InputTokens { get; set; }
        public double OutputTokens { get; set; }
    }

    private class ProcessResult
    {
        public double TokensUsed { get; set; }
    }

    private class LongRunningResult
    {
        public double TokensUsed { get; set; }
        public double ElapsedSeconds { get; set; }
    }
}
