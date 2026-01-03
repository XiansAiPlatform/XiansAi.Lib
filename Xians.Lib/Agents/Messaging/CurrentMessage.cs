using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Workflows;
using Xians.Lib.Agents.Core;
using Xians.Lib.Workflows.Messaging;
using Xians.Lib.Workflows.Messaging.Models;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Represents the current message context with messaging operations.
/// Contains the message text/data and provides reply, history, and hint operations.
/// REFACTORED: Uses MessageActivityExecutor for context-aware execution.
/// </summary>
public class CurrentMessage
{
    private readonly string _text;
    private readonly string _participantId;
    private readonly string _requestId;
    private readonly string? _scope;
    private readonly string? _hint;
    private readonly string? _authorization;
    private readonly string? _threadId;
    private readonly object? _data;
    private readonly string _tenantId;
    private readonly MessageActivityExecutor _executor;
    private readonly ILogger<CurrentMessage> _logger;

    /// <summary>
    /// When set to true, prevents messages from being sent to the user.
    /// Useful when you want to process messages without generating responses.
    /// </summary>
    public bool SkipResponse { get; set; } = false;

    /// <summary>Gets the text content of the message.</summary>
    public string Text => _text;

    /// <summary>The participant ID for this message context.</summary>
    public string ParticipantId => _participantId;

    /// <summary>The request ID for this message context.</summary>
    public string RequestId => _requestId;

    /// <summary>The scope for this message context, if any.</summary>
    public string? Scope => _scope;

    /// <summary>The hint for this message context, if any.</summary>
    public string? Hint => _hint;

    /// <summary>The authorization token for this message context, if any.</summary>
    public string? Authorization => _authorization;

    /// <summary>The thread ID for this message context, if any.</summary>
    public string? ThreadId => _threadId;

    /// <summary>The data associated with this message context, if any.</summary>
    public object? Data => _data;

    /// <summary>The tenant ID for this message context.</summary>
    public string TenantId => _tenantId;

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
        _text = text;
        _participantId = participantId;
        _requestId = requestId;
        _scope = scope;
        _hint = hint;
        _data = data;
        _tenantId = tenantId;
        _authorization = authorization;
        _threadId = threadId;
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<CurrentMessage>();
        
