using Microsoft.Extensions.Logging;
using Temporalio.Workflows;
using Xians.Lib.Agents.Models;
using Xians.Lib.Workflows.Models;

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
    private bool _responseSent = false;

    internal A2AMessageContext(
        UserMessage message,
        A2ARequest request,
        string workflowId,
        string workflowType,
        A2AResponseCapture responseCapture)
        : base(message)
    {
        _request = request;
        _responseCapture = responseCapture;
        _targetWorkflowId = workflowId;
        _targetWorkflowType = workflowType;
    }

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
    /// Gets optional metadata from the request.
    /// </summary>
    public Dictionary<string, string>? Metadata => _request.Metadata;

    /// <summary>
    /// Sends a reply back to the calling agent.
    /// Overrides the base implementation to capture the response instead of sending to a user.
    /// </summary>
    /// <param name="response">The response text to send.</param>
    public override Task ReplyAsync(string response)
    {
        CaptureResponse(response, null);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a reply with both text and data back to the calling agent.
    /// Overrides the base implementation to capture the response instead of sending to a user.
    /// </summary>
    /// <param name="content">The text content to send.</param>
    /// <param name="data">The data object to send.</param>
    public override Task ReplyWithDataAsync(string content, object? data)
    {
        CaptureResponse(content, data);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Internal method to capture the A2A response.
    /// </summary>
    private void CaptureResponse(string text, object? data)
    {
        var logger = GetLogger();
        
        if (_responseSent)
        {
            logger.LogWarning(
                "A2A response already sent from {SourceAgent}. Ignoring duplicate response.",
                _request.SourceAgentName);
            return;
        }

        logger.LogDebug(
            "Capturing A2A response: From={TargetAgent}, To={SourceAgent}",
            XiansContext.AgentName,
            _request.SourceAgentName);

        _responseCapture.HasResponse = true;
        _responseCapture.Text = text;
        _responseCapture.Data = data;
        _responseSent = true;

        logger.LogInformation(
            "A2A response captured from {Agent}",
            XiansContext.AgentName);
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
    /// A2A messages use "a2a" scope.
    /// </summary>
    public override string Scope => "a2a";

    /// <summary>
    /// Overrides GetWorkflowType to return the target workflow's type, not the calling workflow.
    /// This ensures knowledge and other operations use the target agent's context.
    /// </summary>
    protected override string GetWorkflowType()
    {
        return _targetWorkflowType;
    }

    /// <summary>
    /// Gets a context-aware logger for A2A messages.
    /// </summary>
    protected override ILogger GetLogger()
    {
        return XiansLogger.GetLogger<A2AMessageContext>();
    }

    /// <summary>
    /// Chat history is not available for A2A messages.
    /// Returns an empty list since A2A messages are stateless one-off requests.
    /// </summary>
    public override Task<List<DbMessage>> GetChatHistoryAsync(int page = 1, int pageSize = 10)
    {
        var logger = GetLogger();
        logger.LogDebug(
            "Chat history requested in A2A context - returning empty list. " +
            "A2A messages are stateless and don't have conversation history.");
        
        // Return empty list for A2A - these are one-off requests without conversation context
        return Task.FromResult(new List<DbMessage>());
    }
}

