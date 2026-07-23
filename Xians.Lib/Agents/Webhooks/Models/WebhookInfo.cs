using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xians.Lib.Agents.Webhooks.Models;

/// <summary>
/// Represents a builtin (inbound) webhook registered for an agent activation in the current tenant.
/// A builtin webhook exposes an HTTP endpoint (see <see cref="WebhookUrl"/>) that external systems can
/// call to deliver an event to the agent's workflow (handled via <c>OnWebhook</c>).
/// Maps the server's app-integration response for the <c>builtin_webhook</c> platform.
/// </summary>
public class WebhookInfo
{
    /// <summary>Unique identifier of the webhook (integration id). Use this with <c>DeleteAsync</c>.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable name of the webhook.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>The agent that owns the webhook.</summary>
    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>The activation the webhook targets.</summary>
    [JsonPropertyName("activationName")]
    public string ActivationName { get; set; } = string.Empty;

    /// <summary>The workflow id the webhook delivers to.</summary>
    [JsonPropertyName("workflowId")]
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// The relative webhook URL (append to your server's base URL) that external systems should call
    /// to trigger the webhook. Typically points at <c>/api/user/webhooks/builtin?...</c>.
    /// </summary>
    [JsonPropertyName("webhookUrl")]
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>Whether the webhook is currently enabled.</summary>
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    /// <summary>When the webhook was created (UTC).</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Platform-specific configuration (non-sensitive). For builtin webhooks this contains
    /// <c>workflowName</c>, <c>webhookName</c>, <c>participantId</c> and <c>timeoutInSeconds</c>.
    /// Prefer the strongly-typed convenience properties below.
    /// </summary>
    [JsonPropertyName("configuration")]
    public Dictionary<string, JsonElement>? Configuration { get; set; }

    /// <summary>The workflow name the webhook delivers to (from <see cref="Configuration"/>).</summary>
    [JsonIgnore]
    public string? WorkflowName => GetConfigString("workflowName");

    /// <summary>The webhook name/scope used when triggering (from <see cref="Configuration"/>).</summary>
    [JsonIgnore]
    public string? WebhookName => GetConfigString("webhookName");

    /// <summary>The participant id the webhook runs as (from <see cref="Configuration"/>).</summary>
    [JsonIgnore]
    public string? ParticipantId => GetConfigString("participantId");

    /// <summary>The synchronous response timeout in seconds (from <see cref="Configuration"/>).</summary>
    [JsonIgnore]
    public int? TimeoutInSeconds => GetConfigInt("timeoutInSeconds");

    private string? GetConfigString(string key)
    {
        if (Configuration != null && Configuration.TryGetValue(key, out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => value.ToString()
            };
        }
        return null;
    }

    private int? GetConfigInt(string key)
    {
        if (Configuration != null && Configuration.TryGetValue(key, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
                return n;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var s))
                return s;
        }
        return null;
    }
}
