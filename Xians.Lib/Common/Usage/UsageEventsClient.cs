using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Common.Usage;

/// <summary>
/// Client for reporting flexible usage metrics to the Xians platform.
/// Supports token usage, custom metrics, and activity tracking with both integer and fractional values.
/// Typically used via the fluent builder API (XiansContext.Metrics.Track()).
/// </summary>
/// <example>
/// // Fluent builder pattern (recommended):
/// var response = await CallOpenAIAsync(prompt);
/// 
/// // In message handlers:
/// await XiansContext.Metrics.Track(context)
///     .ForModel("gpt-4")
///     .WithCustomIdentifier($"msg-{Guid.NewGuid()}")  // Link to message for tracking
///     .WithMetric(MetricCategories.Tokens, MetricTypes.TotalTokens, response.Usage.TotalTokens, "tokens")
///     .WithMetric("cost", "total_usd", 0.0025, "usd")  // Fractional values supported
///     .WithMetric("performance", "avg_response_ms", 123.45, "ms")  // Decimals for averages
///     .ReportAsync();
///
/// // In workflows:
/// await XiansContext.Metrics.Track()
///     .ForModel("gpt-4")
///     .WithMetric(MetricCategories.Tokens, MetricTypes.TotalTokens, response.Usage.TotalTokens, "tokens")
///     .ReportAsync();
///
/// // Or with direct UsageReportRequest:
/// var request = new UsageReportRequest
/// {
///     Metrics = new List&lt;MetricValue&gt;
///     {
///         new() { Category = MetricCategories.Tokens, Type = MetricTypes.TotalTokens, Value = 150, Unit = "tokens" },
///         new() { Category = "cost", Type = "total_usd", Value = 0.0025, Unit = "usd" }
///     },
///     model: "gpt-4"
/// );
/// </example>
public class UsageEventsClient
{
    private static readonly Lazy<UsageEventsClient> _instance = new(() => new UsageEventsClient());
    
    /// <summary>
    /// Gets the singleton instance of the UsageEventsClient.
    /// </summary>
    public static UsageEventsClient Instance => _instance.Value;

    private readonly ILogger<UsageEventsClient> _logger;

    private UsageEventsClient()
    {
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<UsageEventsClient>();
    }

    /// <summary>
    /// Reports flexible usage metrics to the Xians platform server.
    /// This method is safe to call even if the HTTP service is not ready - it will log a warning and return.
    /// </summary>
    /// <param name="request">The usage report request containing metrics array.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ReportAsync(UsageReportRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get HTTP client from current agent context
            var agent = XiansContext.CurrentAgent;
            if (agent?.HttpService == null)
            {
                _logger.LogWarning("HTTP service not available for usage reporting. Skipping usage event.");
                return;
            }

            var client = agent.HttpService.Client;
            var json = JsonContent.Create(request, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Add tenant header for system-scoped agents
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/agent/usage/report");
            httpRequest.Content = json;
            
            if (!string.IsNullOrEmpty(request.TenantId))
            {
                httpRequest.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, request.TenantId);
            }

            var response = await client.SendAsync(httpRequest, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to report usage metrics. Status={StatusCode}, Payload={Payload}", 
                    response.StatusCode, 
                    payload);
            }
            else
            {
                _logger.LogDebug(
                    "Usage reported successfully: Model={Model}, MetricsCount={MetricsCount}",
                    request.Model,
                    request.Metrics?.Count ?? 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report usage metrics.");
        }
    }

}

/// <summary>
/// Request model for flexible metrics reporting.
/// Supports standard and custom metrics in a scalable array format.
/// </summary>
public class UsageReportRequest
{
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
    public string? WorkflowId { get; set; }
    public string? RequestId { get; set; }
    public string? Source { get; set; }
    public string? Model { get; set; }
    public string? CustomIdentifier { get; set; }
    public required List<MetricValue> Metrics { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Represents a single metric value with category, type, and unit.
/// </summary>
public class MetricValue
{
    public required string Category { get; set; }
    public required string Type { get; set; }
    public required double Value { get; set; }
    public string Unit { get; set; } = "count";
}

/// <summary>
/// Standard metric categories supported by the platform.
/// </summary>
public static class MetricCategories
{
    public const string Tokens = "tokens";
    public const string Activity = "activity";
    public const string Performance = "performance";
    public const string LlmUsage = "llm_usage";
}

/// <summary>
/// Standard metric types supported by the platform.
/// </summary>
public static class MetricTypes
{
    // Token metrics
    public const string PromptTokens = "prompt_tokens";
    public const string CompletionTokens = "completion_tokens";
    public const string TotalTokens = "total_tokens";
    
    // Activity metrics
    public const string MessageCount = "message_count";
    public const string WorkflowCompleted = "workflow_completed";
    public const string EmailSent = "email_sent";
    
    // Performance metrics
    public const string ResponseTimeMs = "response_time_ms";
    public const string ProcessingTimeMs = "processing_time_ms";
    
    // LLM usage metrics
    public const string LlmCalls = "llm_calls";
    public const string CacheHits = "cache_hits";
    public const string CacheMisses = "cache_misses";
}


