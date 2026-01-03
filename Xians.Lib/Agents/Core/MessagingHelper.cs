using Xians.Lib.Agents.Messaging;

namespace Xians.Lib.Agents.Core;

/// <summary>
/// Helper for proactive user messaging operations.
/// For A2A (Agent-to-Agent) communication, use XiansContext.A2A instead.
/// </summary>
public class MessagingHelper
{

    /// <summary>
    /// Sends a chat message to a participant from the current workflow.
    /// Wrapper around UserMessaging.SendChatAsync for convenience.
    /// </summary>
    /// <param name="participantId">The participant (user) ID to send the message to.</param>
    /// <param name="text">The message text to send.</param>
    /// <param name="data">Optional data object to send with the message.</param>
    /// <param name="scope">Optional scope for the message.</param>
    /// <param name="hint">Optional hint for message processing.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public async Task SendChatAsync(
        string participantId, 
        string text, 
        object? data = null, 
        string? scope = null, 
        string? hint = null)
    {
        await UserMessaging.SendChatAsync(participantId, text, data, scope, hint);
    }

    /// <summary>
    /// Sends a data message to a participant from the current workflow.
    /// Wrapper around UserMessaging.SendDataAsync for convenience.
    /// </summary>
    /// <param name="participantId">The participant (user) ID to send the data to.</param>
    /// <param name="text">The text content to accompany the data.</param>
    /// <param name="data">The data object to send.</param>
    /// <param name="scope">Optional scope for the message.</param>
    /// <param name="hint">Optional hint for message processing.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public async Task SendDataAsync(
        string participantId, 
        string text, 
        object data, 
        string? scope = null, 
        string? hint = null)
    {
        await UserMessaging.SendDataAsync(participantId, text, data, scope, hint);
    }

    /// <summary>
    /// Sends a message while impersonating a different workflow.
    /// Useful for sending messages from background workflows as if they came from the main chat workflow.
    /// </summary>
    /// <param name="workflowName">The workflow name to impersonate (not the full workflow type).</param>
    /// <param name="participantId">The participant (user) ID to send the message to.</param>
    /// <param name="text">The message text to send.</param>
    /// <param name="data">Optional data object to send with the message.</param>
    /// <param name="scope">Optional scope for the message.</param>
    /// <param name="hint">Optional hint for message processing.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not in workflow or activity context.</exception>
    public async Task SendChatAsWorkflowAsync(
        string workflowName,
        string participantId, 
        string text, 
        object? data = null, 
        string? scope = null, 
        string? hint = null)
    {
        await UserMessaging.SendChatAsWorkflowAsync(workflowName, participantId, text, data, scope, hint);
    }
}

