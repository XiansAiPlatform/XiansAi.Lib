using Microsoft.Extensions.Logging;
using Xians.Lib.Agents.Messaging;
using Xians.Lib.Temporal.Workflows.Messaging.Models;
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
    private readonly A2ACurrentMessage _a2aMessage;
    private readonly ILogger _logger;

    internal A2AMessageContext(
        string text,
        A2ARequest request,
        string workflowId,
        string workflowType,
        A2AResponseCapture responseCapture)
        : base(
            text,
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
        
        // Create A2A-specific message with all context fields
        _a2aMessage = new A2ACurrentMessage(
            text,
            participantId: request.ParticipantId ?? XiansContext.WorkflowId,
            requestId: request.RequestId ?? request.CorrelationId,
            scope: request.Scope,
            hint: request.Hint,
            data: request.Data,
            tenantId: request.TenantId,
            authorization: request.Authorization,
            threadId: request.ThreadId ?? request.CorrelationId);
    }

    private bool _responseSent = false;

    /// <summary>
    /// Gets the A2A-specific message.
    /// </summary>
    public override CurrentMessage Message => _a2aMessage;

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
    /// Gets the target workflow ID where this A2A message is being processed.
    /// This is the workflow that is handling the A2A request, not the source workflow.
    /// </summary>
    public string TargetWorkflowId => _targetWorkflowId;

    /// <summary>
    /// Gets the target workflow type where this A2A message is being processed.
    /// This is the workflow that is handling the A2A request, not the source workflow.
    /// </summary>
    public string TargetWorkflowType => _targetWorkflowType;

    /// <summary>
    /// Sends a reply back to the calling agent.
    /// Captures the response instead of sending to a user.
    /// </summary>
    /// <param name="response">The response text to send.</param>
    public override Task ReplyAsync(string response)
    {
        CaptureResponse(response, null);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a reply with both text and data back to the calling agent.
    /// Captures the response instead of sending to a user.
    /// </summary>
    /// <param name="text">The text content to send.</param>
    /// <param name="data">The data object to send.</param>
    public override Task ReplyAsync(string text, object? data)
    {
        _logger.LogDebug("A2A response: {Text}, {Data}", text, data);
        CaptureResponse(text, data);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends data message back to the calling agent.
    /// Captures the response instead of sending to a user.
    /// </summary>
    /// <param name="data">The data object to send.</param>
    /// <param name="content">Optional text content to accompany the data.</param>
    public override Task SendDataAsync(object data, string? content = null)
    {
        CaptureResponse(content ?? string.Empty, data);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Chat history is not available for A2A messages.
    /// Returns an empty list since A2A messages are stateless one-off requests.
    /// </summary>
    public override Task<List<DbMessage>> GetChatHistoryAsync(int page = 1, int pageSize = 10)
    {
        _logger.LogDebug(
            "Chat history requested in A2A context - returning empty list. " +
            "A2A messages are stateless and don't have conversation history.");
        
        return Task.FromResult(new List<DbMessage>());
    }

    /// <summary>
    /// Hints are not available for A2A messages.
    /// Returns null since A2A messages are stateless one-off requests.
    /// </summary>
    public override Task<string?> GetLastHintAsync()
    {
        _logger.LogDebug(
            "Last hint requested in A2A context - returning null. " +
            "A2A messages are stateless and don't have hints.");
        
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Internal method to capture the A2A response.
    /// </summary>
    private void CaptureResponse(string text, object? data)
    {
        if (_responseSent)
        {
            _logger.LogWarning(
                "A2A response already sent from {SourceAgent}. Ignoring duplicate response.",
                SourceAgentName);
            return;
        }

        _logger.LogDebug(
            "Capturing A2A response: From={TargetAgent}, To={SourceAgent}",
            XiansContext.AgentName,
            SourceAgentName);

        _responseCapture.HasResponse = true;
        _responseCapture.Text = text;
        _responseCapture.Data = data;
        _responseSent = true;

        _logger.LogDebug(
            "A2A response captured from {Agent}",
            XiansContext.AgentName);
    }
}

