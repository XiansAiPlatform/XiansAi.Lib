using System.Text.Json.Serialization;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Represents a webhook message with its properties.
/// Contains the webhook name, payload, and context information.
/// </summary>
public class WebhookMessage
{
    /// <summary>Gets the participant ID for this webhook.</summary>
    public string ParticipantId { get; init; } = string.Empty;

    /// <summary>Gets the scope for this webhook, if any.</summary>
    public string? Scope { get; init; }

    /// <summary>Gets the webhook name (transferred from the Text field).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the webhook payload (transferred from the Data field).</summary>
    public string? Payload { get; init; }

    /// <summary>Gets the authorization token for this webhook, if any.</summary>
    public string? Authorization { get; init; }

    /// <summary>Gets the request ID for this webhook.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Gets the tenant ID for this webhook context.</summary>
    public string TenantId { get; init; } = string.Empty;

    [JsonConstructor]
    public WebhookMessage() { }

    internal WebhookMessage(
        string participantId,
        string? scope,
        string name,
        string? payload,
        string? authorization,
        string requestId,
        string tenantId)
    {
        ParticipantId = participantId;
        Scope = scope;
        Name = name;
        Payload = payload;
        Authorization = authorization;
        RequestId = requestId;
        TenantId = tenantId;
    }
}

