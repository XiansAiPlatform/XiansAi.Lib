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
    private readonly string _tenantId;

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
    /// Gets the tenant ID for this workflow instance.
    /// For system-scoped agents, this indicates which tenant initiated the workflow.
    /// For non-system-scoped agents, this is always the agent's registered tenant.
    /// </summary>
    public string TenantId => _tenantId;

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
        _tenantId = string.Empty;
    }

    internal UserMessageContext(
        UserMessage message, 
        string participantId, 
        string requestId, 
        string scope,
        string hint,
        object data,
        string tenantId,
        string? authorization = null,
        string? threadId = null)
    {
        Message = message;
        _participantId = participantId;
        _requestId = requestId;
        _scope = scope;
        _hint = hint;
        _data = data;
        _tenantId = tenantId;
        _authorization = authorization;
        _threadId = threadId;
    }

    /// <summary>
    /// Sends a reply to the user (synchronous wrapper).
    /// Note: Prefer using ReplyAsync in async contexts.
    /// </summary>
    /// <param name="response">The response object to send.</param>
    public virtual async Task ReplyAsync(string response)
    {
        await SendMessageToUserAsync(response, null);
    }

    /// <summary>
    /// Sends a reply with both text and data to the user.
    /// </summary>
    /// <param name="content">The text content to send.</param>
    /// <param name="data">The data object to send.</param>
    public virtual async Task ReplyWithDataAsync(string content, object? data)
    {
        await SendMessageToUserAsync(content, data);
    }

    /// <summary>
    /// Retrieves paginated chat history for this conversation from the server.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// </summary>
    /// <param name="page">The page number to retrieve (default: 1).</param>
    /// <param name="pageSize">The number of messages per page (default: 10).</param>
    /// <returns>A list of DbMessage objects representing the chat history.</returns>
    public virtual async Task<List<DbMessage>> GetChatHistoryAsync(int page = 1, int pageSize = 10)
    {
        Workflow.Logger.LogInformation(
            "Fetching chat history: WorkflowType={WorkflowType}, ParticipantId={ParticipantId}, Page={Page}, PageSize={PageSize}, Tenant={Tenant}",
            Workflow.Info.WorkflowType,
            _participantId,
            page,
            pageSize,
            _tenantId);

        var request = new GetMessageHistoryRequest
        {
            WorkflowType = Workflow.Info.WorkflowType,
            ParticipantId = _participantId,
            Scope = _scope,
            TenantId = _tenantId,  // Use tenant ID from workflow context
            Page = page,
            PageSize = pageSize
        };

        // Execute as Temporal activity for proper determinism and retries
        var messages = await Workflow.ExecuteActivityAsync(
            (MessageActivities act) => act.GetMessageHistoryAsync(request),
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

        // Filter out the current message to avoid duplication
        // When retrieving history within a message handler, the current message
        // is already being processed and should not be included in the history
        var filteredMessages = messages.Where(m => 
            !(m.Direction.Equals("inbound", StringComparison.OrdinalIgnoreCase) && 
              m.Text == Message.Text)).ToList();

        Workflow.Logger.LogInformation(
            "Chat history fetched: {Count} messages (filtered from {Total}), Tenant={Tenant}",
            filteredMessages.Count,
            messages.Count,
            _tenantId);
    

        return filteredMessages;
    }

    /// <summary>
    /// Internal method to send messages back to the user via Temporal activity.
    /// Uses Workflow.ExecuteActivityAsync to ensure proper determinism and retry handling.
    /// Exceptions bubble up to be handled by the workflow's top-level event loop.
    /// </summary>
    private async Task SendMessageToUserAsync(string content, object? data)
    {
        Workflow.Logger.LogDebug(
            "Preparing to send message: ParticipantId={ParticipantId}, RequestId={RequestId}, ContentLength={ContentLength}, Tenant={Tenant}",
            _participantId,
            _requestId,
            content?.Length ?? 0,
            _tenantId);
        
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
            Type = "Chat",
            TenantId = _tenantId  // Pass tenant context for system-scoped agents
        };

        Workflow.Logger.LogDebug(
            "Executing SendMessage activity: WorkflowId={WorkflowId}, WorkflowType={WorkflowType}, Tenant={Tenant}, Endpoint=api/agent/conversation/outbound/chat",
            request.WorkflowId,
            request.WorkflowType,
            _tenantId);

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

