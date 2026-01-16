using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xians.Lib.Common.Infrastructure;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Common.Usage;

/// <summary>
/// Client for reporting token usage and LLM metrics to the Xians platform.
/// This is a utility service that developers can use to track LLM usage in their agents.
/// </summary>
/// <example>
/// // In your message handler:
/// var response = await CallOpenAIAsync(prompt);
/// 
/// await UsageEventsClient.Instance.ReportAsync(new UsageEventRecord(
///     TenantId: context.TenantId,
///     UserId: context.ParticipantId,
///     Model: "gpt-4",
///     PromptTokens: response.Usage.PromptTokens,
///     CompletionTokens: response.Usage.CompletionTokens,
///     TotalTokens: response.Usage.TotalTokens,
///     MessageCount: 1,
///     WorkflowId: XiansContext.WorkflowId,
///     RequestId: context.RequestId,
///     Source: "MyAgent.ChatHandler"
/// ));
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
    /// Reports token usage to the Xians platform server.
    /// This method is safe to call even if the HTTP service is not ready - it will log a warning and return.
    /// </summary>
    /// <param name="record">The usage event record containing token counts and metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ReportAsync(UsageEventRecord record, CancellationToken cancellationToken = default)
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
            var json = JsonContent.Create(record, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Add tenant header for system-scoped agents
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/agent/usage/report");
            request.Content = json;
            
            if (!string.IsNullOrEmpty(record.TenantId))
            {
                request.Headers.TryAddWithoutValidation(WorkflowConstants.Headers.TenantId, record.TenantId);
            }

            var response = await client.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to report token usage. Status={StatusCode}, Payload={Payload}", 
                    response.StatusCode, 
                    payload);
            }
            else
            {
                _logger.LogDebug(
                    "Usage reported successfully: Model={Model}, PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}, TotalTokens={TotalTokens}",
                    record.Model,
                    record.PromptTokens,
                    record.CompletionTokens,
                    record.TotalTokens);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report token usage metrics.");
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
/// Record representing a token usage event to be reported to the platform.
/// </summary>
/// <param name="TenantId">The tenant ID associated with this usage.</param>
/// <param name="UserId">The user/participant ID who triggered this usage.</param>
/// <param name="Model">The LLM model used (e.g., "gpt-4", "claude-3-opus").</param>
/// <param name="PromptTokens">Number of tokens in the prompt/input.</param>
/// <param name="CompletionTokens">Number of tokens in the completion/output.</param>
/// <param name="TotalTokens">Total tokens used (prompt + completion).</param>
/// <param name="MessageCount">Number of messages in the conversation context.</param>
/// <param name="WorkflowId">The workflow ID where this usage occurred.</param>
/// <param name="RequestId">The request ID for correlation.</param>
/// <param name="Source">Source identifier (e.g., "MyAgent.ChatHandler", "MyAgent.KnowledgeRetrieval").</param>
/// <param name="Metadata">Optional metadata dictionary for additional context.</param>
/// <param name="ResponseTimeMs">Optional response time in milliseconds.</param>
public record UsageEventRecord(
    string TenantId,
    string UserId,
    string? Model,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens,
    long MessageCount,
    string? WorkflowId,
    string? RequestId,
    string? Source,
    Dictionary<string, string>? Metadata = null,
    long? ResponseTimeMs = null);


