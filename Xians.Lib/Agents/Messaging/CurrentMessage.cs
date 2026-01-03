namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Represents the current message with its properties.
/// Contains message text, data, and context information.
/// </summary>
public class CurrentMessage
{
    /// <summary>Gets the text content of the message.</summary>
    public string Text { get; }

    /// <summary>The participant ID for this message context.</summary>
    public string ParticipantId { get; }

    /// <summary>The request ID for this message context.</summary>
    public string RequestId { get; }

    /// <summary>The scope for this message context, if any.</summary>
    public string? Scope { get; }

    /// <summary>The hint for this message context, if any.</summary>
    public string? Hint { get; }

    /// <summary>The authorization token for this message context, if any.</summary>
    public string? Authorization { get; }

    /// <summary>The thread ID for this message context, if any.</summary>
    public string? ThreadId { get; }

    /// <summary>The data associated with this message context, if any.</summary>
    public object? Data { get; }

    /// <summary>The tenant ID for this message context.</summary>
    public string TenantId { get; }

    internal CurrentMessage(
        string text,
        string participantId,
        string requestId,
        string? scope,
        string? hint,
        object? data,
        string tenantId,
        string? authorization = null,
        string? threadId = null)
    {
        Text = text;
        ParticipantId = participantId;
        RequestId = requestId;
        Scope = scope;
        Hint = hint;
        Data = data;
        TenantId = tenantId;
        Authorization = authorization;
        ThreadId = threadId;
    }
}
