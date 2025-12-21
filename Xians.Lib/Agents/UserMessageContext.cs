using Xians.Lib.Temporal;

namespace Xians.Lib.Agents;

/// <summary>
/// Context provided to user message handlers.
/// </summary>
public class UserMessageContext
{
    private readonly string _participantId;
    private readonly string _requestId;
    private readonly string? _scope;

    /// <summary>
    /// Gets the user message.
    /// </summary>
    public UserMessage Message { get; private set; }

    internal UserMessageContext(UserMessage message)
    {
        Message = message;
        _participantId = string.Empty;
        _requestId = string.Empty;
    }

    internal UserMessageContext(UserMessage message, string participantId, string requestId, string? scope)
    {
        Message = message;
        _participantId = participantId;
        _requestId = requestId;
        _scope = scope;
    }

    /// <summary>
    /// Sends a reply to the user (synchronous wrapper).
    /// </summary>
    /// <param name="response">The response object to send.</param>
    public void Reply(object response)
    {
        ReplyAsync(response).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Sends a reply to the user asynchronously.
    /// </summary>
    /// <param name="response">The response object to send.</param>
    public async Task ReplyAsync(object response)
    {
        var content = response.ToString() ?? string.Empty;
        
        // Use the temporal client service to send the message back
        await TemporalClientService.SendChatOrDataAsync(
            participantId: _participantId,
            content: content,
            data: null,
            requestId: _requestId,
            scope: _scope,
            messageType: "Chat"
        );
    }

    /// <summary>
    /// Sends a reply with both text and data to the user.
    /// </summary>
    /// <param name="content">The text content to send.</param>
    /// <param name="data">The data object to send.</param>
    public async Task ReplyWithDataAsync(string content, object? data)
    {
        await TemporalClientService.SendChatOrDataAsync(
            participantId: _participantId,
            content: content,
            data: data,
            requestId: _requestId,
            scope: _scope,
            messageType: "Chat"
        );
    }
}

