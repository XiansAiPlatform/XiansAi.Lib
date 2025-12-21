namespace Xians.Lib.Agents;

/// <summary>
/// Context provided to user message handlers.
/// </summary>
public class UserMessageContext
{
    /// <summary>
    /// Gets the user message.
    /// </summary>
    public UserMessage Message { get; private set; }

    internal UserMessageContext(UserMessage message)
    {
        Message = message;
    }

    /// <summary>
    /// Sends a reply to the user.
    /// </summary>
    /// <param name="response">The response object to send.</param>
    public void Reply(object response)
    {
        // TODO: Implement reply logic
    }
}

