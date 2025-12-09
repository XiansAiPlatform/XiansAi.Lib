using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Server;

public class UsageEventsClient
{
    private static readonly Lazy<UsageEventsClient> _instance = new(() => new UsageEventsClient());
    public static UsageEventsClient Instance => _instance.Value;

    private readonly ILogger<UsageEventsClient> _logger = Globals.LogFactory.CreateLogger<UsageEventsClient>();

    private UsageEventsClient()
    {
    }

    public async Task ReportAsync(UsageEventRecord report, CancellationToken cancellationToken = default)
    {
        if (!SecureApi.IsReady)
        {
            return;
        }

        try
        {
            var client = SecureApi.Instance.Client;
            var json = JsonContent.Create(report, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var response = await client.PostWithRetryAsync("/api/agent/usage/report", json, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to report token usage. Status={StatusCode}, Payload={Payload}", response.StatusCode, payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report token usage metrics.");
        }
    }

    /// <summary>
    /// Extracts token usage information from LLM response metadata.
    /// Returns actual tokens from ChatMessageContent responses.
    /// If no usage data is found, returns 0 for all token counts and logs a warning.
    /// </summary>
    /// <param name="responses">List of ChatMessageContent responses from the LLM</param>
    /// <returns>Tuple containing token counts, model name, and completion ID</returns>
    public (long promptTokens, long completionTokens, long totalTokens, string? model, string? completionId) 
        ExtractUsageFromResponses(List<ChatMessageContent> responses)
    {
        if (responses == null || responses.Count == 0)
        {
            _logger.LogWarning("No responses provided for token extraction");
            return (0, 0, 0, null, null);
        }

        // Usage data typically appears in the last response when streaming
        // Iterate in reverse to check most recent messages first
        foreach (var response in Enumerable.Reverse(responses))
        {
            if (response.Metadata == null || response.Metadata.Count == 0) 
                continue;
            
            long? promptTokens = null;
            long? completionTokens = null;
            long? totalTokens = null;
            
            // Try to extract from nested Usage object first (common format)
            if (response.Metadata.TryGetValue("Usage", out var usageObj))
            {
                _logger.LogDebug("Found Usage object of type: {UsageType}", usageObj?.GetType().FullName);
                
                // Handle usage object using reflection (works with OpenAI.Chat.ChatTokenUsage and other types)
                if (usageObj != null)
                {
                    // Try to get properties using reflection (handles OpenAI.Chat.ChatTokenUsage)
                    promptTokens = TryGetPropertyValue(usageObj, "InputTokenCount", "PromptTokens", "InputTokens");
                    completionTokens = TryGetPropertyValue(usageObj, "OutputTokenCount", "CompletionTokens", "OutputTokens");
                    totalTokens = TryGetPropertyValue(usageObj, "TotalTokenCount", "TotalTokens");
                }
            }
            
            // If we found any usage data, extract other metadata and return
            if (promptTokens.HasValue || completionTokens.HasValue || totalTokens.HasValue)
            {
                var pt = promptTokens ?? 0;;
                var ct = completionTokens ?? 0;
                var tt = totalTokens ?? (pt + ct);
                
                var model = response.ModelId;
                _logger.LogInformation("Model: {Model}", model);
                var completionId = TryGetString(response.Metadata, "Id", "id", "completion_id");
                
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
            responses.Count);
        
        return (0, 0, 0, null, null);
    }

    /// <summary>
    /// Tries to get a long value from metadata dictionary with multiple possible key names.
    /// </summary>
    private long? TryGetLong(IReadOnlyDictionary<string, object?> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    return Convert.ToInt64(value);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to convert metadata key '{Key}' to long: {Value}", key, value);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Tries to get a long value from a nested dictionary.
    /// </summary>
    private long? TryGetLongFromDict(IDictionary<string, object> dict, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                try
                {
                    return Convert.ToInt64(value);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to convert nested key '{Key}' to long: {Value}", key, value);
                }
            }
        }
        return null;
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
    Dictionary<string, string>? Metadata,
    long? ResponseTimeMs = null);
