namespace Xians.Lib.Common.Models;

/// <summary>
/// Defines the types of messages that can be processed by the platform.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Chat message type for human-readable text conversations.
    /// </summary>
    Chat,

    /// <summary>
    /// Data message type for structured data exchanges.
    /// </summary>
    Data,

    /// <summary>
    /// Webhook message type for HTTP-style webhook requests and responses.
    /// </summary>
    Webhook,

    /// <summary>
    /// Handoff message type for transferring conversations between agents.
    /// </summary>
    Handoff
}

/// <summary>
/// Extension methods for MessageType enum.
/// </summary>
public static class MessageTypeExtensions
{
    /// <summary>
    /// Gets the lowercase string representation of the message type.
    /// </summary>
    public static string ToLowerString(this MessageType type)
    {
        return type.ToString().ToLower();
    }

    /// <summary>
    /// Parses a string to a MessageType enum.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="ignoreCase">Whether to ignore case when parsing.</param>
    /// <returns>The parsed MessageType.</returns>
    /// <exception cref="ArgumentException">Thrown when the value cannot be parsed.</exception>
    public static MessageType ParseMessageType(string value, bool ignoreCase = true)
    {
        if (Enum.TryParse<MessageType>(value, ignoreCase, out var result))
        {
            return result;
        }

        throw new ArgumentException($"Invalid message type: {value}. Valid types are: {string.Join(", ", GetAllowedTypes())}", nameof(value));
    }

    /// <summary>
    /// Tries to parse a string to a MessageType enum.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="result">The parsed MessageType if successful.</param>
    /// <param name="ignoreCase">Whether to ignore case when parsing.</param>
    /// <returns>True if parsing was successful, false otherwise.</returns>
    public static bool TryParseMessageType(string value, out MessageType result, bool ignoreCase = true)
    {
        return Enum.TryParse(value, ignoreCase, out result);
    }

    /// <summary>
    /// Gets all allowed message type names in lowercase.
    /// </summary>
    public static string[] GetAllowedTypes()
    {
        return Enum.GetNames<MessageType>().Select(t => t.ToLower()).ToArray();
    }

    /// <summary>
    /// Checks if a string is a valid message type.
    /// </summary>
    public static bool IsValidMessageType(string value, bool ignoreCase = true)
    {
        return Enum.TryParse<MessageType>(value, ignoreCase, out _);
    }
}

