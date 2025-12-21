using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Workflows;

namespace Xians.Lib.Agents;

/// <summary>
/// Context provided to user message handlers.
/// Contains message information and methods to reply.
/// </summary>
public class UserMessageContext
{
    private readonly string _participantId;
    private readonly string _requestId;
    private readonly string _scope;
    private readonly string _hint;
    private readonly string? _authorization;
    private readonly string? _threadId;
    private readonly object _data;

    /// <summary>
    /// Gets the user message.
    /// </summary>
    public UserMessage Message { get; private set; }

    /// <summary>
    /// Gets the participant ID (user ID).
    /// </summary>
    public string ParticipantId => _participantId;

    /// <summary>
    /// Gets the request ID for tracking.
    /// </summary>
    public string RequestId => _requestId;

    /// <summary>
    /// Gets the scope of the message.
    /// </summary>
    public string Scope => _scope;

    /// <summary>
    /// Gets the hint for message processing.
    /// </summary>
    public string Hint => _hint;

    /// <summary>
    /// Gets the thread ID for conversation tracking.
    /// </summary>
    public string? ThreadId => _threadId;

    /// <summary>
    /// Gets the data object associated with the message.
    /// </summary>
    public object Data => _data;

    internal UserMessageContext(UserMessage message)
    {
        Message = message;
        _participantId = string.Empty;
        _requestId = string.Empty;
        _scope = string.Empty;
        _hint = string.Empty;
        _data = new object();
    }

    internal UserMessageContext(
        UserMessage message, 
        string participantId, 
        string requestId, 
        string scope,
        string hint,
        object data,
        string? authorization = null,
        string? threadId = null)
    {
        Message = message;
        _participantId = participantId;
        _requestId = requestId;
        _scope = scope;
        _hint = hint;
        _data = data;
        _authorization = authorization;
        _threadId = threadId;
    }

    /// <summary>
    /// Sends a reply to the user (synchronous wrapper).
    /// Note: Prefer using ReplyAsync in async contexts.
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
        await SendMessageToUserAsync(content, null);
    }

    /// <summary>
    /// Sends a reply with both text and data to the user.
    /// </summary>
    /// <param name="content">The text content to send.</param>
    /// <param name="data">The data object to send.</param>
    public async Task ReplyWithDataAsync(string content, object? data)
    {
        await SendMessageToUserAsync(content, data);
    }

    /// <summary>
    /// Internal method to send messages back to the user via Temporal activity.
    /// Uses Workflow.ExecuteActivityAsync to ensure proper determinism and retry handling.
    /// Exceptions bubble up to be handled by the workflow's top-level event loop.
    /// </summary>
    private async Task SendMessageToUserAsync(string content, object? data)
    {
        Workflow.Logger.LogDebug(
            "Preparing to send message: ParticipantId={ParticipantId}, RequestId={RequestId}, ContentLength={ContentLength}",
            _participantId,
            _requestId,
            content?.Length ?? 0);
        
        var request = new SendMessageRequest
        {
            ParticipantId = _participantId,
            WorkflowId = Workflow.Info.WorkflowId,
            WorkflowType = Workflow.Info.WorkflowType,
            Text = content,
            Data = data ?? _data, // Use provided data or original message data
            RequestId = _requestId,
            Scope = _scope,
            ThreadId = _threadId,
            Authorization = _authorization,
            Hint = _hint, // Pass through the hint from the original message
            Origin = null,
            Type = "Chat"
        };

        Workflow.Logger.LogDebug(
            "Executing SendMessage activity: WorkflowId={WorkflowId}, WorkflowType={WorkflowType}, Endpoint=api/agent/conversation/outbound/chat",
            request.WorkflowId,
            request.WorkflowType);

        // Execute as Temporal activity for proper determinism, retries, and observability
        await Workflow.ExecuteActivityAsync(
            (MessageActivities act) => act.SendMessageAsync(request),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(30),
                RetryPolicy = new()
                {
                    MaximumAttempts = 3,
                    InitialInterval = TimeSpan.FromSeconds(1),
                    MaximumInterval = TimeSpan.FromSeconds(10),
                    BackoffCoefficient = 2
                }
            });
        
        Workflow.Logger.LogDebug(
            "Message sent successfully: ParticipantId={ParticipantId}, RequestId={RequestId}",
            _participantId,
            _requestId);
    }
}

