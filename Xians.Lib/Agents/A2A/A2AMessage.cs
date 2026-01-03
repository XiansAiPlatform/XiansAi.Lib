using Xians.Lib.Agents.Messaging;

namespace Xians.Lib.Agents.A2A;

/// <summary>
/// Represents a message sent between agents.
/// </summary>
public class A2AMessage
{
    /// <summary>
    /// Gets or sets the text content of the message.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional data payload for the message.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Gets or sets optional metadata for the message.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets optional authorization token for the message.
    /// This will be passed to the target workflow context for authentication/authorization.
    /// </summary>
    public string? Authorization { get; set; }

    /// <summary>
    /// Gets or sets the original participant ID (user ID) from the source context.
    /// If not set, a correlation ID will be used.
    /// </summary>
    public string? ParticipantId { get; set; }

    /// <summary>
    /// Gets or sets the original request ID from the source context.
    /// If not set, a correlation ID will be used.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Gets or sets the scope for the message.
    /// If not set, defaults to "A2A".
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the hint for message processing.
    /// If not set, defaults to the source agent name.
    /// </summary>
    public string? Hint { get; set; }

    /// <summary>
    /// Gets or sets the thread ID for conversation tracking.
    /// If not set, a correlation ID will be used.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Creates an A2AMessage with context fields copied from a UserMessageContext.
    /// This preserves the original message context chain when forwarding messages.
    /// </summary>
    /// <param name="sourceContext">The source context to copy fields from.</param>
    /// <param name="text">Optional text for the message. If not provided, uses context.Message.Text.</param>
    /// <param name="data">Optional data for the message. If not provided, uses context.Data.</param>
    /// <returns>A new A2AMessage with context fields populated.</returns>
    public static A2AMessage FromContext(
        UserMessageContext sourceContext,
        string? text = null,
        object? data = null)
    {
        return new A2AMessage
        {
            Text = text ?? sourceContext.Message.Text,
            Data = data ?? sourceContext.Data,
            ParticipantId = sourceContext.ParticipantId,
            RequestId = sourceContext.RequestId,
            Scope = sourceContext.Scope,
            Hint = sourceContext.Hint,
            ThreadId = sourceContext.ThreadId,
            Authorization = sourceContext.Authorization,
            Metadata = sourceContext.Metadata
        };
    }
}