        // Initialize executor for context-aware execution
        var agent = XiansContext.CurrentAgent;
        var executorLogger = Common.Infrastructure.LoggerFactory.CreateLogger<MessageActivityExecutor>();
        _executor = new MessageActivityExecutor(agent, executorLogger);
    }

    /// <summary>
    /// Sends a reply to the user.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="response">The response text to send.</param>
    public virtual async Task ReplyAsync(string response)
    {
        await SendMessageToUserAsync(response, null);
    }

    /// <summary>
    /// Sends a reply with both text and data to the user.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="content">The text content to send.</param>
    /// <param name="data">The data object to send.</param>
    public virtual async Task ReplyWithDataAsync(string content, object? data)
    {
        await SendMessageToUserAsync(content, data);
    }

    /// <summary>
    /// Sends data to the user with optional text content.
    /// Primarily for sending structured data responses.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="data">The data object to send.</param>
    /// <param name="content">Optional text content to accompany the data.</param>
    public virtual async Task SendDataAsync(object data, string? content = null)
    {
        await SendMessageToUserAsync(content ?? string.Empty, data, "Data");
    }

    /// <summary>
    /// Retrieves paginated chat history for this conversation from the server.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="page">The page number to retrieve (default: 1).</param>
    /// <param name="pageSize">The number of messages per page (default: 10).</param>
    /// <returns>A list of DbMessage objects representing the chat history.</returns>
    public virtual async Task<List<DbMessage>> GetHistoryAsync(int page = 1, int pageSize = 10)
    {
        var workflowType = WorkflowContextHelper.GetWorkflowType();
        
        _logger.LogInformation(
            "Fetching chat history: WorkflowType={WorkflowType}, ParticipantId={ParticipantId}, Page={Page}, PageSize={PageSize}, Tenant={Tenant}",
            workflowType,
            _participantId,
            page,
            pageSize,
            _tenantId);

        // Shared business logic: Build request
        var request = BuildMessageHistoryRequest(workflowType, page, pageSize);

        // Context-aware execution via executor
        var messages = await _executor.GetHistoryAsync(request);

        _logger.LogInformation(
            "Chat history retrieved: {Count} messages, Tenant={Tenant}",
            messages.Count,
            _tenantId);

        return messages;
    }

    /// <summary>
    /// Retrieves the last hint for this conversation from the server.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <returns>The last hint string, or null if not found.</returns>
    public virtual async Task<string?> GetLastHintAsync()
    {
        var workflowType = WorkflowContextHelper.GetWorkflowType();
        
        _logger.LogInformation(
            "Fetching last hint: WorkflowType={WorkflowType}, ParticipantId={ParticipantId}, Tenant={Tenant}",
            workflowType,
            _participantId,
            _tenantId);

        // Shared business logic: Build request
        var request = BuildLastHintRequest(workflowType);

        // Context-aware execution via executor
        var hint = await _executor.GetLastHintAsync(request);

        _logger.LogInformation(
            "Last hint retrieved: Found={Found}, Tenant={Tenant}",
            hint != null,
            _tenantId);

        return hint;
    }

    /// <summary>
    /// Internal method to send messages back to the user.
    /// Context-aware: Uses activity in workflow, direct service call in activity.
    /// </summary>
    private async Task SendMessageToUserAsync(string content, object? data, string messageType = "Chat")
    {
        // Shared business logic: Check skip response flag
        if (SkipResponse)
        {
            _logger.LogDebug(
                "Skipping message send due to SkipResponse flag: ParticipantId={ParticipantId}, RequestId={RequestId}",
                _participantId,
                _requestId);
            return;
        }

        _logger.LogDebug(
            "Preparing to send message: ParticipantId={ParticipantId}, RequestId={RequestId}, ContentLength={ContentLength}, Tenant={Tenant}",
            _participantId,
            _requestId,
            content.Length,
            _tenantId);
        
        // Shared business logic: Build request
        var request = BuildSendMessageRequest(content, data, messageType);

        // Context-aware execution via executor
        await _executor.SendMessageAsync(request);

        _logger.LogInformation(
            "Message sent successfully: ParticipantId={ParticipantId}, RequestId={RequestId}",
            _participantId,
            _requestId);
    }

    #region Shared Business Logic Methods

    /// <summary>
    /// Builds a message history request.
    /// Shared business logic used by GetHistoryAsync.
    /// </summary>
    private GetMessageHistoryRequest BuildMessageHistoryRequest(string workflowType, int page, int pageSize)
    {
        return new GetMessageHistoryRequest
        {
            WorkflowType = workflowType,
            ParticipantId = _participantId,
            Scope = _scope,
            TenantId = _tenantId,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Builds a last hint request.
    /// Shared business logic used by GetLastHintAsync.
    /// </summary>
    private GetLastHintRequest BuildLastHintRequest(string workflowType)
    {
        return new GetLastHintRequest
        {
            WorkflowType = workflowType,
            ParticipantId = _participantId,
            Scope = _scope,
            TenantId = _tenantId
        };
    }

    /// <summary>
    /// Builds a send message request.
    /// Shared business logic used by SendMessageToUserAsync.
    /// </summary>
    private SendMessageRequest BuildSendMessageRequest(string content, object? data, string messageType)
    {
        return new SendMessageRequest
        {
            ParticipantId = _participantId,
            WorkflowId = WorkflowContextHelper.GetWorkflowId(),
            WorkflowType = WorkflowContextHelper.GetWorkflowType(),
            Text = content,
            Data = data ?? _data,
            RequestId = _requestId,
            Scope = _scope,
            ThreadId = _threadId,
            Authorization = _authorization,
            Hint = _hint,
            Origin = null,
            Type = messageType,
            TenantId = _tenantId
        };
    }

    #endregion
}

