using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Core;
using Xians.Lib.Agents.Metrics;
using Xians.Lib.Temporal.Workflows.Messaging.Models;

namespace Xians.Lib.Agents.Messaging;

/// <summary>
/// Context provided to user message handlers.
/// Contains message-specific information and messaging operations.
/// For agent-wide operations (Knowledge, Documents, Schedules), use XiansContext.CurrentAgent or XiansContext.CurrentWorkflow.
/// All operations are workflow-aware and handle both workflow and activity contexts.
/// 
/// Access message properties (ParticipantId, RequestId, TenantId, etc.) via the Message property.
/// </summary>
public class UserMessageContext
{
    private readonly Dictionary<string, string>? _metadata;
    private readonly MessageActivityExecutor? _executor;
    private readonly ILogger<UserMessageContext> _logger;
    private readonly string? _cachedWorkflowId;

    /// <summary>
    /// Gets the current message with text, data, and context information.
    /// Use this to access ParticipantId, RequestId, TenantId, Scope, Hint, ThreadId, Data, Authorization, etc.
    /// </summary>
    public virtual CurrentMessage Message { get; protected set; }

    /// <summary>
    /// Gets the optional metadata for the message.
    /// </summary>
    public Dictionary<string, string>? Metadata => _metadata;

    /// <summary>
    /// Gets a metrics builder for tracking usage metrics, pre-initialized with this message context.
    /// Automatically populates TenantId, ParticipantId, WorkflowId, RequestId, AgentName, and ActivationName.
    /// </summary>
    /// <example>
    /// <code>
    /// await context.Metrics
    ///     .ForModel("gpt-4")
    ///     .WithMetric("tokens", "total", 150, "tokens")
    ///     .ReportAsync();
    /// </code>
    /// </example>
    public ContextAwareUsageReportBuilder Metrics => XiansContext.CurrentAgent.Metrics.Track(this);

    /// <summary>
    /// When set to true, prevents messages from being sent to the user.
    /// Useful when you want to process messages without generating responses.
    /// </summary>
    public bool SkipResponse { get; set; } = false;

    internal UserMessageContext(
        string text, 
        string participantId, 
        string requestId, 
        string? scope,
        string? hint,
        object? data,
        string tenantId,
        string? authorization = null,
        string? threadId = null,
        Dictionary<string, string>? metadata = null)
    {
        _metadata = metadata;
        _logger = Common.Infrastructure.LoggerFactory.CreateLogger<UserMessageContext>();

        // Cache workflow ID from context if available
        _cachedWorkflowId = XiansContext.SafeWorkflowId;

        // Initialize current message with context
        Message = new CurrentMessage(
            text, participantId, requestId, scope, hint, data, tenantId, authorization, threadId);

        // Initialize executor for context-aware execution
        var agent = XiansContext.CurrentAgent;
        var executorLogger = Common.Infrastructure.LoggerFactory.CreateLogger<MessageActivityExecutor>();
        _executor = new MessageActivityExecutor(agent, executorLogger);
    }

    /// <summary>
    /// Sends a simple text reply to the user.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="text">The text content to send.</param>
    public virtual async Task ReplyAsync(string text)
    {
        await SendMessageToUserAsync(text, null);
    }

    /// <summary>
    /// Sends a chat reply with both text and data to the user.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="text">The text content to send.</param>
    /// <param name="data">The data object to send.</param>
    public virtual async Task ReplyAsync(string text, object? data)
    {
        await SendMessageToUserAsync(text, data);
    }

