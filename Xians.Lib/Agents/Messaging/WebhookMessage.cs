namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Represents a webhook message with its properties.
/// Contains the webhook name, payload, and context information.
/// </summary>
public class WebhookMessage
{
    /// <summary>Gets the participant ID for this webhook.</summary>
    public string ParticipantId { get; }

    /// <summary>Gets the scope for this webhook, if any.</summary>
    public string? Scope { get; }

    /// <summary>Gets the webhook name (transferred from the Text field).</summary>
    public string Name { get; }

    /// <summary>Gets the webhook payload (transferred from the Data field).</summary>
    public object? Payload { get; }

    /// <summary>Gets the authorization token for this webhook, if any.</summary>
    public string? Authorization { get; }

    /// <summary>Gets the request ID for this webhook.</summary>
    public string RequestId { get; }

    /// <summary>Gets the tenant ID for this webhook context.</summary>
    public string TenantId { get; }

    internal WebhookMessage(
        string participantId,
        string? scope,
        string name,
        object? payload,
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

