using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Agents.Messaging.Models;
using Xians.Lib.Workflows.Messaging.Models;
using Xians.Lib.Agents.Core;

namespace Xians.Lib.Agents.A2A;

/// <summary>
/// Specialized context for Agent-to-Agent messages.
/// Extends UserMessageContext to capture replies instead of sending them to users.
/// </summary>
public class A2AMessageContext : UserMessageContext
{
    private readonly A2ARequest _request;
    private readonly A2AResponseCapture _responseCapture;
    private readonly string _targetWorkflowId;
    private readonly string _targetWorkflowType;
    private readonly A2AMessageCollection _a2aMessages;
    private readonly ILogger _logger;

    internal A2AMessageContext(
        UserMessage message,
        A2ARequest request,
        string workflowId,
        string workflowType,
        A2AResponseCapture responseCapture)
        : base(
            message,
            participantId: request.ParticipantId ?? request.CorrelationId,
            requestId: request.RequestId ?? request.CorrelationId,
            scope: request.Scope ?? "a2a",
            hint: request.Hint ?? string.Empty,  // Hint is for message processing, not agent name
            data: request.Data ?? new object(),
            tenantId: request.TenantId,
            authorization: request.Authorization,
            threadId: request.ThreadId ?? request.CorrelationId,
            metadata: request.Metadata)
    {
        _request = request;
        _responseCapture = responseCapture;
        _targetWorkflowId = workflowId;
        _targetWorkflowType = workflowType;
        _logger = XiansLogger.GetLogger<A2AMessageContext>();
        
        // Create A2A-specific message collection
        _a2aMessages = new A2AMessageCollection(this, _responseCapture);
    }

    /// <summary>
    /// Gets the A2A-specific messaging operations collection.
    /// Overrides base to provide A2A reply behavior that captures responses instead of sending to users.
    /// </summary>
    public override MessageCollection Messages => _a2aMessages;

    /// <summary>
    /// Gets the source agent name that sent this A2A request.
    /// </summary>
    public string SourceAgentName => _request.SourceAgentName;

    /// <summary>
    /// Gets the source workflow ID that sent this A2A request.
    /// </summary>
    public string SourceWorkflowId => _request.SourceWorkflowId;

    /// <summary>
    /// Gets the source workflow type that sent this A2A request.
    /// </summary>
    public string SourceWorkflowType => _request.SourceWorkflowType;

    /// <summary>
    /// Gets the correlation ID for this A2A request.
    /// </summary>
    public string CorrelationId => _request.CorrelationId;

    /// <summary>
    /// Sends a reply back to the calling agent.
    /// Captures the response instead of sending to a user.
    /// Delegates to the message collection for consistency.
    /// </summary>
    /// <param name="response">The response text to send.</param>
    public Task ReplyAsync(string response)
    {
        return _a2aMessages.ReplyAsync(response);
    }

    /// <summary>
    /// Sends a reply with both text and data back to the calling agent.
    /// Captures the response instead of sending to a user.
    /// Delegates to the message collection for consistency.
    /// </summary>
    /// <param name="content">The text content to send.</param>
    /// <param name="data">The data object to send.</param>
    public Task ReplyWithDataAsync(string content, object? data)
    {
        return _a2aMessages.ReplyWithDataAsync(content, data);
    }

    /// <summary>
    /// Gets the tenant ID for this A2A message.
    /// Overrides the base property to return the tenant from the A2A request.
    /// </summary>
    public override string TenantId => _request.TenantId;

    /// <summary>
    /// Gets the request ID for this A2A message.
    /// </summary>
    public override string RequestId => _request.CorrelationId;

    /// <summary>
    /// Gets the scope for this A2A message.
    /// A2A messages use "A2A" scope.
    /// </summary>
    public override string Scope => "A2A";

    /// <summary>
    /// Chat history is not available for A2A messages.
    /// Returns an empty list since A2A messages are stateless one-off requests.
    /// </summary>
    [Obsolete("Use ctx.Messages.GetHistoryAsync() instead. This method will be removed in a future version.")]
    public Task<List<DbMessage>> GetChatHistoryAsync(int page = 1, int pageSize = 10)
    {
        // Delegate to the A2A message collection which returns empty list
        return _a2aMessages.GetHistoryAsync(page, pageSize);
    }
}