    /// <summary>
    /// Sends data message to the user with optional text content.
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
    public virtual async Task<List<DbMessage>> GetChatHistoryAsync(int page = 1, int pageSize = 10)
    {
        
        Console.WriteLine($"***** Fetching chat history: WorkflowId={_cachedWorkflowId}, ParticipantId={Message.ParticipantId}, Page={page}, PageSize={pageSize}, Tenant={Message.TenantId}");
        _logger.LogDebug(
            "Fetching chat history: WorkflowId={WorkflowId}, ParticipantId={ParticipantId}, Page={Page}, PageSize={PageSize}, Tenant={Tenant}",
            _cachedWorkflowId,
            Message.ParticipantId,
            page,
            pageSize,
            Message.TenantId);

        // Shared business logic: Build request
        var request = BuildMessageHistoryRequest( page, pageSize);

        // Context-aware execution via executor
        if (_executor == null)
        {
            throw new InvalidOperationException("MessageActivityExecutor is not available. This typically means the context was created outside a workflow and no agent is registered.");
        }
        var messages = await _executor.GetHistoryAsync(request);

        _logger.LogDebug(
            "Chat history retrieved: {Count} messages, Tenant={Tenant}",
            messages.Count,
            Message.TenantId);

        return messages;
    }

    /// <summary>
    /// Retrieves the last task ID for this conversation from the server.
    /// For system-scoped agents, uses tenant ID from workflow context.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <returns>The last task ID string, or null if not found.</returns>
    public virtual async Task<string?> GetLastTaskIdAsync()
    {
        
        _logger.LogDebug(
            "Fetching last task ID: WorkflowId={WorkflowId}, ParticipantId={ParticipantId}, Tenant={Tenant}",
            XiansContext.WorkflowId,
            Message.ParticipantId,
            Message.TenantId);

        // Shared business logic: Build request
        var request = BuildLastTaskIdRequest();

        // Context-aware execution via executor
        if (_executor == null)
        {
            throw new InvalidOperationException("MessageActivityExecutor is not available. This typically means the context was created outside a workflow and no agent is registered.");
        }
        var taskId = await _executor.GetLastTaskIdAsync(request);

        _logger.LogDebug(
            "Last task ID retrieved: Found={Found}, Tenant={Tenant}",
            taskId != null,
            Message.TenantId);

        return taskId;
    }


