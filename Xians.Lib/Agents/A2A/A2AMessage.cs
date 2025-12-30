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
}

