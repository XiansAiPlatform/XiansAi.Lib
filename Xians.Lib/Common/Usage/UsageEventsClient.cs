using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Common.Usage;

/// <summary>
/// Client for reporting flexible usage metrics to the Xians platform.
/// Supports token usage, custom metrics, and activity tracking with both integer and fractional values.
/// Typically used via the fluent builder API (context.TrackUsage()) or extension methods.
/// </summary>
/// <example>
/// // Fluent builder pattern (recommended):
/// var response = await CallOpenAIAsync(prompt);
/// 
/// await context.TrackUsage()
///     .ForModel("gpt-4")
///     .WithCustomIdentifier(context.Message.Id)  // Link to message for tracking
///     .WithMetric(MetricCategories.Tokens, MetricTypes.TotalTokens, response.Usage.TotalTokens, "tokens")
///     .WithMetric("cost", "total_usd", 0.0025, "usd")  // Fractional values supported
///     .WithMetric("performance", "avg_response_ms", 123.45, "ms")  // Decimals for averages
///     .ReportAsync();
///
/// // Or with direct metrics array:
/// await context.ReportUsageAsync(
///     metrics: new List&lt;MetricValue&gt;
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

    /// <summary>
    /// Extracts token usage information from Microsoft.SemanticKernel ChatMessageContent responses.
    /// Returns actual tokens from the LLM response metadata.
    /// If no usage data is found, returns 0 for all token counts and logs a warning.
    /// </summary>
    /// <param name="responses">List of ChatMessageContent responses from Semantic Kernel.</param>
    /// <returns>Tuple containing token counts, model name, and completion ID.</returns>
    public (long promptTokens, long completionTokens, long totalTokens, string? model, string? completionId) 
        ExtractUsageFromSemanticKernelResponses(IEnumerable<object> responses)
    {
        if (responses == null || !responses.Any())
        {
            _logger.LogWarning("No responses provided for token extraction");
            return (0, 0, 0, null, null);
        }

        // Convert to list for easier iteration
        var responseList = responses.ToList();

        // Usage data typically appears in the last response when streaming
        // Iterate in reverse to check most recent messages first
        foreach (var response in Enumerable.Reverse(responseList))
        {
            // Try to access Metadata property via reflection
            var metadataProperty = response.GetType().GetProperty("Metadata");
            if (metadataProperty == null)
                continue;

            var metadata = metadataProperty.GetValue(response) as IReadOnlyDictionary<string, object?>;
            if (metadata == null || metadata.Count == 0)
                continue;
            
            long? promptTokens = null;
            long? completionTokens = null;
            long? totalTokens = null;
            
            // Try to extract from nested Usage object first (common format)
            if (metadata.TryGetValue("Usage", out var usageObj) && usageObj != null)
            {
                _logger.LogDebug("Found Usage object of type: {UsageType}", usageObj.GetType().FullName);
                
                // Try to get properties using reflection (handles OpenAI.Chat.ChatTokenUsage)
                promptTokens = TryGetPropertyValue(usageObj, "InputTokenCount", "PromptTokens", "InputTokens");
                completionTokens = TryGetPropertyValue(usageObj, "OutputTokenCount", "CompletionTokens", "OutputTokens");
                totalTokens = TryGetPropertyValue(usageObj, "TotalTokenCount", "TotalTokens");
            }
            
            // If we found any usage data, extract other metadata and return
            if (promptTokens.HasValue || completionTokens.HasValue || totalTokens.HasValue)
            {
                var pt = promptTokens ?? 0;
                var ct = completionTokens ?? 0;
                var tt = totalTokens ?? (pt + ct);
                
                // Try to get ModelId property
                var modelProperty = response.GetType().GetProperty("ModelId");
                var model = modelProperty?.GetValue(response) as string;
                
                _logger.LogDebug("Model: {Model}", model);
                var completionId = TryGetString(metadata, "Id", "id", "completion_id");
                
                _logger.LogInformation(
                    "Extracted token usage from LLM response: prompt={PromptTokens}, completion={CompletionTokens}, total={TotalTokens}, model={Model}",
                    pt, ct, tt, model ?? "null");
                
                return (pt, ct, tt, model, completionId);
            }
        }
        
        // No usage data found - log warning and return zeros
        _logger.LogWarning(
            "No usage metadata found in LLM response ({ResponseCount} responses checked). Token tracking will report 0 tokens. " +
            "Ensure your LLM provider returns usage information in the response metadata.",
            responseList.Count);
        
        return (0, 0, 0, null, null);
    }

    /// <summary>
    /// Tries to get a string value from metadata with multiple possible key names.
    /// </summary>
    private string? TryGetString(IReadOnlyDictionary<string, object?> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value) && value != null)
            {
                return value.ToString();
            }
        }
        return null;
    }

    /// <summary>
    /// Tries to get a property value from an object using reflection.
    /// Handles objects like OpenAI.Chat.ChatTokenUsage that have properties instead of dictionary access.
    /// </summary>
    private long? TryGetPropertyValue(object obj, params string[] propertyNames)
    {
        if (obj == null) return null;
        
        var type = obj.GetType();
        
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var property = type.GetProperty(propertyName, 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.IgnoreCase);
                
                if (property != null)
                {
                    var value = property.GetValue(obj);
                    if (value != null)
                    {
                        return Convert.ToInt64(value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get property '{PropertyName}' from type {TypeName}", propertyName, type.FullName);
            }
        }
        
        return null;
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