    /// <summary>
    /// Sends a handoff request to transfer the conversation to another workflow using its workflow ID.
    /// Works in both workflow and activity contexts.
    /// </summary>
    /// <param name="targetWorkflowId">The workflow ID of the target workflow to hand off to.</param>
    /// <param name="message">Optional custom message for the handoff. If null, uses the current message text.</param>
    /// <param name="data">Optional data to pass with the handoff. If null, uses the current message data.</param>
    /// <param name="userMessage">Optional message to send to the user before the handoff.</param>
    /// <returns>The response from the handoff operation.</returns>
    public virtual async Task<string?> SendHandoffAsync(string targetWorkflowId, string? message = null, object? data = null, string? userMessage = null)
    {
        if (string.IsNullOrEmpty(targetWorkflowId))
        {
            throw new ArgumentException("Target workflow ID cannot be null or empty", nameof(targetWorkflowId));
        }

        // Send message to user if provided
        if (!string.IsNullOrEmpty(userMessage))
        {
            await ReplyAsync(userMessage);
        }

        return await SendHandoffInternalAsync(targetWorkflowId, null, message, data);
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
                Message.ParticipantId,
                Message.RequestId);
            return;
        }

        _logger.LogDebug(
            "Preparing to send message: ParticipantId={ParticipantId}, RequestId={RequestId}, ContentLength={ContentLength}, Tenant={Tenant}",
            Message.ParticipantId,
            Message.RequestId,
            content.Length,
            Message.TenantId);
        
        // Shared business logic: Build request
        var request = BuildSendMessageRequest(content, data, messageType);

        // Context-aware execution via executor
        if (_executor == null)
        {
            throw new InvalidOperationException("MessageActivityExecutor is not available. This typically means the context was created outside a workflow and no agent is registered.");
        }
        await _executor.SendMessageAsync(request);

        _logger.LogDebug(
            "Message sent successfully: ParticipantId={ParticipantId}, RequestId={RequestId}",
            Message.ParticipantId,
            Message.RequestId);
    }

    /// <summary>
    /// Internal method to send handoff requests.
    /// Context-aware: Uses activity in workflow, direct service call in activity.
    /// </summary>
    private async Task<string?> SendHandoffInternalAsync(string targetWorkflowId, string? targetWorkflowType, string? message, object? data)
    {
        if (string.IsNullOrEmpty(Message.ThreadId))
        {
            throw new InvalidOperationException("ThreadId is required for handoff operations");
        }

        // Use provided message or fall back to current message text
        var text = message ?? Message.Text;
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidOperationException("Message text is required for handoff");
        }

        _logger.LogDebug(
            "Preparing to send handoff: TargetWorkflowId={TargetWorkflowId}, TargetWorkflowType={TargetWorkflowType}, Tenant={Tenant}",
            targetWorkflowId,
            targetWorkflowType,
            Message.TenantId);

        // Shared business logic: Build request
        var request = BuildSendHandoffRequest(targetWorkflowId, targetWorkflowType, text, data);

        // Context-aware execution via executor
        if (_executor == null)
        {
            throw new InvalidOperationException("MessageActivityExecutor is not available. This typically means the context was created outside a workflow and no agent is registered.");
        }
        var result = await _executor.SendHandoffAsync(request);

        _logger.LogDebug(
            "Handoff sent successfully: TargetWorkflowId={TargetWorkflowId}, TargetWorkflowType={TargetWorkflowType}",
            targetWorkflowId,
            targetWorkflowType);

        return result;
    }

    #region Shared Business Logic Methods

    /// <summary>
    /// Builds a message history request.
    /// Shared business logic used by GetChatHistoryAsync.
    /// </summary>
    private GetMessageHistoryRequest BuildMessageHistoryRequest( int page, int pageSize)
    {
        return new GetMessageHistoryRequest
        {
            WorkflowId = _cachedWorkflowId ?? XiansContext.WorkflowId ?? throw new InvalidOperationException("WorkflowId is required"),
            WorkflowType = XiansContext.WorkflowType,
            ParticipantId = Message.ParticipantId,
            Scope = Message.Scope,
            TenantId = Message.TenantId,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Builds a last task ID request.
    /// Shared business logic used by GetLastTaskIdAsync.
    /// </summary>
    private GetLastTaskIdRequest BuildLastTaskIdRequest()
    {
        return new GetLastTaskIdRequest
        {
            WorkflowId = _cachedWorkflowId ?? string.Empty,
            ParticipantId = Message.ParticipantId,
            Scope = Message.Scope,
            TenantId = Message.TenantId
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
            ParticipantId = Message.ParticipantId,
            WorkflowId = _cachedWorkflowId ?? string.Empty,
            WorkflowType = XiansContext.WorkflowType,
            Text = content,
            Data = data ?? Message.Data,
            RequestId = Message.RequestId,
            Scope = Message.Scope,
            ThreadId = Message.ThreadId,
            Authorization = Message.Authorization,
            Hint = Message.Hint,
            Origin = null,
            Type = messageType,
            TenantId = Message.TenantId
        };
    }

    /// <summary>
    /// Builds a send handoff request.
    /// Shared business logic used by SendHandoffInternalAsync.
    /// </summary>
    private SendHandoffRequest BuildSendHandoffRequest(string? targetWorkflowId, string? targetWorkflowType, string text, object? data)
    {
        var agent = XiansContext.CurrentAgent;
        
        return new SendHandoffRequest
        {
            TargetWorkflowId = targetWorkflowId,
            TargetWorkflowType = targetWorkflowType,
            SourceAgent = agent.Name,
            SourceWorkflowType = XiansContext.WorkflowType,
            SourceWorkflowId = _cachedWorkflowId ?? string.Empty,
            ThreadId = Message.ThreadId!,
            ParticipantId = Message.ParticipantId,
            Authorization = Message.Authorization,
            Text = text,
            Data = data ?? Message.Data,
            TenantId = Message.TenantId
        };
    }

    #endregion
}

